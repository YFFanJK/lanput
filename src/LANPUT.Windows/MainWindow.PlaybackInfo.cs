using System.Windows;
using System.Windows.Input;

namespace LANPUT.Windows;

public partial class MainWindow
{
    DateTime nextPlaybackInfoRefresh;

    void PlaybackInfo_Click(object sender, RoutedEventArgs e)
    {
        if (closing) return;
        if (PlaybackInfoPopup.IsOpen)
        { CloseInterfacePopup(PlaybackInfoPopup, PlaybackInfoPanel); return; }
        popupReturnFocus = Keyboard.FocusedElement;
        SettingsPopup.IsOpen = DiagnosticsPopup.IsOpen = false;
        PlaybackInfoPanel.Width = Math.Max(350, Math.Min(540, ActualWidth - 36));
        PlaybackInfoText.Height = Math.Max(150, Math.Min(390, ActualHeight - 205));
        PlaybackInfoPopup.IsOpen = true;
        nextPlaybackInfoRefresh = DateTime.MinValue;
        UpdatePlaybackInfoPanel();
    }

    void ClosePlaybackInfo_Click(object sender, RoutedEventArgs e) => CloseInterfacePopup(PlaybackInfoPopup, PlaybackInfoPanel);

    void UpdatePlaybackInfoPanel()
    {
        if (!PlaybackInfoPopup.IsOpen || closing || DateTime.UtcNow < nextPlaybackInfoRefresh) return;
        nextPlaybackInfoRefresh = DateTime.UtcNow.AddSeconds(1);
        try
        {
            var text = playback.ReadPlaybackInfo().ToDisplayText();
            if (PlaybackInfoText.Text != text) PlaybackInfoText.Text = text;
        }
        catch (Exception error)
        {
            PlaybackInfoText.Text = "播放信息暂时不可用，请稍后重试。";
            Log("播放信息读取未完成 · " + error.GetType().Name);
        }
    }
}
