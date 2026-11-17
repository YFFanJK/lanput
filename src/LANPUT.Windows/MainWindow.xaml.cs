using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Input;
using System.Windows.Threading;
using LANPUT.Core;
using Microsoft.Win32;

namespace LANPUT.Windows;

public partial class MainWindow : Window
{
    sealed record Settings(Guid Id, string Name, string? Address = null, string? NetworkName = null);
    static readonly string DataPath = AppStorage.DataDirectory;
    readonly string logPath;
    readonly object logGate = new();
    readonly RotatingLog sessionLog;
    DateTime lastSearchLog = DateTime.MinValue;
    int suppressedSearchLogs;
    readonly Queue<string> logLines = new();
    readonly DispatcherTimer timer;
    readonly VlcPlayback playback;
    Settings settings;
    ReceiverHost? receiver;
    bool dragging, syncingVolume, closing, closed, busy, fullscreen, copying;
    WindowState savedState;
    WindowChrome? cinemaChrome;
    readonly CancellationTokenSource lifetime = new();
    public MainWindow()
    {
        InitializeComponent();
        cinemaChrome = WindowChrome.GetWindowChrome(this);
        Directory.CreateDirectory(DataPath);
        Directory.CreateDirectory(Path.Combine(DataPath, "logs"));
        logPath = Path.Combine(DataPath, "logs", "receiver-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Environment.ProcessId + ".log");
        sessionLog = new RotatingLog(logPath);
        RotatingLog.Cleanup(Path.Combine(DataPath, "logs"), DateTime.UtcNow);
        settings = LoadSettings(); DeviceName.Text = settings.Name; ReceiverNameLabel.Text = settings.Name;
        playback = new VlcPlayback(Dispatcher, Log, preferences.CompatibilityRendering);
        playback.PresentationStateChanged += () =>
        { if (!closing && !closed) UpdateInterfacePresentation(playback.Snapshot); };
        Loaded += async (_, _) =>
        {
            Video.ApplyTemplate(); Video.UpdateLayout(); Video.MediaPlayer = playback.Player;
            Log("视频窗口已初始化；播放时将再次记录窗口绑定状态。");
            await TryAutomaticReceiveAsync();
            await CheckForUpdatesAsync(manual: false);
        };
        InitializeInterfaceAccessibility();
        InitializeDesktopFeatures();
        InitializeUpdates();
        InitializeInterfaceMotion();
        InitializeStartupPresentation();
        InitializeMusicTransition();
        // Slider's move-to-point class handler marks mouse input handled. Observe
        // that routed input too, otherwise polling overwrites the selected value.
        PositionSlider.AddHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler((s, e) =>
        { if (e.ChangedButton == MouseButton.Left) Seek_Down(s, e); }), true);
        PositionSlider.AddHandler(Mouse.PreviewMouseUpEvent, new MouseButtonEventHandler((s, e) =>
        { if (e.ChangedButton == MouseButton.Left) Seek_Up(s, e); }), true);
        NetworkBox.SelectionChanged += (_, _) =>
        {
            ConnectionCheckSummary.Text = "网卡选择已更新；开始接收前将重新检查。";
            ConnectionCheckSummary.ToolTip = null;
            ConnectionCheckSummary.Foreground = (Brush)FindResource("TextMuted");
        };
        RefreshNetworks();
        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        timer.Tick += (_, _) => UpdatePlayback(); timer.Start();
        PreviewKeyDown += HandlePreviewKey;
        if (AppStorage.MigrationNotice is not null) Log(AppStorage.MigrationNotice);
        Log("LANPUT 0.9.4 · 目标：Mate 80 Pro Max / HarmonyOS 7.0 / 华为视频 1.2.50 / Windows 11");
        Log("请选择手机所在局域网的网卡，启动后允许 Windows 防火墙在专用网络中通信。");
    }
    static Settings LoadSettings()
    {
        try
        {
            var path = Path.Combine(DataPath, "settings.json");
            if (File.Exists(path) && JsonSerializer.Deserialize<Settings>(File.ReadAllText(path)) is { } s && s.Id != Guid.Empty) return s;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { }
        return new Settings(Guid.NewGuid(), "LANPUT · MateBook GT 14");
    }
    void RefreshNetworks()
    {
        var choices = NetworkChoice.Enumerate(); NetworkBox.ItemsSource = choices;
        NetworkBox.SelectedItem = choices.FirstOrDefault(c => c.Address.ToString() == settings.Address) ?? choices.FirstOrDefault();
        if (choices.Count == 0) Log("未找到可用的私人 IPv4 网卡，请连接路由器后刷新。VPN、访客网络或 AP 隔离可能影响发现。");
    }
    void Log(string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss}  {message}";
        lock (logGate)
        {
            if (message == "收到设备搜索，已安排回应")
            {
                if (DateTime.UtcNow - lastSearchLog < TimeSpan.FromSeconds(5)) { suppressedSearchLogs++; return; }
                lastSearchLog = DateTime.UtcNow;
                if (suppressedSearchLogs > 0) { line += $"（合并 {suppressedSearchLogs} 条重复搜索）"; suppressedSearchLogs = 0; }
            }
            sessionLog.Append(line + Environment.NewLine);
        }
        Dispatcher.InvokeAsync(() =>
        {
            if (closed) return;
            logLines.Enqueue(line); while (logLines.Count > 180) logLines.Dequeue();
            LogBox.Text = string.Join(Environment.NewLine, logLines); LogBox.ScrollToEnd();
        });
    }
    async void Start_Click(object sender, RoutedEventArgs e) => await StartReceiverAsync(automatic: false);
    async Task StartReceiverAsync(bool automatic)
    {
        if (busy || checkingConnection || receiver is not null || closing) return;
        if (NetworkBox.SelectedItem is not NetworkChoice network || string.IsNullOrWhiteSpace(DeviceName.Text))
        {
            if (!automatic) MessageBox.Show("请填写设备名称，并选择手机所在局域网的网卡。", "LANPUT");
            return;
        }
        receiverPresentationFailed = false;
        busy = true; SetButtons(false); ServiceStatus.Text = "检查连接条件…";
        try
        {
            var readiness = await CheckConnectionAsync(network);
            if (!readiness.CanStart)
            {
                receiverPresentationFailed = true; ServiceStatus.Text = "连接条件需处理";
                FooterHint.Text = "请在接收设置中查看连接检查结果";
                ShowMotionToast("连接检查发现问题，请查看接收设置", "Error");
                if (!automatic && !SettingsPopup.IsOpen) Settings_Click(this, new RoutedEventArgs());
                return;
            }
            ServiceStatus.Text = "正在启动…";
            var updatedSettings = settings with { Name = DeviceName.Text.Trim(), Address = network.Address.ToString(), NetworkName = network.Name };
            receiver = await CreateReceiverHostAsync(network, updatedSettings.Name);
            settings = updatedSettings;
            receivingRequested = true; forceRecovery = false; boundAddress = network.Address.ToString(); boundName = network.Name;
            try { File.WriteAllText(Path.Combine(DataPath, "settings.json"), JsonSerializer.Serialize(settings)); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            { Log("接收已开启，但设置保存未完成 · " + error.GetType().Name); }
            ServiceStatus.Text = "接收中 · " + network.Address;
            ReceiverNameLabel.Text = settings.Name;
            SettingsPopup.IsOpen = false;
            if (readiness.HasWarnings) ShowMotionToast("连接检查有提醒；详情见诊断", "Info");
            FooterHint.Text = "LANPUT 0.9.4";
            ConnectionCheckSummary.Text = "接收服务已开启；若手机找不到设备，请检查同一局域网与防火墙。";
            Log("等待手机 TV 按钮连接。此接收器不代表华为认证设备，不保证会员片源播放权限。");
        }
        catch (Exception ex)
        {
            receiverPresentationFailed = true; ServiceStatus.Text = "启动失败";
            Log("启动失败 · " + ex.GetType().Name + "。检查 TCP 41000 / UDP 1900、网卡状态及防火墙。");
            ConnectionCheckSummary.Text = "接收服务启动未完成，请重新检查网卡、端口和防火墙。";
            ShowMotionToast("接收服务未启动，请检查连接条件", "Error");
            if (!automatic) MessageBox.Show("接收服务未启动。请检查连接检查结果、网卡与防火墙。\n错误类型：" + ex.GetType().Name, "LANPUT");
        }
        finally { busy = false; SetButtons(receivingRequested); }
    }
    void SetButtons(bool running)
    {
        StartButton.IsEnabled = !busy && !running; StopButton.IsEnabled = !busy && running;
        QuickReceiveButton.IsEnabled = !busy;
        QuickReceiveButton.Content = running ? "停止接收" : "开始接收";
        QuickReceiveButton.Background = running ? Brushes.Transparent : (Brush)FindResource("Accent");
        QuickReceiveButton.Foreground = (Brush)FindResource(running ? "TextSecondary" : "TextOnAccent");
        NetworkBox.IsEnabled = DeviceName.IsEnabled = RefreshButton.IsEnabled = !busy && !running;
        ConnectionCheckButton.IsEnabled = !busy && !checkingConnection;
    }
    async Task StopReceiver(bool stopPlayback = true)
    {
        if (receiver is null) { if (stopPlayback) playback.Stop(); return; }
        var host = receiver; receiver = null;
        try { await host.DisposeAsync(); }
        finally { if (stopPlayback) playback.Stop(); }
        Log("接收已停止，已发送设备下线通知。");
    }
    async void Stop_Click(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        receivingRequested = false; receiverPresentationFailed = false;
        busy = true; SetButtons(true);
        try
        {
            await StopReceiver(); ServiceStatus.Text = "已停止";
            ShowMotionToast("接收已停止", "Idle");
            FooterHint.Text = "LANPUT 0.9.4";
        }
        catch (Exception ex) { ServiceStatus.Text = "已停止 · 清理异常"; Log("接收停止清理异常 · " + ex.GetType().Name); }
        finally { busy = false; SetButtons(false); }
    }
    void Refresh_Click(object sender, RoutedEventArgs e) => RefreshNetworks();
    void UpdatePlayback()
    {
        playback.Poll(); var p = playback.Snapshot;
        MediaTitle.Text = string.IsNullOrWhiteSpace(p.Title) || p.Title.Trim().Equals("null", StringComparison.OrdinalIgnoreCase)
            ? (p.Uri.Length == 0 ? "等待影片" : "正在投放的影片") : p.Title;
        PlaybackState.Foreground = (Brush)FindResource(p.Status != "OK" ? "TextError" : "TextMuted");
        var activity = playback.Activity;
        bool canPause = p.State == "PLAYING" || activity.Phase == PlaybackPhase.Buffering
            || (playback.AllowsTransitionPause && p.State != "PAUSED_PLAYBACK");
        PlayButton.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, canPause ? "暂停播放" : "开始播放");
        PlaybackState.Text = p.Uri.Length == 0 ? "尚无影片" : playback.AllowsTransitionPause && p.State != "PAUSED_PLAYBACK" ? "正在准备放映" : activity.Label;
        BufferingIndicator.Visibility = cinemaVideoRevealed && activity.Phase == PlaybackPhase.Buffering && playback.HasVideoOutput
            ? Visibility.Visible : Visibility.Collapsed;
        BufferingLabel.Text = activity.Label;
        MuteButton.ToolTip = p.Muted ? "取消静音 · M" : "静音 · M";
        MuteButton.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, p.Muted ? "取消静音" : "静音");
        MuteGlyph.Data = Geometry.Parse(p.Muted ? "M3 9H8L13 5V19L8 15H3Z M17 9L23 15 M23 9L17 15" : "M3 9H8L13 5V19L8 15H3Z M17 8A6 6 0 0 1 17 16");
        PlayGlyph.Data = Geometry.Parse(canPause ? "M8 5V19 M16 5V19" : "M8 5L19 12L8 19Z");
        PlayButton.ToolTip = canPause ? "暂停 · 空格" : "播放 · 空格";
        PlayButton.IsEnabled = p.Uri.Length > 0 && !busy;
        PositionSlider.IsEnabled = playback.CanSeek;
        IdlePrimaryButton.IsEnabled = !busy && p.State != "TRANSITIONING";
        IdlePrimaryButton.Content = p.Status != "OK" ? "重试播放" : activity.Phase == PlaybackPhase.Paused ? "继续播放" : receivingRequested ? "接收设置" : "开始接收";
        IdleDetailsButton.Content = p.Status != "OK" ? "查看诊断" : "打开本地视频";
        bool idleNeedsAction = p.Status != "OK" || activity.Phase == PlaybackPhase.Paused || !receivingRequested;
        IdlePrimaryButton.Background = idleNeedsAction ? (Brush)FindResource("Accent") : Brushes.Transparent;
        IdlePrimaryButton.Foreground = (Brush)FindResource(idleNeedsAction ? "TextOnAccent" : "TextSecondary");
        PlaybackState.Visibility = p.Uri.Length == 0 ? Visibility.Collapsed : Visibility.Visible;

