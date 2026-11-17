using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using LANPUT.Core;

namespace LANPUT.Windows;

public partial class MainWindow
{
    const int MusicTransitionDurationMs = 3600;
    bool musicSceneVisible, musicTransitionActive, musicTransitionTarget;
    long musicTransitionEpoch;
    double musicTransitionCopyCenter;
    public static readonly DependencyProperty MusicCurtainProgressProperty = DependencyProperty.Register(
        nameof(MusicCurtainProgress), typeof(double), typeof(MainWindow),
        new PropertyMetadata(0d, (d, _) => ((MainWindow)d).ApplyMusicCurtainFrame()));
    public double MusicCurtainProgress
    { get => (double)GetValue(MusicCurtainProgressProperty); set => SetValue(MusicCurtainProgressProperty, value); }

    void InitializeMusicTransition()
    {
        MusicCurtainOverlay.SizeChanged += (_, e) =>
        {
            // Reparenting controls changes height once, underneath fully closed curtains.
            // A user width change is an interruption, rather than a moving seam.
            if (musicTransitionActive && Math.Abs(e.NewSize.Width - e.PreviousSize.Width) > .5)
                CompleteMusicTransition();
            else if (musicTransitionActive) ApplyMusicCurtainFrame();
        };
        OverlayLayer.PreviewGotKeyboardFocus += (_, e) =>
        {
            if (musicTransitionActive && e.NewFocus is DependencyObject target
                && target != MusicCurtainOverlay && !MusicCurtainOverlay.IsAncestorOf(target))
                CompleteMusicTransition();
        };
        PreviewMouseDown += (_, _) => { if (musicTransitionActive) CompleteMusicTransition(); };
    }

    void RequestMusicScene(bool music)
    {
        if (musicTransitionActive || !MotionEnabled || closing || !IsLoaded)
        { CompleteMusicTransition(); return; }
        if (musicSceneVisible == music) return;
        if (startupActive) DismissStartupPresentation();
        // An audio source has already replaced any native movie output. Keep the
        // cinema artwork behind closing curtains instead of exposing an empty HWND.
        if (music) SnapCinemaPresentation(false);
        // Measure while hidden, so the very first painted frame already has
        // curtains positioned fully outside the viewport.
        MusicCurtainOverlay.Visibility = Visibility.Hidden;
        MusicCurtainOverlay.UpdateLayout();
        musicTransitionCopyCenter = MusicCurtainOverlay.ActualHeight / 2;
        musicTransitionTarget = music;
        musicTransitionActive = true;
        long epoch = ++musicTransitionEpoch;
        MusicCurtainTitle.Text = music ? "这一场，听音乐" : "下一场，回到故事里";
        MusicCurtainSubtitle.Text = music ? "私人音乐厅" : "私人放映室";
        MusicTools.IsExpanded = false;
        MusicCurtainProgress = 0;
        MusicCurtainOverlay.Visibility = Visibility.Visible;
        if (IsActive) MusicCurtainOverlay.Focus();
        ApplyMusicCurtainFrame();
        var animation = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(MusicTransitionDurationMs));
        animation.Completed += (_, _) =>
        { if (musicTransitionActive && epoch == musicTransitionEpoch) CompleteMusicTransition(); };
        BeginAnimation(MusicCurtainProgressProperty, animation);
    }

    static double CurtainEase(double start, double end, double p)
    {
        double t = Math.Clamp((p - start) / (end - start), 0, 1);
        return t * t * (3 - 2 * t);
    }

    void ApplyMusicCurtainFrame()
    {
        if (MusicCurtainOverlay is null || !musicTransitionActive) return;
        double p = Math.Clamp(MusicCurtainProgress, 0, 1);
        double open = 1 - CurtainEase(0, .27, p) + CurtainEase(.6, 1, p);
        double travel = MusicCurtainOverlay.ActualWidth / 2 + 2;
        MusicCurtainLeftShift.X = -travel * open;
        MusicCurtainRightShift.X = travel * open;
        MusicCurtainCopy.Opacity = CurtainEase(.28, .38, p) * (1 - CurtainEase(.54, .63, p));
        // Keep the caption in the same physical place when the covered control
        // bar moves into or out of this viewport (a one-time 102 DIP height change).
        MusicCurtainCopyShift.Y = musicTransitionCopyCenter - MusicCurtainOverlay.ActualHeight / 2;
        if (p >= .44 && musicSceneVisible != musicTransitionTarget)
            SetMusicScene(musicTransitionTarget);
    }

    void SetMusicScene(bool music)
    {
        bool changed = musicSceneVisible != music;
        musicSceneVisible = music;
        StopMotion(MusicOverlay, OpacityProperty);
        MusicOverlay.Opacity = 1;
        MusicOverlay.Visibility = music ? Visibility.Visible : Visibility.Collapsed;
        if (!music) StopRecordRotation();
        // Resolve the latest media, not the media that started a previous animation.
        cinemaRevision = playback.MediaRevision;
        var snapshot = playback.Snapshot;
        SnapCinemaPresentation(!music && CinemaPresentation.ShouldImmerse(snapshot, playback.HasVideoOutput, false));
        if (changed) ApplyMusicPlayerLayout();
        ShowPlaybackControls();
    }

    void CompleteMusicTransition()
    {
        bool restoreFocus = MusicCurtainOverlay.IsKeyboardFocusWithin;
        ++musicTransitionEpoch;
        musicTransitionActive = false;
        BeginAnimation(MusicCurtainProgressProperty, null);
        MusicCurtainProgress = 0;
        SetMusicScene(musicMode);
        MusicCurtainOverlay.Visibility = Visibility.Collapsed;
        MusicCurtainCopy.Opacity = 0;
        if (restoreFocus && !closing && IsVisible) OverlayLayer.Focus();
    }

    void MusicCurtainSkip_Click(object sender, RoutedEventArgs e)
    { CompleteMusicTransition(); e.Handled = true; }
}
