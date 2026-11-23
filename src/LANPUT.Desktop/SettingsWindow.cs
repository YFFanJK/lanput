using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using LANPUT.Core;

namespace LANPUT.Desktop;

internal sealed class SettingsWindow : Window
{
    readonly CancellationTokenSource stop = new();
    public SettingsWindow(DesktopSettings current)
    {
        Title = "接收与播放设置"; Width = 620; Height = 670;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var name = new TextBox { Text = current.DeviceName, MaxLength = 80 };
        var networks = NetworkChoice.Enumerate();
        var network = new ComboBox { ItemsSource = networks, HorizontalAlignment = HorizontalAlignment.Stretch,
            SelectedItem = networks.FirstOrDefault(n => n.Address.ToString() == current.NetworkAddress) ?? networks.FirstOrDefault() };
        var auto = new CheckBox { Content = "打开软件后自动接收", IsChecked = current.AutoReceive };
        var motion = new CheckBox { Content = "影院换场与黑胶动画（关闭以减少动态效果）", IsChecked = current.Animations };
        var online = new CheckBox { Content = "自动在线搜索歌词（向 LRCLIB 发送歌名、歌手）", IsChecked = current.OnlineLyrics };
        var feed = new TextBox { Text = current.UpdateFeed, Watermark = "本平台专用的 HTTPS 更新清单地址" };
        var notice = new TextBlock { Text = "当前平台：" + DesktopSettings.PlatformId + "。更新清单需对应本平台和架构。", Foreground = Brush.Parse("#A8B4BE"), TextWrapping = TextWrapping.Wrap };
        var check = new Button { Content = "检查更新" };
        var download = new Button { Content = "前往下载", IsVisible = false };
        Uri? downloadUri = null;
        download.Click += (_, _) =>
        {
            if (downloadUri is null) return;
            try { Process.Start(new ProcessStartInfo(downloadUri.AbsoluteUri) { UseShellExecute = true }); }
            catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException) { notice.Text = "未能打开浏览器，请稍后重试。"; }
        };
        check.Click += async (_, _) =>
        {
            download.IsVisible = false; check.IsEnabled = false; downloadUri = null;
            try
            {
                if (string.IsNullOrWhiteSpace(feed.Text)) { notice.Text = "请先填写 " + DesktopSettings.PlatformId + " 专用更新地址。"; return; }
                var release = await SoftwareUpdates.CheckAsync(feed.Text, stop.Token);
                PlatformUpdatePolicy.Validate(release, DesktopSettings.PlatformId);
                notice.Text = SoftwareUpdates.IsNewer(release, "0.9.5") ? "发现新版本 " + release.Version + "\n" + release.Notes : "当前已是最新版本。";
                if (SoftwareUpdates.IsNewer(release, "0.9.5")) { downloadUri = SoftwareUpdates.RequireHttps(release.DownloadUrl); download.IsVisible = true; }
            }
            catch (OperationCanceledException) { }
            catch (Exception e) when (e is HttpRequestException or FormatException or System.Text.Json.JsonException)
            { notice.Text = "检查失败：请确认清单可访问、格式正确，且安装包对应当前平台。"; }
            finally { check.IsEnabled = true; }
        };
        var save = new Button { Content = "保存", Classes = { "primary" } };
        var cancel = new Button { Content = "取消" };
        cancel.Click += (_, _) => Close(null);
        save.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(name.Text)) { notice.Text = "请输入设备名称。"; return; }
            try { if (!string.IsNullOrWhiteSpace(feed.Text)) SoftwareUpdates.RequireHttps(feed.Text); }
            catch (FormatException) { notice.Text = "更新地址必须使用 HTTPS。"; return; }
            Close(current with { DeviceName = name.Text.Trim(), NetworkAddress = (network.SelectedItem as NetworkChoice)?.Address.ToString() ?? "",
                AutoReceive = auto.IsChecked == true, Animations = motion.IsChecked == true, OnlineLyrics = online.IsChecked == true, UpdateFeed = feed.Text?.Trim() ?? "" });
        };
        var panel = new StackPanel { Spacing = 12, Margin = new Thickness(28) };
        void Label(string text) => panel.Children.Add(new TextBlock { Text = text });
        Label("手机中显示的设备名称"); panel.Children.Add(name);
        Label("接收网卡"); panel.Children.Add(network);
        panel.Children.Add(auto); panel.Children.Add(motion); panel.Children.Add(online);
        Label("在线更新"); panel.Children.Add(feed); panel.Children.Add(notice);
        panel.Children.Add(new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8, Children = { check, download } });
        panel.Children.Add(new TextBlock { Text = "局域网发现需要 UDP 1900，媒体控制需要 TCP 41000。\n更换设备名或网卡后会自动重启接收。", Foreground = Brush.Parse("#A8B4BE"), TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Children = { cancel, save } });
        Content = new ScrollViewer { Content = panel };
        Closed += (_, _) => { stop.Cancel(); };
    }
}
