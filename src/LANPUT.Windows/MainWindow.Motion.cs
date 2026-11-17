using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using LANPUT.Core;

namespace LANPUT.Windows;

public partial class MainWindow
{
    readonly Dictionary<(DependencyObject Target, DependencyProperty Property), long> motionClocks = new();
    readonly HashSet<Button> motionButtons = new();
    readonly DispatcherTimer toastTimer = new() { Interval = TimeSpan.FromSeconds(2.4) };
    long motionSequence;
    string interfaceState = "", receiverColorState = "", pulseState = "";
    bool receiverPresentationFailed;
    bool? idleIsVisible;
    bool pauseIsVisible, controlsAreVisible = true;
    bool MotionEnabled => preferences.InterfaceAnimations && SystemParameters.ClientAreaAnimation
        && IsVisible && WindowState != WindowState.Minimized && !closing;

    void InitializeInterfaceMotion()
    {
        InterfaceAnimationsBox.IsChecked = preferences.InterfaceAnimations;
        SystemParameters.StaticPropertyChanged += MotionSystemParametersChanged;
        Loaded += (_, _) =>
        {
            AttachButtonMotion(this);
            UpdateInterfacePresentation(playback.Snapshot);
        };
        IsVisibleChanged += (_, _) => ResetInterfaceMotion();
        StateChanged += (_, _) => ResetInterfaceMotion();
        SettingsPopup.Opened += (_, _) => OpenInterfacePanel(SettingsPanel);
        PlaybackInfoPopup.Opened += (_, _) => OpenInterfacePanel(PlaybackInfoPanel);
        PlaybackInfoPopup.Closed += (_, _) => ResetPanelMotion(PlaybackInfoPanel);
        DiagnosticsPopup.Opened += (_, _) => OpenInterfacePanel(DiagnosticsPanel);
        SettingsPopup.Closed += (_, _) => ResetPanelMotion(SettingsPanel);
        DiagnosticsPopup.Closed += (_, _) => ResetPanelMotion(DiagnosticsPanel);
        toastTimer.Tick += (_, _) =>
        {
            toastTimer.Stop();
            LobbyNotice.Visibility = Visibility.Collapsed;
            AnimateValue(MotionToast, OpacityProperty, 0, 150,
                completed: () => MotionToast.Visibility = Visibility.Collapsed);
        };
    }

