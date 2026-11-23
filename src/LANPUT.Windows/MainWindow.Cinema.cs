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
        SetCinemaWelcomeVisible(true);
        CinemaLobby.IsHitTestVisible = false;
        LobbyNotice.Visibility = Visibility.Collapsed;
        StopMotion(MotionToast, OpacityProperty);
        MotionToast.Visibility = Visibility.Collapsed;
        playback.SetPresentationHold(target);
        SetPlaybackControlsVisible(!fullscreen);
        // Only the photograph and welcome copy move. VideoView and its HWND stay full-size.
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

    void SetCinemaWelcomeVisible(bool visible)
    {
        // Switch both layers in the same UI operation, before the next frame.
        if (visible) IdleContent.Visibility = Visibility.Hidden;
        CinemaWelcomeCopy.Visibility = CinemaWelcomeOverlay.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        if (!visible) IdleContent.Visibility = Visibility.Visible;
    }

    void SnapCinemaPresentation(bool target)
    {
        ++cinemaTransitionEpoch;
        StopMotion(this, CinemaProgressProperty);
        StopMotion(CinemaScene, OpacityProperty);
        cinemaTransitioning = false;
        cinemaImmersive = cinemaVideoRevealed = target;
        CinemaScene.Opacity = 1;
        SetCinemaWelcomeVisible(false);
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
        CinemaScreenFrame.Width = CinemaArtworkLayout.ContentWidth;
        CinemaScreenFrame.Height = CinemaArtworkLayout.ContentHeight;
        ApplyCinemaVisuals();
    }

    void CinemaScene_SizeChanged(object sender, SizeChangedEventArgs e) => ApplyCinemaVisuals();

    void ApplyCinemaVisuals()
    {
        if (CinemaScreenScale is null || CinemaWelcomeShift is null || CinemaLobby is null) return;
        double progress = Math.Clamp(CinemaProgress, 0, 1);
        double width = Math.Max(1, CinemaScene.ActualWidth), height = Math.Max(1, CinemaScene.ActualHeight);
        var screen = CinemaArtworkLayout.ScreenIn(width, height);
        var pixels = CinemaArtworkLayout.ScreenPixels;
        // The photograph and screen copy share one source-coordinate canvas.
        // WPF's Viewbox owns resizing/DPI updates, so a StateChanged callback
        // cannot leave the copy at the previous window's screen coordinates.
        CinemaScreenScale.ScaleX = CinemaScreenScale.ScaleY = pixels.Width / CinemaArtworkLayout.ContentWidth;
        CinemaScreenTranslate.X = pixels.X;
        CinemaScreenTranslate.Y = pixels.Y;
        double endZoom = Math.Max(width / Math.Max(.0001, screen.Width), height / Math.Max(.0001, screen.Height));
        CinemaHallScale.CenterX = pixels.CenterX;
        CinemaHallScale.CenterY = pixels.CenterY;
        CinemaHallScale.ScaleX = CinemaHallScale.ScaleY = 1 + (endZoom - 1) * progress;
        CinemaHallTranslate.X = (CinemaArtworkLayout.ImageWidth / 2 - pixels.CenterX) * progress;
        CinemaHallTranslate.Y = (CinemaArtworkLayout.ImageHeight / 2 - pixels.CenterY) * progress;
        // Keep the copy in window coordinates so WPF draws the text at the
        // display's DPI instead of enlarging the cached silver-screen layer.
        CinemaWelcomeShift.X = (screen.CenterX - width / 2) * (1 - progress);
        CinemaWelcomeShift.Y = (screen.CenterY - height / 2) * (1 - progress);
        CinemaHallArt.Opacity = 1;
        CinemaLobby.Opacity = Math.Clamp(1 - progress * 3, 0, 1);
        CinemaLobby.IsHitTestVisible = !cinemaImmersive && !cinemaTransitioning;
        CinemaExitFullscreenButton.Visibility = fullscreen && !cinemaImmersive ? Visibility.Visible : Visibility.Collapsed;
        if (fullscreen && !musicMode) ControlsPanel.MaxWidth = 860;
    }
}
