using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace LANPUT.Windows;

public partial class MainWindow
{
    const int StartupLogoFadeInMs = 420;
    const int StartupLogoHoldUntilMs = 1300;
    const int StartupLogoFadeOutMs = 1650;
    const int StartupCurtainStartMs = 1700;
    const int StartupDurationMs = 3200;
    bool startupAttempted, startupActive;

    void InitializeStartupPresentation()
    {
        // Cover the first painted frame inside VideoView's separate overlay HWND.
        StartupOverlay.Visibility = preferences.InterfaceAnimations && SystemParameters.ClientAreaAnimation
            ? Visibility.Visible : Visibility.Collapsed;
        Loaded += (_, _) => Dispatcher.BeginInvoke(StartStartupPresentation, DispatcherPriority.Loaded);
        StartupOverlay.SizeChanged += (_, _) => { if (startupActive) DismissStartupPresentation(); };
        PreviewMouseDown += (_, _) => { if (startupActive) DismissStartupPresentation(); };
        OverlayLayer.PreviewGotKeyboardFocus += (_, e) =>
        {
            if (startupActive && e.NewFocus is DependencyObject target && !StartupOverlay.IsAncestorOf(target))
                DismissStartupPresentation();
        };
    }

    void StartStartupPresentation()
    {
        if (startupAttempted) return;
        startupAttempted = true;
        if (!MotionEnabled || playback.Snapshot.Uri.Length > 0 || receiverPresentationFailed
            || SettingsPopup.IsOpen || DiagnosticsPopup.IsOpen || PlaybackInfoPopup.IsOpen
            || StartupOverlay.ActualWidth <= 0)
        { DismissStartupPresentation(); return; }

        startupActive = true;
        StartupOverlay.Visibility = Visibility.Visible;
        if (IsActive) StartupSkipButton.Focus();
        double travel = StartupOverlay.ActualWidth / 2 + 2;
        var brand = new DoubleAnimationUsingKeyFrames();
        brand.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        brand.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(StartupLogoFadeInMs))));
        brand.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(StartupLogoHoldUntilMs))));
        brand.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(StartupLogoFadeOutMs))));
        StartupBrand.BeginAnimation(OpacityProperty, brand);
        foreach (var (target, end) in new[] { (StartupLeftShift, -travel), (StartupRightShift, travel) })
        {
            var curtain = new DoubleAnimationUsingKeyFrames();
            curtain.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            curtain.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(StartupCurtainStartMs))));
            curtain.KeyFrames.Add(new EasingDoubleKeyFrame(end, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(StartupDurationMs)),
                new CubicEase { EasingMode = EasingMode.EaseInOut }));
            if (target == StartupRightShift) curtain.Completed += (_, _) => { if (startupActive) DismissStartupPresentation(); };
            target.BeginAnimation(TranslateTransform.XProperty, curtain);
        }
        // Keep initial base values intact until the first animation tick, avoiding
        // a one-frame glimpse of the lobby. Only cached render transforms move.
    }

    void DismissStartupPresentation()
    {
        startupAttempted = true;
        startupActive = false;
        bool restoreFocus = StartupOverlay.IsKeyboardFocusWithin;
        StopMotion(StartupBrand, OpacityProperty);
        StopMotion(StartupLeftShift, TranslateTransform.XProperty);
        StopMotion(StartupRightShift, TranslateTransform.XProperty);
        StartupOverlay.Visibility = Visibility.Collapsed;
        StartupBrand.Opacity = 0;
        StartupLeftShift.X = StartupRightShift.X = 0;
        if (restoreFocus && !closing && IsVisible) OverlayLayer.Focus();
    }

    void StartupSkip_Click(object sender, RoutedEventArgs e)
    { DismissStartupPresentation(); e.Handled = true; }
}
