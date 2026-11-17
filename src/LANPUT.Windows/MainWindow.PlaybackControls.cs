using System.Windows;
using System.Windows.Input;
using LANPUT.Core;

namespace LANPUT.Windows;

public partial class MainWindow
{
    void Surface_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (musicTransitionActive) { CompleteMusicTransition(); e.Handled = true; return; }
        if (startupActive) { DismissStartupPresentation(); e.Handled = true; return; }
        if (e.ClickCount != 2 || SettingsPopup.IsOpen || DiagnosticsPopup.IsOpen || PlaybackInfoPopup.IsOpen || closing) return;
        if (e.OriginalSource is DependencyObject source && IsEditingControl(source, true)) return;
        ToggleFullscreen(); e.Handled = true;
    }

    bool HandlePlaybackShortcut(Key key)
    {
        if (key is Key.Left or Key.Right)
        {
            if (!playback.CanSeek || PlaybackNavigation.RelativeSeek(playback.Snapshot.PositionMs,
                playback.Snapshot.DurationMs, key == Key.Right ? 10000 : -10000) is not { } target)
            { ShowMotionToast("当前媒体不支持快进或后退", "Info"); return true; }
            try
            {
                playback.Seek(target);
                if (!dragging) PositionSlider.Value = target;
                PositionLabel.Text = XmlSafe.Time(target);
                ShowMotionToast((key == Key.Right ? "快进 10 秒 · " : "后退 10 秒 · ") + XmlSafe.Time(target), "Info");
            }
            catch (UpnpException) { ShowMotionToast("当前媒体暂不支持调整进度", "Info"); }
            return true;
        }
        if (key is Key.Up or Key.Down)
        {
            int volume = Math.Clamp(playback.Snapshot.Volume + (key == Key.Up ? 5 : -5), 0, 100);
            playback.SetVolume(volume);
            if (volume > 0 && playback.Snapshot.Muted) playback.SetMute(false);
            ShowMotionToast("音量 " + volume + "%", "Info");
            return true;
        }
        if (key == Key.M) { Mute_Click(this, new RoutedEventArgs()); return true; }
        return false;
    }

    void Mute_Click(object sender, RoutedEventArgs e)
    {
        playback.SetMute(!playback.Snapshot.Muted);
        ShowMotionToast(playback.Snapshot.Muted ? "已静音" : "已取消静音", "Info");
    }
}