    void MotionSystemParametersChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SystemParameters.ClientAreaAnimation))
            Dispatcher.InvokeAsync(() => { if (!closing && !closed) ResetInterfaceMotion(); });
    }

    void InterfaceAnimations_Click(object sender, RoutedEventArgs e)
    {
        preferences = preferences with { InterfaceAnimations = InterfaceAnimationsBox.IsChecked == true };
        SaveDesktopPreferences();
        ResetInterfaceMotion();
    }

    void ResetInterfaceMotion()
    {
        if (musicTransitionActive) CompleteMusicTransition();
        if (startupActive) DismissStartupPresentation();
        foreach (var key in motionClocks.Keys.ToArray()) StopMotion(key.Target, key.Property);
        ResetPanelMotion(SettingsPanel);
        ResetPanelMotion(DiagnosticsPanel);
        ResetPanelMotion(PlaybackInfoPanel);
        idleIsVisible = null; pauseIsVisible = false; interfaceState = receiverColorState = pulseState = "";
        foreach (var button in motionButtons)
        {
            if (button.RenderTransform is not TransformGroup group) continue;
            ((ScaleTransform)group.Children[0]).ScaleX = ((ScaleTransform)group.Children[0]).ScaleY = 1;
            ((TranslateTransform)group.Children[1]).Y = 0;
        }
        ControlsPanel.Visibility = controlsAreVisible ? Visibility.Visible : Visibility.Collapsed;
        ControlsPanel.Opacity = controlsAreVisible ? 1 : 0;
        ExitFullscreenButton.Visibility = fullscreen && (cinemaVideoRevealed || musicMode) && controlsAreVisible ? Visibility.Visible : Visibility.Collapsed;
        ExitFullscreenButton.Opacity = controlsAreVisible ? 1 : 0;
        ResetCinemaPresentation();
        UpdateInterfacePresentation(playback.Snapshot);
        // A minimized or hidden window never retains decorative animation clocks.
        if (!MotionEnabled)
        {
            MotionToast.Visibility = LobbyNotice.Visibility = Visibility.Collapsed;
            toastTimer.Stop();
        }
    }

    void UpdateInterfacePresentation(PlaybackSnapshot snapshot)
    {
        if (startupActive && (!MotionEnabled || snapshot.Uri.Length > 0 || receiverPresentationFailed))
            DismissStartupPresentation();
        var activity = playback.Activity;
        bool connectingMedia = activity.Phase is PlaybackPhase.Opening or PlaybackPhase.Buffering or PlaybackPhase.WaitingForVideo;
        bool showIdle = !(playback.HasVideoOutput && ((snapshot.State is "PLAYING" or "PAUSED_PLAYBACK") || activity.Phase == PlaybackPhase.Buffering));
        string state = snapshot.Status != "OK" ? "Error"
            : connectingMedia ? "Connecting"
            : snapshot.State == "PLAYING" ? "Playing"
            : snapshot.State == "PAUSED_PLAYBACK" ? "Paused"
            : receiverPresentationFailed ? "Error"
            : busy || (receivingRequested && receiver is null) ? "Connecting"
            : receivingRequested ? "Receiving" : "Idle";

        string serviceState = receiverPresentationFailed ? "Error"
            : busy || (receivingRequested && receiver is null) ? "Connecting"
            : receivingRequested ? "Receiving" : "Idle";
        if (receiverColorState != serviceState)
        {
            receiverColorState = serviceState;
            var serviceBrush = (SolidColorBrush)FindResource("State" + serviceState);
            ServiceDot.Fill = serviceBrush;
            ServiceHalo.Stroke = serviceBrush;
            TransitionTextColor(ServiceStatus, (SolidColorBrush)FindResource(serviceState == "Error" ? "StateError" : "TextSecondary"));
        }
        string mediaColor = snapshot.Status != "OK" ? "Error"
            : connectingMedia ? "Connecting"
            : snapshot.State == "PLAYING" ? "Playing" : snapshot.State == "PAUSED_PLAYBACK" ? "Paused" : "Idle";
        StateAlert.Visibility = snapshot.Status != "OK" ? Visibility.Visible : Visibility.Collapsed;
        if (interfaceState != state)
        {
            string previous = interfaceState;
            interfaceState = state;
            TransitionTextColor(PlaybackState, (SolidColorBrush)FindResource("State" + mediaColor));
            if (previous.Length > 0 && state == "Error") ShowMotionToast(snapshot.Status != "OK" ? "影片暂时无法播放 · 可重试或查看诊断" : "接收服务异常 · 请检查网络与端口", "Error");
        }
        else PlaybackState.Foreground = (Brush)FindResource("State" + mediaColor);

        UpdateMusicPresentation(snapshot);
        if (musicTransitionActive && !MotionEnabled) CompleteMusicTransition();
        if (!musicMode && !musicTransitionActive) UpdateCinemaTransition(snapshot);
        idleIsVisible = showIdle;
        IdleOverlay.IsHitTestVisible = false;
        IdleOverlay.Opacity = 1;
        IdleOverlay.Visibility = Visibility.Visible;

        bool paused = cinemaVideoRevealed && !showIdle && snapshot.State == "PAUSED_PLAYBACK";
        if (paused != pauseIsVisible)
        {
            pauseIsVisible = paused;
            if (paused)
            {
                PauseOverlay.Visibility = Visibility.Visible;
                AnimateValue(PauseOverlay, OpacityProperty, 1, 150, from: 0);
            }
            else AnimateValue(PauseOverlay, OpacityProperty, 0, 150,
                completed: () => { if (!pauseIsVisible) PauseOverlay.Visibility = Visibility.Collapsed; });
        }
        else if (!paused) PauseOverlay.Visibility = Visibility.Collapsed;

        string nextPulse = MotionEnabled ? serviceState + "|" + state + "|" + IdleArtwork.Visibility : "";
        if (pulseState != nextPulse)
        {
            pulseState = nextPulse;
            StopPulse(ServiceHalo);
            StopPulse(OpeningRingOne);
            StopPulse(OpeningRingTwo);
            if (MotionEnabled && serviceState == "Connecting")
                StartPulse(ServiceHalo, 2200, 0, .6, 2.1, .7);
            if (MotionEnabled && showIdle && IdleArtwork.Visibility == Visibility.Visible && state == "Connecting")
            {
                StartPulse(OpeningRingOne, 1900, 0, .82, 1.5, .5);
                StartPulse(OpeningRingTwo, 1900, 650, .82, 1.5, .5);
            }
        }
    }

    void TransitionTextColor(TextBlock text, SolidColorBrush target)
    {
        if (!MotionEnabled || text.Foreground is not SolidColorBrush current)
        { text.Foreground = target; return; }
        var brush = new SolidColorBrush(target.Color);
        text.Foreground = brush;
        long token = ++motionSequence;
        motionClocks[(brush, SolidColorBrush.ColorProperty)] = token;
        var animation = new ColorAnimation(current.Color, target.Color, TimeSpan.FromMilliseconds(300)) { FillBehavior = FillBehavior.Stop };
        animation.Completed += (_, _) =>
        {
            if (motionClocks.TryGetValue((brush, SolidColorBrush.ColorProperty), out long value) && value == token)
                StopMotion(brush, SolidColorBrush.ColorProperty);
        };
        brush.BeginAnimation(SolidColorBrush.ColorProperty, animation);
    }

    void AttachButtonMotion(DependencyObject parent)
    {
        if (parent is Button button && !TitleBar.IsAncestorOf(button) && motionButtons.Add(button))
        {
            var scale = new ScaleTransform(1, 1);
            var translate = new TranslateTransform();
            button.RenderTransformOrigin = new Point(.5, .5);
            button.RenderTransform = new TransformGroup { Children = { scale, translate } };
            button.MouseLeave += (_, _) =>
            {
                AnimateValue(scale, ScaleTransform.ScaleXProperty, 1, 150);
                AnimateValue(scale, ScaleTransform.ScaleYProperty, 1, 150);
            };
            button.PreviewMouseLeftButtonDown += (_, _) =>
            {
                AnimateValue(scale, ScaleTransform.ScaleXProperty, MotionEnabled ? .975 : 1, 80);
                AnimateValue(scale, ScaleTransform.ScaleYProperty, MotionEnabled ? .975 : 1, 80);
            };
            button.PreviewMouseLeftButtonUp += (_, _) =>
            {
                AnimateValue(scale, ScaleTransform.ScaleXProperty, 1, 150);
                AnimateValue(scale, ScaleTransform.ScaleYProperty, 1, 150);
            };
            button.LostMouseCapture += (_, _) =>
            {
                AnimateValue(scale, ScaleTransform.ScaleXProperty, 1, 150);
                AnimateValue(scale, ScaleTransform.ScaleYProperty, 1, 150);
            };
        }
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            AttachButtonMotion(VisualTreeHelper.GetChild(parent, i));
    }

    void OpenInterfacePanel(FrameworkElement panel)
    {
        panel.IsHitTestVisible = true;
        AttachButtonMotion(panel);
        var translate = new TranslateTransform();
        panel.RenderTransform = translate;
        AnimateValue(panel, OpacityProperty, 1, 320, from: 0);
        AnimateValue(translate, TranslateTransform.YProperty, 0, 320, from: 14);
    }

    void ResetPanelMotion(FrameworkElement panel)
    {
        StopMotion(panel, OpacityProperty);
        if (panel.RenderTransform is TranslateTransform translate) StopMotion(translate, TranslateTransform.YProperty);
        panel.RenderTransform = Transform.Identity;
        panel.Opacity = 1;
        panel.IsHitTestVisible = true;
    }

    void CloseInterfacePopup(Popup popup, FrameworkElement panel)
    {
        if (!popup.IsOpen) return;
        panel.IsHitTestVisible = false;
        AnimateValue(panel, OpacityProperty, 0, 180, completed: () => popup.IsOpen = false);
    }

    void ShowMotionToast(string message, string state)
    {
        if (closing || !IsVisible || WindowState == WindowState.Minimized) return;
        toastTimer.Stop();
        if (!cinemaVideoRevealed && !musicMode)
        {
            LobbyNotice.Text = message;
            LobbyNotice.Visibility = Visibility.Visible;
            MotionToast.Visibility = Visibility.Collapsed;
            toastTimer.Start();
            return;
        }
        MotionToastText.Text = message;
        MotionToast.HorizontalAlignment = musicMode ? HorizontalAlignment.Right : HorizontalAlignment.Center;
        MotionToastText.Foreground = Brushes.White;
        (string background, string border) = state switch
        {
            "Receiving" => ("#261F12", "#8A6A2F"), "Connecting" or "Info" => ("#161C26", "#3A5A80"),
            "Playing" => ("#131D16", "#3F6B4F"), "Paused" => ("#171A26", "#565E86"),
            "Error" => ("#261411", "#8A4636"), _ => ("#241F15", "#8A744A")
        };
        MotionToast.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(background));
        MotionToast.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(border));
        MotionToast.Visibility = Visibility.Visible;
        var translate = new TranslateTransform(); MotionToast.RenderTransform = translate;
        AnimateValue(MotionToast, OpacityProperty, 1, 280, from: 0);
        AnimateValue(translate, TranslateTransform.YProperty, 0, 280, from: 10);
        toastTimer.Start();
    }

    void SetPlaybackControlsVisible(bool visible)
    {
        visible = visible && (!fullscreen || cinemaVideoRevealed || musicMode);
        if (controlsAreVisible == visible) return;
        controlsAreVisible = visible;
        if (visible)
        {
            ControlsPanel.Visibility = Visibility.Visible;
            if (fullscreen) ExitFullscreenButton.Visibility = Visibility.Visible;
            AnimateValue(ControlsPanel, OpacityProperty, 1, 180);
            AnimateValue(ExitFullscreenButton, OpacityProperty, 1, 180);
        }
        else
        {
            AnimateValue(ControlsPanel, OpacityProperty, 0, 180, completed: () =>
            { if (!controlsAreVisible) ControlsPanel.Visibility = Visibility.Collapsed; });
            AnimateValue(ExitFullscreenButton, OpacityProperty, 0, 180, completed: () =>
            { if (!controlsAreVisible) ExitFullscreenButton.Visibility = Visibility.Collapsed; });
        }
    }

    void AnimateValue(DependencyObject target, DependencyProperty property, double to, int milliseconds,
        double? from = null, int delay = 0, Action? completed = null)
    {
        double start = from ?? (double)target.GetValue(property);
        StopMotion(target, property);
        target.SetValue(property, to);
        if (!MotionEnabled) { completed?.Invoke(); return; }
        long token = ++motionSequence;
        motionClocks[(target, property)] = token;
        // A zero-time keyframe holds the initial value during stagger delays.
        var animation = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
        animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(start, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        if (delay > 0) animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(start, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(delay))));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(to, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(delay + milliseconds)),
            new CubicEase { EasingMode = EasingMode.EaseOut }));
        animation.Completed += (_, _) =>
        {
            if (!motionClocks.TryGetValue((target, property), out long current) || current != token) return;
            StopMotion(target, property);
            completed?.Invoke();
        };
        BeginMotion(target, property, animation);
    }

    void StartPulse(FrameworkElement element, int milliseconds, int delay, double from, double to, double opacity)
    {
        var scale = (ScaleTransform)element.RenderTransform;
        foreach (var property in new[] { ScaleTransform.ScaleXProperty, ScaleTransform.ScaleYProperty })
        {
            motionClocks[(scale, property)] = ++motionSequence;
            scale.BeginAnimation(property, new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(milliseconds))
            { BeginTime = TimeSpan.FromMilliseconds(delay), RepeatBehavior = RepeatBehavior.Forever });
        }
        motionClocks[(element, OpacityProperty)] = ++motionSequence;
        element.BeginAnimation(OpacityProperty, new DoubleAnimation(opacity, 0, TimeSpan.FromMilliseconds(milliseconds))
        { BeginTime = TimeSpan.FromMilliseconds(delay), RepeatBehavior = RepeatBehavior.Forever });
    }

    void StopPulse(FrameworkElement element)
    {
        StopMotion(element, OpacityProperty); element.Opacity = 0;
        var scale = (ScaleTransform)element.RenderTransform;
        StopMotion(scale, ScaleTransform.ScaleXProperty); StopMotion(scale, ScaleTransform.ScaleYProperty);
        scale.ScaleX = scale.ScaleY = 1;
    }

    static void BeginMotion(DependencyObject target, DependencyProperty property, AnimationTimeline? animation)
    {
        if (target is UIElement element) element.BeginAnimation(property, animation, HandoffBehavior.SnapshotAndReplace);
        else if (target is Animatable value) value.BeginAnimation(property, animation, HandoffBehavior.SnapshotAndReplace);
    }

    void StopMotion(DependencyObject target, DependencyProperty property)
    {
        motionClocks.Remove((target, property));
        BeginMotion(target, property, null);
    }

    void DisposeInterfaceMotion()
    {
        SystemParameters.StaticPropertyChanged -= MotionSystemParametersChanged;
        toastTimer.Stop();
        StopRecordRotation();
        foreach (var key in motionClocks.Keys.ToArray()) StopMotion(key.Target, key.Property);
    }
}