        if (p.Status != "OK")
        {
            IdleTitle.Text = "影片暂时无法播放";
            IdleDetail.Text = "接收器仍在运行，可重试播放或查看诊断详情";
        }
        else if (activity.Phase == PlaybackPhase.Buffering)
        {
            IdleTitle.Text = "正在缓冲影片…";
            IdleDetail.Text = activity.BufferPercent is null ? "正在接收媒体数据，请稍候" : activity.Label + " · 正在接收媒体数据";
        }
        else if (activity.Phase == PlaybackPhase.Opening)
        {
            IdleTitle.Text = "正在连接媒体来源…"; IdleDetail.Text = "已收到播放请求，正在打开媒体";
        }
        else if (p.State == "PLAYING" && !playback.HasVideoOutput && !playback.IsAudioOnly)
        {
            IdleTitle.Text = "等待视频画面"; IdleDetail.Text = "正在准备画面，请稍候";
        }
        else if (activity.Phase == PlaybackPhase.Paused)
        {
            IdleTitle.Text = "已暂停播放"; IdleDetail.Text = "点击继续播放，恢复影片";
        }
        else if (receivingRequested && receiver is null)
        {
            IdleTitle.Text = "等待网络恢复";
            IdleDetail.Text = "等待原网卡重新连接，或在接收设置中停止后选择其他网卡";
        }
        else
        {
            IdleTitle.Text = p.Uri.Length == 0 ? "今晚，坐进故事里。" : "放映结束，欢迎再次入座。";
            IdleDetail.Text = receiver is null ? "点击上方“开始接收”，或打开本地视频" : "在手机播放器中点击 TV，选择上方的电脑名称";
        }
        PositionLabel.Text = XmlSafe.Time(p.PositionMs); DurationLabel.Text = XmlSafe.Time(p.DurationMs);
        PositionSlider.Maximum = Math.Max(1, p.DurationMs);
        if (!dragging) PositionSlider.Value = p.PositionMs;
        syncingVolume = true; VolumeSlider.Value = p.Volume; syncingVolume = false;
        UpdatePlaybackInfoPanel();
        UpdateDesktopFeatures(p);
        UpdateInterfacePresentation(p);
        if (DateTime.UtcNow >= nextNetworkCheck) _ = RecoverReceiverAsync();
    }
    void PlayPause_Click(object sender, RoutedEventArgs e)
    {
        if (playback.Snapshot.Uri.Length == 0) return;
        try { if (playback.Snapshot.State == "PLAYING" || playback.Activity.Phase == PlaybackPhase.Buffering
            || (playback.AllowsTransitionPause && playback.Snapshot.State != "PAUSED_PLAYBACK")) playback.Pause(); else playback.Play(); }
        catch (UpnpException) { Log("本机播放操作失败，请检查媒体来源。"); }
    }
    void StopPlayback_Click(object sender, RoutedEventArgs e) => playback.Stop();
    void Volume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    { if (!syncingVolume && playback is not null) playback.SetVolume((int)e.NewValue); }
    void Seek_Down(object sender, MouseButtonEventArgs e) => dragging = true;
    void Seek_Up(object sender, MouseButtonEventArgs e)
    {
        dragging = false;
        try { if (playback.Snapshot.Uri.Length != 0) playback.Seek((long)PositionSlider.Value); }
        catch (UpnpException) { Log("当前媒体暂不支持拖动进度。"); }
    }
    void ToggleFullscreen()
    {
        ShowPlaybackControls();
        SettingsPopup.IsOpen = DiagnosticsPopup.IsOpen = PlaybackInfoPopup.IsOpen = false;
        if (!fullscreen)
        {
            SaveDesktopPreferences();
            savedState = WindowState;
            fullscreen = true;
            WindowChrome.SetWindowChrome(this, null);
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Normal; WindowState = WindowState.Maximized;
            TitleBar.Visibility = HeaderPanel.Visibility = FooterPanel.Visibility = Visibility.Collapsed;
            OuterFrame.Margin = new Thickness(0); OuterFrame.BorderThickness = new Thickness(0);
            Grid.SetRow(PlayerSurface, 0); Grid.SetRowSpan(PlayerSurface, 5);
            PlayerSurface.Margin = new Thickness(0); Panel.SetZIndex(PlayerSurface, 10);
            // Native video is an HWND. Reuse its supported Content overlay for the playback bar.
            DetachPlaybackControls(); OverlayLayer.Children.Add(ControlsPanel);
            Grid.SetRow(ControlsPanel, 1); ControlsPanel.VerticalAlignment = VerticalAlignment.Bottom;
            ControlsPanel.HorizontalAlignment = HorizontalAlignment.Stretch; ControlsPanel.MaxWidth = 860;
            ControlsPanel.Margin = new Thickness(32, 0, 32, 26); ControlsPanel.CornerRadius = new CornerRadius(13);
            ControlsPanel.Background = new SolidColorBrush(Color.FromArgb(235, 14, 17, 20));
            ControlsPanel.Height = 98;
            ExitFullscreenButton.Visibility = Visibility.Visible;
        }
        else
        {
            fullscreen = false;
            DetachPlaybackControls(); LayoutRoot.Children.Add(ControlsPanel);
            Grid.SetRow(ControlsPanel, 3); ControlsPanel.VerticalAlignment = VerticalAlignment.Stretch;
            ControlsPanel.MaxWidth = double.PositiveInfinity; ControlsPanel.Height = double.NaN;
            ControlsPanel.Margin = new Thickness(0); ControlsPanel.CornerRadius = new CornerRadius(0);
            ControlsPanel.Background = new SolidColorBrush(Color.FromRgb(14, 17, 20));
            TitleBar.Visibility = HeaderPanel.Visibility = FooterPanel.Visibility = Visibility.Visible;
            Grid.SetRow(PlayerSurface, 2); Grid.SetRowSpan(PlayerSurface, 1);
            PlayerSurface.Margin = new Thickness(13, 0, 13, 0); Panel.SetZIndex(PlayerSurface, 0);
            ExitFullscreenButton.Visibility = Visibility.Collapsed;
            WindowState = WindowState.Normal;
            WindowChrome.SetWindowChrome(this, cinemaChrome); ResizeMode = ResizeMode.CanResize;
            OuterFrame.BorderThickness = new Thickness(1); WindowState = savedState;
            UpdateFrameInset();
        }
        ApplyMusicPlayerLayout();
    }
    void UpdateFrameInset()
    {
        if (MaximizeGlyph is not null)
        {
            bool maximized = WindowState == WindowState.Maximized;
            MaximizeGlyph.Visibility = maximized ? Visibility.Collapsed : Visibility.Visible;
            RestoreGlyph.Visibility = maximized ? Visibility.Visible : Visibility.Collapsed;
            MaximizeButton.ToolTip = maximized ? "还原" : "最大化";
            MaximizeButton.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, maximized ? "还原窗口" : "最大化窗口");
        }
        if (OuterFrame is null) return;
        OuterFrame.Margin = !fullscreen && WindowState == WindowState.Maximized
            ? SystemParameters.WindowResizeBorderThickness : new Thickness(0);
    }
    void Window_StateChanged(object? sender, EventArgs e) => UpdateFrameInset();
    void Minimize_Click(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);
    void Maximize_Click(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(this);
        else SystemCommands.MaximizeWindow(this);
    }
    void CloseWindow_Click(object sender, RoutedEventArgs e) => Close();
    void QuickReceive_Click(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        if (receivingRequested) Stop_Click(sender, e); else Start_Click(sender, e);
    }
    void Settings_Click(object sender, RoutedEventArgs e)
    {
        DiagnosticsPopup.IsOpen = PlaybackInfoPopup.IsOpen = false;
        SettingsScroll.MaxHeight = Math.Max(180, Math.Min(470, ActualHeight - 180));
        if (!SettingsPopup.IsOpen) popupReturnFocus = Keyboard.FocusedElement;
        if (SettingsPopup.IsOpen) CloseInterfacePopup(SettingsPopup, SettingsPanel);
        else SettingsPopup.IsOpen = true;
    }
    void CloseSettings_Click(object sender, RoutedEventArgs e) => CloseInterfacePopup(SettingsPopup, SettingsPanel);
    void OpenDiagnostics()
    {
        popupReturnFocus = Keyboard.FocusedElement;
        SettingsPopup.IsOpen = PlaybackInfoPopup.IsOpen = false;
        DiagnosticsPanel.Width = Math.Max(350, Math.Min(660, ActualWidth - 36));
        LogBox.Height = Math.Max(100, Math.Min(275, ActualHeight - 230));
        DiagnosticsPopup.IsOpen = true;
    }
    void CloseDiagnostics_Click(object sender, RoutedEventArgs e) => CloseInterfacePopup(DiagnosticsPopup, DiagnosticsPanel);
    void IdlePrimary_Click(object sender, RoutedEventArgs e)
    {
        if ((playback.Snapshot.Status != "OK" || playback.Snapshot.State == "PAUSED_PLAYBACK") && playback.Snapshot.Uri.Length > 0) PlayPause_Click(sender, e);
        else if (!receivingRequested) QuickReceive_Click(sender, e);
        else Settings_Click(sender, e);
    }
    void IdleDetails_Click(object sender, RoutedEventArgs e)
    {
        if (playback.Snapshot.Status != "OK") OpenDiagnostics(); else LocalFile_Click(sender, e);
    }
    void Overlay_KeyDown(object sender, KeyEventArgs e) => HandlePreviewKey(sender, e);
    void HandlePreviewKey(object sender, KeyEventArgs e)
    {
        if (e.Handled || closing) return;
        if (musicTransitionActive)
        {
            CompleteMusicTransition();
            if (e.Key is Key.Escape or Key.Space or Key.Enter) { e.Handled = true; return; }
        }
        if (startupActive)
        {
            DismissStartupPresentation();
            if (e.Key is Key.Escape or Key.Space or Key.Enter) { e.Handled = true; return; }
        }
        ShowPlaybackControls();
        if (e.Key == Key.Escape)
        {
            if (NetworkBox.IsDropDownOpen)
            { NetworkBox.IsDropDownOpen = false; NetworkBox.Focus(); e.Handled = true; return; }
            if (SettingsPopup.IsOpen || DiagnosticsPopup.IsOpen || PlaybackInfoPopup.IsOpen)
            { SettingsPopup.IsOpen = DiagnosticsPopup.IsOpen = PlaybackInfoPopup.IsOpen = false; e.Handled = true; }
            else if (fullscreen) { ToggleFullscreen(); e.Handled = true; }
            return;
        }
        if (e.Key == Key.F11) { ToggleFullscreen(); e.Handled = true; return; }
        if (e.OriginalSource is DependencyObject target)
        {
            if (IsEditingControl(target, false)) return;
            if (e.Key == Key.Space && IsEditingControl(target, true)) return;
        }
        if (Keyboard.Modifiers == ModifierKeys.None && !SettingsPopup.IsOpen && !DiagnosticsPopup.IsOpen && !PlaybackInfoPopup.IsOpen
            && e.OriginalSource is DependencyObject source && !IsEditingControl(source, true)
            && HandlePlaybackShortcut(e.Key)) { e.Handled = true; return; }
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.I)
        { PlaybackInfo_Click(this, new RoutedEventArgs()); e.Handled = true; }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.O)
        { LocalFile_Click(this, new RoutedEventArgs()); e.Handled = true; }
        else if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.F)
        { ToggleFullscreen(); e.Handled = true; }
        else if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.Space)
        { PlayPause_Click(this, new RoutedEventArgs()); e.Handled = true; }
    }
    static bool IsEditingControl(DependencyObject target, bool includeButtons)
    {
        while (target is not null)
        {
            if (target is TextBoxBase or ComboBox or Slider || includeButtons && target is ButtonBase) return true;
            target = target is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(target) : LogicalTreeHelper.GetParent(target);
        }
        return false;
    }
    void Seek_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Left or Key.Right or Key.Home or Key.End or Key.PageUp or Key.PageDown)) return;
        try { if (playback.Snapshot.Uri.Length > 0) playback.Seek((long)PositionSlider.Value); }
        catch (UpnpException) { Log("当前媒体暂不支持拖动进度。"); }
    }
    void Seek_CaptureLost(object sender, MouseEventArgs e) => dragging = false;
    void LocalFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "选择本地视频验证播放窗口", Filter = "视频文件|*.mp4;*.mkv;*.mov;*.avi;*.ts;*.webm|所有文件|*.*" };
        if (dialog.ShowDialog(this) != true) return;
        try { playback.LoadLocal(dialog.FileName); playback.Play(); }
        catch (Exception ex)
        { Log("本地视频测试未成功 · " + ex.GetType().Name + "，请打开诊断记录或尝试兼容渲染。"); }
    }
    async void Probe_Click(object sender, RoutedEventArgs e)
    {
        ProbeButton.IsEnabled = false; OpenDiagnostics();
        try { await playback.ProbeAsync(lifetime.Token); }
        finally { if (!closed) ProbeButton.IsEnabled = true; }
    }
    void Compatibility_Click(object sender, RoutedEventArgs e)
    {
        playback.Stop();
        Video.MediaPlayer = null;
        bool enabled = CompatibilityBox.IsChecked == true;
        try
        {
            playback.ConfigureCompatibility(enabled);
            preferences = preferences with { CompatibilityRendering = enabled };
            SaveDesktopPreferences();
        }
        finally { Video.ApplyTemplate(); Video.MediaPlayer = playback.Player; }
    }
    void ToggleLogs_Click(object sender, RoutedEventArgs e)
    {
        if (DiagnosticsPopup.IsOpen) DiagnosticsPopup.IsOpen = false; else OpenDiagnostics();
    }
    void Fullscreen_Click(object sender, RoutedEventArgs e) => ToggleFullscreen();
    void Logs_Click(object sender, RoutedEventArgs e) => Process.Start(new ProcessStartInfo(Path.Combine(DataPath, "logs")) { UseShellExecute = true });
    void NetworkSettings_Click(object sender, RoutedEventArgs e) => Process.Start(new ProcessStartInfo("ms-settings:network-status") { UseShellExecute = true });
    string DiagnosticSummary()
    {
        return $"LANPUT 0.9.4\nWindows：{Environment.OSVersion.VersionString}\n接收状态：{ServiceStatus.Text}\n播放状态：{playback.Snapshot.State}/{playback.Snapshot.Status}\n视频输出：{(playback.HasVideoOutput ? "已建立" : "未建立")}\n应用事件：{AppDiagnostics.LastEvent}\n" + string.Join(Environment.NewLine, logLines);
    }
    async void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (copying || closing) return;
        copying = true; CopyDiagnosticsButton.IsEnabled = false;
        string? savedName = null;
        try
        {
            var summary = DiagnosticSummary();
            try
            {
                savedName = "diagnostic-summary-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".txt";
                File.WriteAllText(Path.Combine(DataPath, "logs", savedName), summary, System.Text.Encoding.UTF8);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                savedName = null; Log("诊断文件保存失败 · " + ex.GetType().Name + "，仍尝试复制。");
            }
            var result = await ClipboardCopy.TryAsync(() => Clipboard.SetText(summary), lifetime.Token);
            if (closing || closed) return;
            if (result.Copied)
            {
                ShowMotionToast("诊断已复制", "Info");
                FooterHint.Text = savedName is null ? "诊断已复制" : "诊断已复制，并已保存到日志目录";
                Log($"诊断复制成功 · 尝试 {result.Attempts} 次" + (savedName is null ? "" : " · 已保存 " + savedName));
            }
            else
            {
                var code = result.ErrorCode.GetValueOrDefault();
                ShowMotionToast("剪贴板暂不可用，可导出诊断文件", "Error");
                AppDiagnostics.Record($"剪贴板复制失败（已处理，不退出）· HRESULT 0x{code:X8}");
                FooterHint.Text = savedName is null ? "剪贴板不可用，请点击“导出诊断”保存文件" : "剪贴板不可用；诊断已保存，请在日志目录查看";
                Log($"剪贴板暂不可用 · HRESULT 0x{code:X8} · 已重试 {result.Attempts} 次，不影响程序继续运行。");
            }
            if (savedName is not null) Log("诊断文件 · " + savedName);
        }
        catch (OperationCanceledException) { /* Window closing cancels retries. */ }
        finally
        {
            copying = false;
            if (!closing && !closed) CopyDiagnosticsButton.IsEnabled = true;
        }
    }
    void ExportDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "保存诊断（不使用剪贴板）", Filter = "文本文件|*.txt", DefaultExt = ".txt", AddExtension = true,
            FileName = "LANPUT-diagnostic-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt"
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            File.WriteAllText(dialog.FileName, DiagnosticSummary(), System.Text.Encoding.UTF8);
            ShowMotionToast("诊断文件已导出", "Info");
            FooterHint.Text = "诊断文件已导出"; Log("诊断文件已按所选位置导出（不记录完整路径）。");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowMotionToast("导出失败，请选择可写目录", "Error");
            FooterHint.Text = "导出失败，请选择可写目录";
            Log("诊断导出失败 · " + ex.GetType().Name);
        }
    }
    async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (closed) return;
        DismissStartupPresentation();
        if (musicTransitionActive) CompleteMusicTransition();
        e.Cancel = true;
        if (!exitRequested && preferences.CloseToTray && tray is not null && !closing)
        { SettingsPopup.IsOpen = DiagnosticsPopup.IsOpen = PlaybackInfoPopup.IsOpen = false; SaveDesktopPreferences(); Hide(); return; }
        if (closing) return;
        if (busy) { Log("操作进行中，请稍后关闭。"); return; }
        SaveDesktopPreferences();
        receivingRequested = false;
        DisposeInterfaceMotion();
        DisposeDesktopFeatures();
        closing = true; SettingsPopup.IsOpen = DiagnosticsPopup.IsOpen = PlaybackInfoPopup.IsOpen = false; IsEnabled = false; timer.Stop(); lifetime.Cancel();
        try { await StopReceiver(); }
        finally
        {
            CancelLyrics(); lyricsHttp.Dispose(); playback.Stop(); Video.MediaPlayer = null; Video.Dispose(); playback.Dispose();
            closed = true; lifetime.Dispose();
            // StopReceiver may complete synchronously. Let the cancelled Closing event
            // return before closing again, otherwise WPF throws VerifyNotClosing.
            _ = Dispatcher.BeginInvoke(new Action(Close));
        }
    }
}
