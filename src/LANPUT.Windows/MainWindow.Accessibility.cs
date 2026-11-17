using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace LANPUT.Windows;
public partial class MainWindow
{
    IInputElement? popupReturnFocus;
    void InitializeInterfaceAccessibility()
    {
        SettingsPopup.Opened += (_, _) => Dispatcher.InvokeAsync(() =>
        {
            if (SettingsPopup.IsOpen) Keyboard.Focus(DeviceName.IsEnabled ? DeviceName : CloseSettingsButton);
        }, DispatcherPriority.Input);
        DiagnosticsPopup.Opened += (_, _) => Dispatcher.InvokeAsync(() =>
        {
            if (DiagnosticsPopup.IsOpen) CloseDiagnosticsButton.Focus();
        }, DispatcherPriority.Input);
        PlaybackInfoPopup.Opened += (_, _) => Dispatcher.InvokeAsync(() =>
        { if (PlaybackInfoPopup.IsOpen) ClosePlaybackInfoButton.Focus(); }, DispatcherPriority.Input);
        PlaybackInfoPopup.Closed += (_, _) => RestorePopupFocus();
        SettingsPopup.Closed += (_, _) => RestorePopupFocus();
        DiagnosticsPopup.Closed += (_, _) => RestorePopupFocus();
    }
    void RestorePopupFocus() => Dispatcher.InvokeAsync(() =>
    {
        if (closing || !IsVisible || SettingsPopup.IsOpen || DiagnosticsPopup.IsOpen || PlaybackInfoPopup.IsOpen) return;
        // Dismissing by mouse must not steal focus from the newly clicked control.
        if (Keyboard.FocusedElement is UIElement current && current.IsVisible && current.IsEnabled) return;
        if (popupReturnFocus is UIElement previous && previous.IsVisible && previous.IsEnabled) previous.Focus();
        else if (SettingsButton.IsVisible) SettingsButton.Focus();
        else OverlayLayer.Focus();
    }, DispatcherPriority.Input);
    void PlayerSurface_SizeChanged(object sender, SizeChangedEventArgs e)
    { ApplyCinemaGeometry(); UpdateIdleLayout(IdleOverlay.ActualHeight); }

    void IdleOverlay_SizeChanged(object sender, SizeChangedEventArgs e)
        => UpdateIdleLayout(e.NewSize.Height);

    void UpdateIdleLayout(double availableHeight)
    {
        if (IdleContent is null) return;
        bool compact = availableHeight * .52 < 210;
        IdleArtwork.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        IdleEyebrow.Visibility = Visibility.Collapsed;
        IdleDetail.Visibility = availableHeight * .52 < 125 ? Visibility.Collapsed : Visibility.Visible;
        IdleContent.Margin = new Thickness(compact ? 16 : 24);
        IdleTitle.FontSize = compact ? 32 : 40;
        IdleHint.FontSize = compact ? 20 : 22;
    }
}
