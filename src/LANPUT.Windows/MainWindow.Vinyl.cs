using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using LANPUT.Core;

namespace LANPUT.Windows;

public partial class MainWindow
{
    AnimationClock? recordClock;
    bool recordRunning;
    readonly ToolTip musicSeekPreview = new() { Background = new SolidColorBrush(Color.FromRgb(32, 36, 32)),
        Foreground = new SolidColorBrush(Color.FromRgb(234, 212, 164)), BorderBrush = new SolidColorBrush(Color.FromRgb(94, 78, 49)),
        BorderThickness = new Thickness(1), Padding = new Thickness(9, 5, 9, 5), FontSize = 11, Placement = PlacementMode.Relative };

    void UpdateRecordRotation(PlaybackSnapshot snapshot)
    {
        bool run = musicMode && snapshot.State == "PLAYING" && playback.Activity.Phase == PlaybackPhase.Playing
            && MotionEnabled && VinylFrame.IsVisible;
        if (recordClock is null && run)
        {
            var animation = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(26)) { RepeatBehavior = RepeatBehavior.Forever };
            recordClock = (AnimationClock)animation.CreateClock(true);
            RecordRotation.ApplyAnimationClock(RotateTransform.AngleProperty, recordClock);
            recordRunning = true;
        }
        if (recordClock is null || recordRunning == run) return;
        if (run) recordClock.Controller?.Resume(); else recordClock.Controller?.Pause();
        recordRunning = run;
    }
    void StopRecordRotation()
    {
        recordClock?.Controller?.Remove(); recordClock = null; recordRunning = false;
        RecordRotation.ApplyAnimationClock(RotateTransform.AngleProperty, null); RecordRotation.Angle = 0;
        musicSeekPreview.IsOpen = false;
    }
    void MusicLayout_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (MusicTitle is null || MusicToolsScroll is null) return;
        MusicTitle.FontSize = Math.Clamp(MusicLayout.ActualWidth * .026, 19, 34);
        MusicCurrent.FontSize = Math.Clamp(Math.Min(MusicLayout.ActualWidth * .033, MusicLayout.ActualHeight * .1), 20, 43);
        MusicPrevious.FontSize = MusicNext.FontSize = Math.Clamp(MusicLayout.ActualWidth * .019, 13, 20);
        MusicCurrent.MaxHeight = Math.Max(70, MusicLayout.ActualHeight * .5);
        LyricsPlainScroll.MaxHeight = Math.Max(90, MusicLayout.ActualHeight - 90);
        MusicTools.MaxWidth = Math.Max(88, Math.Min(500, MusicOverlay.ActualWidth - 48));
        MusicToolsScroll.MaxHeight = Math.Clamp(MusicLayout.ActualHeight - 32, 60, 320);
        MusicFarPrevious.Visibility = MusicFarNext.Visibility = MusicLayout.ActualHeight >= 430 ? Visibility.Visible : Visibility.Collapsed;
    }
    void DetachPlaybackControls()
    { if (ControlsPanel.Parent is Panel panel) panel.Children.Remove(ControlsPanel); }

    void ApplyMusicPlayerLayout()
    {
        musicSeekPreview.IsOpen = false;
        DetachPlaybackControls();
        if (musicSceneVisible)
        {
            MusicControlsHost.Children.Add(ControlsPanel);
            Grid.SetRow(ControlsPanel, 0);
            ControlsPanel.Margin = new Thickness(0); ControlsPanel.Padding = new Thickness(0, 4, 0, 0);
            ControlsPanel.Background = Brushes.Transparent; ControlsPanel.CornerRadius = new CornerRadius(0);
            ControlsPanel.Height = 102; ControlsPanel.MaxWidth = double.PositiveInfinity;
            ControlsPanel.VerticalAlignment = VerticalAlignment.Bottom; ControlsPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
        }
        else
        {
            (fullscreen ? OverlayLayer : LayoutRoot).Children.Add(ControlsPanel);
            Grid.SetRow(ControlsPanel, fullscreen ? 1 : 3);
            ControlsPanel.Padding = new Thickness(24, 12, 24, 8);
            ControlsPanel.Background = new SolidColorBrush(Color.FromArgb(fullscreen ? (byte)235 : (byte)255, 14, 17, 20));
            ControlsPanel.Margin = fullscreen ? new Thickness(32, 0, 32, 26) : new Thickness(0);
            ControlsPanel.CornerRadius = new CornerRadius(fullscreen ? 13 : 0);
            ControlsPanel.Height = fullscreen ? 98 : double.NaN;
            ControlsPanel.MaxWidth = fullscreen ? 860 : double.PositiveInfinity;
            ControlsPanel.VerticalAlignment = fullscreen ? VerticalAlignment.Bottom : VerticalAlignment.Stretch;
        }
        LayoutRoot.RowDefinitions[3].Height = new GridLength(musicSceneVisible ? 0 : 102);
        // Reuse the same real controls and seek handlers in every mode.
        Grid.SetColumn(PlaybackTransport, musicSceneVisible ? 1 : 0);
        Grid.SetColumn(PlaybackTrackDetails, musicSceneVisible ? 0 : 1);
        PlaybackButtonsGrid.ColumnDefinitions[0].Width = musicSceneVisible ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
        PlaybackButtonsGrid.ColumnDefinitions[1].Width = musicSceneVisible ? GridLength.Auto : new GridLength(1, GridUnitType.Star);
        PlaybackButtonsGrid.ColumnDefinitions[2].Width = musicSceneVisible ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
        PlaybackTransport.HorizontalAlignment = musicSceneVisible ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        PlaybackTrackDetails.Margin = new Thickness(0, 0, musicSceneVisible ? 32 : 16, 0);
        PlaybackRight.HorizontalAlignment = HorizontalAlignment.Right;
        if (StopMediaButton.Parent is Panel stopParent) stopParent.Children.Remove(StopMediaButton);
        if (musicSceneVisible) PlaybackRight.Children.Insert(0, StopMediaButton);
        else PlaybackTransport.Children.Insert(PlaybackTransport.Children.IndexOf(TransportDivider), StopMediaButton);
        TransportDivider.Visibility = musicSceneVisible ? Visibility.Collapsed : Visibility.Visible;
        PreviousLyricButton.Visibility = NextLyricButton.Visibility = musicSceneVisible ? Visibility.Visible : Visibility.Collapsed;
        PlayButton.Width = PlayButton.Height = musicSceneVisible ? 45 : 39;
        PlayButton.Style = (Style)FindResource(musicSceneVisible ? "MusicPlayButton" : typeof(Button));
        PlayButton.Padding = new Thickness(musicSceneVisible ? 13 : 11);
        PlayButton.Background = musicSceneVisible ? new LinearGradientBrush(Color.FromRgb(242, 219, 164), Color.FromRgb(213, 174, 96), 135)
            : new SolidColorBrush(Color.FromRgb(232, 200, 122));
        PositionLabel.FontSize = DurationLabel.FontSize = 12;
        PositionSlider.Style = (Style)FindResource(musicSceneVisible ? "MusicSeekSlider" : typeof(Slider));
        MediaTitle.Foreground = (Brush)FindResource("TextPrimary");
        SetPlaybackControlsVisible(true);
    }
    void MusicSeek_MouseMove(object sender, MouseEventArgs e)
    {
        if (!musicMode || !PositionSlider.IsEnabled || playback.Snapshot.DurationMs <= 0) return;
        double x = Math.Clamp(e.GetPosition(PositionSlider).X, 7, Math.Max(7, PositionSlider.ActualWidth - 7));
        double fraction = (x - 7) / Math.Max(1, PositionSlider.ActualWidth - 14);
        musicSeekPreview.Content = XmlSafe.Time((long)(fraction * playback.Snapshot.DurationMs));
        musicSeekPreview.PlacementTarget = PositionSlider; musicSeekPreview.HorizontalOffset = x - 25;
        musicSeekPreview.VerticalOffset = -28; musicSeekPreview.IsOpen = true;
    }
    void MusicSeek_MouseLeave(object sender, MouseEventArgs e) => musicSeekPreview.IsOpen = false;
    void PreviousLyric_Click(object sender, RoutedEventArgs e) => SeekLyric(-1);
    void NextLyric_Click(object sender, RoutedEventArgs e) => SeekLyric(1);
    void SeekLyric(int direction)
    {
        if (lyrics is null || lyrics.Lines.Count == 0 || !playback.CanSeek) return;
        long position = playback.Snapshot.PositionMs;
        int index = lyrics.IndexAt(position, lyricAdjustment);
        int target = direction < 0 && index >= 0 && position + lyrics.OffsetMs + lyricAdjustment > lyrics.Lines[index].TimeMs + 2000
            ? index : Math.Clamp(index + direction, 0, lyrics.Lines.Count - 1);
        try { playback.Seek(Math.Clamp(lyrics.Lines[target].TimeMs - (long)lyrics.OffsetMs - lyricAdjustment, 0, playback.Snapshot.DurationMs)); }
        catch (UpnpException) { Log("当前音乐暂不支持跳转。"); }
    }
}
