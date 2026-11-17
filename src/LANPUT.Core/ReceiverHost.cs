using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace LANPUT.Core;

public sealed class ReceiverHost(NetworkChoice network, string name, Guid id, int port, IPlayback player, Action<string> log) : IAsyncDisposable
{
    WebApplication? app;
    SsdpService? ssdp;
    readonly EventSubscriptions events = new();
    readonly CancellationTokenSource stop = new();
    Task? eventTask;
    int disposed;
    public Uri Origin { get; } = new($"http://{network.Address}:{port}/");
    public async Task StartAsync(bool discovery = true)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders(); // Never write request URLs / signed media URLs to default logs.
        builder.WebHost.ConfigureKestrel(o =>
        {
            o.Listen(network.Address, port);
            o.Limits.MaxRequestBodySize = 262144;
            o.Limits.MaxConcurrentConnections = 32;
            o.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(5);
        });
        app = builder.Build();
        var router = new SoapRouter(player);
        app.Use(async (ctx, next) =>
        {
            var remote = ctx.Connection.RemoteIpAddress;
            if (remote is null || !network.Contains(remote)) { ctx.Response.StatusCode = 403; return; }
            ctx.Response.Headers["Server"] = "Windows/10.0 UPnP/1.0 LanCast/0.2";
            try { await next(ctx); }
            catch (BadHttpRequestException e) { ctx.Response.StatusCode = e.StatusCode; }
            catch (OperationCanceledException) { }
            catch { if (!ctx.Response.HasStarted) ctx.Response.StatusCode = 500; log("请求处理失败（未记录请求内容）"); }
        });
        app.MapGet("/description.xml", () =>
        {
            log("手机已获取设备描述");
            return Results.Text(UpnpProtocol.Description(name, id, Origin), "text/xml; charset=utf-8");
        });
        app.MapGet("/health", () => Results.Json(new { receiver = "LANPUT", version = "0.4.2", status = player.Snapshot.State }));
        foreach (var service in UpnpProtocol.Services)
        {
            var key = UpnpProtocol.Key(service);
            app.MapGet($"/{key}/scpd.xml", () => Results.Text(Scpd.Document(service), "text/xml; charset=utf-8"));
            app.MapPost($"/{key}/control", async ctx =>
            {
                ctx.Response.ContentType = "text/xml; charset=utf-8";
                try
                {
                    using var reader = new StreamReader(ctx.Request.Body);
                    var body = await reader.ReadToEndAsync(ctx.RequestAborted);
                    var result = router.Execute(service, ctx.Request.Headers["SOAPACTION"].ToString(), body);
                    var action = ctx.Request.Headers["SOAPACTION"].ToString().Trim('"').Split('#').Last();
                    // Action is validated against the fixed action table before logging.
                    if (!action.StartsWith("Get", StringComparison.Ordinal)) log($"手机控制 · {key}/{action}");
                    await ctx.Response.WriteAsync(result, ctx.RequestAborted);
                }
                catch (UpnpException e)
                {
                    ctx.Response.StatusCode = 500;
                    log($"协议请求未完成 · UPnP {e.Code}");
                    await ctx.Response.WriteAsync(UpnpProtocol.Fault(e.Code, e.Message), ctx.RequestAborted);
                }
            });
            app.MapMethods($"/{key}/event", ["SUBSCRIBE", "UNSUBSCRIBE"], async ctx =>
            {
                var sid = ctx.Request.Headers["SID"].ToString();
                var owner = ctx.Connection.RemoteIpAddress!;
                var callback = ctx.Request.Headers["CALLBACK"].ToString();
                var nt = ctx.Request.Headers["NT"].ToString();
                if (ctx.Request.Method == "UNSUBSCRIBE")
                {
                    ctx.Response.StatusCode = callback.Length == 0 && nt.Length == 0 && events.Remove(sid, service, owner) ? 200 : 412;
                    return;
                }
                var seconds = EventSubscriptions.TimeoutSeconds(ctx.Request.Headers["TIMEOUT"].ToString());
                if (sid.Length != 0)
                {
                    if (callback.Length != 0 || nt.Length != 0 || !events.Renew(sid, service, owner, seconds)) { ctx.Response.StatusCode = 412; return; }
                }
                else
                {
                    if (!nt.Equals("upnp:event", StringComparison.OrdinalIgnoreCase)) { ctx.Response.StatusCode = 412; return; }
                    try { sid = events.Add(service, callback, owner, seconds); }
                    catch (UpnpException e) { ctx.Response.StatusCode = e.Code; return; }
                    var initialSid = sid;
                    ctx.Response.OnCompleted(() => events.NotifyAsync(player.Snapshot, initialSid));
                }
                ctx.Response.Headers["SID"] = sid; ctx.Response.Headers["TIMEOUT"] = $"Second-{seconds}";
                ctx.Response.StatusCode = 200;
                await Task.CompletedTask;
            });
        }
        try
        {
            await app.StartAsync();
            if (discovery) { ssdp = new SsdpService(network, id, new Uri(Origin, "description.xml"), log); ssdp.Start(); }
            eventTask = EventLoopAsync();
            log($"HTTP 接收服务已启动 · {Origin} · TCP {port}");
        }
        catch { await DisposeAsync(); throw; }
    }
    async Task EventLoopAsync()
    {
        PlaybackSnapshot? previous = null;
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));
            while (await timer.WaitForNextTickAsync(stop.Token))
            {
                var current = player.Snapshot with { PositionMs = 0 };
                if (current == previous) continue;
                previous = current; await events.NotifyAsync(current);
            }
        }
        catch (OperationCanceledException) { }
    }
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        stop.Cancel();
        try
        {
            if (ssdp is not null) await ssdp.DisposeAsync();
            if (eventTask is not null) await eventTask;
        }
        finally
        {
            // Even an SSDP cleanup error must not retain the TCP listener.
            try
            {
                if (app is not null)
                {
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    try { await app.StopAsync(timeout.Token); }
                    catch (OperationCanceledException) { }
                    finally { await app.DisposeAsync(); }
                }
            }
            finally
            {
                try { await events.DisposeAsync(); }
                finally { stop.Dispose(); }
            }
        }
    }
}
