namespace LANPUT.Core;

public static class CinemaPresentation
{
    // First entry requires real video output. Once seated in the film, a pause
    // or temporary buffering/Vout loss must not send the viewer back to the hall.
    public static bool ShouldImmerse(PlaybackSnapshot media, bool hasVideo, bool alreadyImmersed)
    {
        if (media.Status != "OK" || media.Uri.Length == 0 || media.State is "STOPPED" or "NO_MEDIA_PRESENT") return false;
        return media.State switch
        {
            "PLAYING" or "PAUSED_PLAYBACK" => hasVideo || alreadyImmersed,
            "TRANSITIONING" => alreadyImmersed,
            _ => false
        };
    }
}
