using System.Windows;
using System.Windows.Media;
using LANPUT.Core;

namespace LANPUT.Windows;

public partial class MainWindow
{
    public static readonly DependencyProperty CinemaProgressProperty = DependencyProperty.Register(
        nameof(CinemaProgress), typeof(double), typeof(MainWindow),
        new PropertyMetadata(0d, (d, _) => ((MainWindow)d).ApplyCinemaVisuals()));
    public double CinemaProgress { get => (double)GetValue(CinemaProgressProperty); set => SetValue(CinemaProgressProperty, value); }
    bool cinemaImmersive;
    bool cinemaVideoRevealed, cinemaTransitioning;
    long cinemaRevision = -1, cinemaTransitionEpoch;

    void UpdateCinemaTransition(PlaybackSnapshot snapshot)
    {
        long revision = playback.MediaRevision;
        bool sameMedia = cinemaRevision == revision;
        cinemaRevision = revision;
        bool target = CinemaPresentation.ShouldImmerse(snapshot, playback.HasVideoOutput, sameMedia && cinemaImmersive);
        if (target == cinemaImmersive && (sameMedia || !target))
        {
            if (!MotionEnabled && cinemaTransitioning) SnapCinemaPresentation(target);
            return;
        }
        cinemaImmersive = target;
        if (!MotionEnabled) { SnapCinemaPresentation(target); return; }
        long epoch = ++cinemaTransitionEpoch;
        cinemaTransitioning = true;
        cinemaVideoRevealed = false;
        StopMotion(CinemaScene, OpacityProperty);
        CinemaScene.Opacity = 1;
        CinemaScene.Visibility = Visibility.Visible;
        CinemaWelcomeText.Text = target ? "好戏马上上演" : "欢迎再次入座";
        CinemaWelcomeSubtitle.Text = target ? "灯光渐暗，把这一刻交给电影。" : "灯光渐亮，下一场依然为你留座。";
        CinemaWelcomeOverlay.Visibility = Visibility.Visible;
        CinemaLobby.IsHitTestVisible = false;
        LobbyNotice.Visibility = Visibility.Collapsed;
        StopMotion(MotionToast, OpacityProperty);
        MotionToast.Visibility = Visibility.Collapsed;
        playback.SetPresentationHold(target);
        SetPlaybackControlsVisible(!fullscreen);
        // Only cached WPF artwork moves. VideoView and its HWND stay full-size.
        AnimateValue(this, CinemaProgressProperty, target ? 1 : 0, target ? 1600 : 1100,
            completed: () =>
            {
                if (epoch != cinemaTransitionEpoch || closing) return;
                if (!target) { SnapCinemaPresentation(false); return; }
                if (playback.MediaRevision != revision || !CinemaPresentation.ShouldImmerse(playback.Snapshot, playback.HasVideoOutput, true))
                { UpdateCinemaTransition(playback.Snapshot); return; }
                AnimateValue(CinemaScene, OpacityProperty, 0, 180, completed: () =>
                {
                    if (epoch != cinemaTransitionEpoch || closing) return;
                    if (playback.MediaRevision != revision || !CinemaPresentation.ShouldImmerse(playback.Snapshot, playback.HasVideoOutput, true))
                    { UpdateCinemaTransition(playback.Snapshot); return; }
                    SnapCinemaPresentation(true);
                });
            });
    }

    void SnapCinemaPresentation(bool target)
    {
        ++cinemaTransitionEpoch;
        StopMotion(this, CinemaProgressProperty);
        StopMotion(CinemaScene, OpacityProperty);
        cinemaTransitioning = false;
        cinemaImmersive = cinemaVideoRevealed = target;
        CinemaScene.Opacity = 1;
        CinemaWelcomeOverlay.Visibility = Visibility.Collapsed;
        CinemaProgress = target ? 1 : 0;
        CinemaScene.Visibility = target ? Visibility.Collapsed : Visibility.Visible;
        playback.SetPresentationHold(false);
        ApplyCinemaVisuals();
        if (target) ShowPlaybackControls();
    }

    void ResetCinemaPresentation()
    {
        cinemaRevision = playback.MediaRevision;
        bool target = CinemaPresentation.ShouldImmerse(playback.Snapshot, playback.HasVideoOutput, cinemaImmersive);
        SnapCinemaPresentation(target);
    }

    // Called on actual surface resize, not on each animation frame.
    void ApplyCinemaGeometry()
    {
        if (Video is null || CinemaScreenFrame is null || OverlayLayer is null) return;
        Video.Margin = new Thickness(2);
        double width = Math.Max(1, PlayerSurface.ActualWidth - 4), height = Math.Max(1, PlayerSurface.ActualHeight - 4);
        if (CinemaScreenFrame.Width != width) CinemaScreenFrame.Width = width;
        if (CinemaScreenFrame.Height != height) CinemaScreenFrame.Height = height;
        ApplyCinemaVisuals();
    }

    void ApplyCinemaVisuals()
    {
        if (CinemaScreenScale is null || CinemaLobby is null) return;
        double progress = Math.Clamp(CinemaProgress, 0, 1), rest = 1 - progress;
        double width = Math.Max(1, CinemaScreenFrame.Width), height = Math.Max(1, CinemaScreenFrame.Height);
        CinemaScreenScale.ScaleX = CinemaScreenScale.ScaleY = .52 + .48 * progress;
        CinemaScreenTranslate.X = width * .24 * rest;
        CinemaScreenTranslate.Y = height * .065 * rest;
        CinemaHallArt.Opacity = rest;
        CinemaHallScale.ScaleX = CinemaHallScale.ScaleY = 1 + progress * 1.7;
        CinemaLobby.Opacity = Math.Clamp(1 - progress * 3, 0, 1);
        CinemaLobby.IsHitTestVisible = !cinemaImmersive && !cinemaTransitioning;
        CinemaExitFullscreenButton.Visibility = fullscreen && !cinemaImmersive ? Visibility.Visible : Visibility.Collapsed;
        if (fullscreen && !musicMode) ControlsPanel.MaxWidth = 860;
    }
}
