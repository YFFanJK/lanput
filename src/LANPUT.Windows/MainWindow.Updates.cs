using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using LANPUT.Core;

namespace LANPUT.Windows;

public partial class MainWindow
{
    sealed record UpdateOptions(string FeedUrl = "", bool CheckOnStartup = true);
    UpdateOptions updateOptions = new();
    bool checkingUpdates;
    static string UpdateOptionsPath => Path.Combine(DataPath, "updates.json");
    static string CurrentVersion => typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "0.9.4";

    void InitializeUpdates()
    {
        try
        {
            var path = File.Exists(UpdateOptionsPath) ? UpdateOptionsPath : Path.Combine(AppContext.BaseDirectory, "update-defaults.json");
            if (File.Exists(path)) updateOptions = JsonSerializer.Deserialize<UpdateOptions>(File.ReadAllText(path)) ?? new();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        { Log("更新配置读取失败 · " + error.GetType().Name); }
        UpdateFeedBox.Text = updateOptions.FeedUrl;
        AutoUpdateCheckBox.IsChecked = updateOptions.CheckOnStartup;
        UpdateStatus.Text = $"当前版本 {CurrentVersion} · " + (string.IsNullOrWhiteSpace(updateOptions.FeedUrl) ? "尚未配置更新地址" : "等待检查更新");
    }

    bool SaveUpdateOptions()
    {
        var url = UpdateFeedBox.Text.Trim();
        try
        {
            if (url.Length > 0) SoftwareUpdates.RequireHttps(url);
            var options = new UpdateOptions(url, AutoUpdateCheckBox.IsChecked == true);
            File.WriteAllText(UpdateOptionsPath + ".tmp", JsonSerializer.Serialize(options));
            File.Move(UpdateOptionsPath + ".tmp", UpdateOptionsPath, true);
            updateOptions = options;
            return true;
        }
        catch (Exception error) when (error is FormatException or IOException or UnauthorizedAccessException)
        {
            UpdateStatus.Text = error is FormatException ? error.Message : "更新配置保存失败，请检查目录权限。";
            return false;
        }
    }

    void SaveUpdateOptions_Click(object sender, RoutedEventArgs e)
    {
        if (SaveUpdateOptions()) UpdateStatus.Text = string.IsNullOrWhiteSpace(updateOptions.FeedUrl)
            ? "配置已保存 · 尚未配置更新地址" : "更新配置已保存";
    }

    async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        if (SaveUpdateOptions()) await CheckForUpdatesAsync(manual: true);
    }

    async Task CheckForUpdatesAsync(bool manual)
    {
        if (closing || checkingUpdates || (!manual && !updateOptions.CheckOnStartup)) return;
        if (string.IsNullOrWhiteSpace(updateOptions.FeedUrl))
        { UpdateStatus.Text = $"当前版本 {CurrentVersion} · 尚未配置更新地址"; return; }
        checkingUpdates = true; CheckUpdatesButton.IsEnabled = false;
        UpdateStatus.Text = "正在检查更新…";
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            var release = await SoftwareUpdates.CheckAsync(updateOptions.FeedUrl, timeout.Token);
            if (closing) return;
            if (!SoftwareUpdates.IsNewer(release, CurrentVersion))
            { UpdateStatus.Text = $"当前版本 {CurrentVersion} · 已是最新稳定版"; return; }
            UpdateStatus.Text = $"发现新版本 {release.Version}（当前 {CurrentVersion}）";
            var url = SoftwareUpdates.RequireHttps(release.DownloadUrl);
            var answer = MessageBox.Show(this, $"发现 LANPUT {release.Version}，当前为 {CurrentVersion}。\n\n{release.Notes}\n\n下载网站：{url.Host}\n是否打开浏览器下载新版？下载后运行安装包即可升级。",
                "LANPUT 更新", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (answer == MessageBoxResult.Yes && !closing)
                Process.Start(new ProcessStartInfo(url.AbsoluteUri) { UseShellExecute = true });
        }
        catch (OperationCanceledException)
        { if (!closing) UpdateStatus.Text = "检查更新超时，请稍后重试。"; }
        catch (System.Net.Http.HttpRequestException error) when (error.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            if (!closing) UpdateStatus.Text = "更新清单未找到（HTTP 404），请确认服务器已发布版本及地址正确。";
            Log("检查更新未完成 · HTTP 404 · 未找到更新清单");
        }
        catch (Exception error)
        {
            if (!closing) UpdateStatus.Text = "暂时无法检查更新，请检查网络、地址或更新文件格式。";
            Log("检查更新未完成 · " + error.GetType().Name);
        }
        finally
        { checkingUpdates = false; if (!closing) CheckUpdatesButton.IsEnabled = true; }
    }
}
