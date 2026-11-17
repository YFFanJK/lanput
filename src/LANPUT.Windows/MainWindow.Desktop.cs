using System.IO;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using LANPUT.Core;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace LANPUT.Windows;
public partial class MainWindow
{
    DesktopPreferences preferences = DesktopPreferences.Load();
    readonly PlaybackPower power = new();
    Forms.NotifyIcon? tray;
    System.Drawing.Icon? trayIcon;
    bool exitRequested, receivingRequested, forceRecovery;
    string? boundAddress, boundName;
    DateTime nextNetworkCheck, controlsActiveUntil = DateTime.UtcNow.AddSeconds(3), volumeChangedAt;
    int rememberedVolume = -1;
    Point lastPointer = new(double.NaN, double.NaN);
    void InitializeDesktopFeatures()
    {
        KeepAwakeBox.IsChecked = preferences.KeepAwake;
        CloseToTrayBox.IsChecked = preferences.CloseToTray;
        AutoReceiveBox.IsChecked = preferences.AutoReceive;
        CompatibilityBox.IsChecked = preferences.CompatibilityRendering;
        if (preferences.HasBounds && double.IsFinite(preferences.Left) && double.IsFinite(preferences.Top)
            && double.IsFinite(preferences.Width) && double.IsFinite(preferences.Height))
        {
            Width = Math.Clamp(preferences.Width, MinWidth, Math.Max(MinWidth, SystemParameters.VirtualScreenWidth));
            Height = Math.Clamp(preferences.Height, MinHeight, Math.Max(MinHeight, SystemParameters.VirtualScreenHeight));
            Left = Math.Clamp(preferences.Left, SystemParameters.VirtualScreenLeft,
                SystemParameters.VirtualScreenLeft + Math.Max(0, SystemParameters.VirtualScreenWidth - Width));
            Top = Math.Clamp(preferences.Top, SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenTop + Math.Max(0, SystemParameters.VirtualScreenHeight - Height));
            WindowStartupLocation = WindowStartupLocation.Manual;
            if (preferences.Maximized) WindowState = WindowState.Maximized;
        }
        playback.SetVolume(Math.Clamp(preferences.Volume, 0, 100));
        rememberedVolume = Math.Clamp(preferences.Volume, 0, 100);
        PreviewMouseMove += Playback_MouseMove;
        OverlayLayer.PreviewMouseMove += Playback_MouseMove;
        OverlayLayer.PreviewMouseDown += (_, _) => ShowPlaybackControls();
        NetworkChange.NetworkAddressChanged += Network_AddressChanged;
        SystemEvents.PowerModeChanged += PowerMode_Changed;
        try
        {
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("显示 LANPUT", null, (_, _) => Dispatcher.InvokeAsync(RestoreFromTray));
            menu.Items.Add("播放 / 暂停", null, (_, _) => Dispatcher.InvokeAsync(() => PlayPause_Click(this, new RoutedEventArgs())));
            menu.Items.Add("开始 / 停止接收", null, (_, _) => Dispatcher.InvokeAsync(() => QuickReceive_Click(this, new RoutedEventArgs())));
            menu.Items.Add("退出", null, (_, _) => Dispatcher.InvokeAsync(() => { exitRequested = true; RestoreFromTray(); Close(); }));
            using var iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/LANPUT.ico")).Stream;
            using var loadedIcon = new System.Drawing.Icon(iconStream, new System.Drawing.Size(32, 32));
            trayIcon = (System.Drawing.Icon)loadedIcon.Clone();
            tray = new Forms.NotifyIcon { Text = "LANPUT · 私人放映室", Icon = trayIcon,
                ContextMenuStrip = menu, Visible = true };
            tray.DoubleClick += (_, _) => Dispatcher.InvokeAsync(RestoreFromTray);
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        { Log("托盘初始化失败 · " + e.GetType().Name); tray?.Dispose(); tray = null; }
    }
    public void RestoreFromTray()
    {
        if (closing || closed) return;
        Show(); if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate(); ShowPlaybackControls();
    }
    void HideToTray_Click(object sender, RoutedEventArgs e)
    {
        if (tray is null) { Log("托盘不可用，窗口保持显示。"); return; }
        SettingsPopup.IsOpen = DiagnosticsPopup.IsOpen = PlaybackInfoPopup.IsOpen = false; SaveDesktopPreferences(); Hide();
    }
    void DesktopOption_Click(object sender, RoutedEventArgs e)
    {
        preferences = preferences with { KeepAwake = KeepAwakeBox.IsChecked == true, CloseToTray = CloseToTrayBox.IsChecked == true };
        SaveDesktopPreferences(); power.Update(preferences.KeepAwake && playback.Snapshot.State == "PLAYING" && playback.Snapshot.Status == "OK");
    }
    void SaveDesktopPreferences()
    {
        var bounds = fullscreen || WindowState != WindowState.Normal ? RestoreBounds : new Rect(Left, Top, ActualWidth, ActualHeight);
        // Fullscreen uses maximized bounds; retain the last genuine window geometry.
        preferences = preferences with { Volume = Math.Clamp(playback.Snapshot.Volume, 0, 100) };
        if (!fullscreen && bounds.Width >= MinWidth && bounds.Height >= MinHeight)
            preferences = preferences with { Left = bounds.Left, Top = bounds.Top, Width = bounds.Width, Height = bounds.Height,
                HasBounds = true, Maximized = WindowState == WindowState.Maximized };
        if (!preferences.Save()) Log("窗口和音量设置保存失败，请检查用户目录权限。");
    }
    void ShowPlaybackControls()
    {
        controlsActiveUntil = DateTime.UtcNow.AddSeconds(3);
        SetPlaybackControlsVisible(true);
        if (fullscreen && (cinemaVideoRevealed || musicMode)) ExitFullscreenButton.Visibility = Visibility.Visible;
    }
    void Playback_MouseMove(object sender, MouseEventArgs e)
    {
        var point = e.GetPosition(OverlayLayer);
        if (point == lastPointer) return;
        lastPointer = point; ShowPlaybackControls();
    }
    void UpdateDesktopFeatures(PlaybackSnapshot snapshot)
    {
        power.Update(preferences.KeepAwake && snapshot.State == "PLAYING" && snapshot.Status == "OK");
        if (snapshot.Volume != rememberedVolume) { rememberedVolume = snapshot.Volume; volumeChangedAt = DateTime.UtcNow; }
        if (snapshot.Volume != preferences.Volume && DateTime.UtcNow - volumeChangedAt > TimeSpan.FromSeconds(2)) SaveDesktopPreferences();
        bool hide = fullscreen && playback.Activity.Phase == PlaybackPhase.Playing && snapshot.State == "PLAYING" && snapshot.Status == "OK" && playback.HasVideoOutput
            && DateTime.UtcNow > controlsActiveUntil && !ControlsPanel.IsMouseOver
            && !dragging && Mouse.Captured is null && !SettingsPopup.IsOpen && !DiagnosticsPopup.IsOpen && !PlaybackInfoPopup.IsOpen;
        if (hide && ControlsPanel.IsKeyboardFocusWithin) Keyboard.Focus(OverlayLayer);
        SetPlaybackControlsVisible(!hide);
    }
    void Network_AddressChanged(object? sender, EventArgs e) => Dispatcher.InvokeAsync(() =>
    { if (!closing) nextNetworkCheck = DateTime.UtcNow.AddSeconds(2); });
    void PowerMode_Changed(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume) Dispatcher.InvokeAsync(() =>
        { if (!closing) { power.Dispose(); forceRecovery = true; nextNetworkCheck = DateTime.UtcNow.AddSeconds(2); } });
    }
    async Task RecoverReceiverAsync()
    {
        nextNetworkCheck = DateTime.UtcNow.AddSeconds(5);
        if (!receivingRequested || busy || closing) return;
        busy = true; SetButtons(receiver is not null);
        try
        {
            var choices = NetworkChoice.Enumerate();
            var exact = choices.FirstOrDefault(n => n.Address.ToString() == boundAddress && n.Name == boundName);
            if (receiver is not null && exact is not null && !forceRecovery) return;
            var network = ReceiverRecovery.SelectNetwork(choices, boundName, boundAddress);
            if (receiver is not null) await StopReceiver(stopPlayback: false);
            if (network is null)
            { receiverPresentationFailed = false; ServiceStatus.Text = "等待原网卡恢复 · 可停止后另选网卡"; return; }
            receiver = await CreateReceiverHostAsync(network);
            NetworkBox.ItemsSource = choices; NetworkBox.SelectedItem = network;
            ConnectionCheckSummary.Text = "原网卡接收服务已恢复；如手机断开，请重新投放。";
            boundAddress = network.Address.ToString(); forceRecovery = false;
            settings = settings with { Address = boundAddress, NetworkName = network.Name };
            try { File.WriteAllText(Path.Combine(DataPath, "settings.json"), JsonSerializer.Serialize(settings)); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            { Log("网络接收已恢复，但设置保存未完成 · " + error.GetType().Name); }
            receiverPresentationFailed = false;
            ServiceStatus.Text = "接收中 · " + boundAddress;
            FooterHint.Text = "网络已恢复 · 如手机断开，请重新选择 LANPUT 投放";
            Log("网络接收服务已恢复 · 保留设备身份；手机可能需要重新投放。");
        }
        catch (Exception ex)
        { receiverPresentationFailed = true; ServiceStatus.Text = "网络恢复失败 · 5 秒后重试"; Log("网络恢复暂未完成 · " + ex.GetType().Name); }
        finally { busy = false; SetButtons(receivingRequested); }
    }
    internal void DisposeDesktopFeatures()
    {
        NetworkChange.NetworkAddressChanged -= Network_AddressChanged;
        SystemEvents.PowerModeChanged -= PowerMode_Changed;
        power.Dispose();
        if (tray is not null) { tray.Visible = false; tray.ContextMenuStrip?.Dispose(); tray.Dispose(); tray = null; }
        trayIcon?.Dispose(); trayIcon = null;
    }
}
