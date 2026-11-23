using LANPUT.Core;
using LibVLCSharp.Shared;

namespace LANPUT.Desktop;

public sealed partial class VlcPlayback
{
    bool audioOnly;
    bool knownVideoTrack;
    public bool HasKnownVideoTrack => musicRevision == MediaRevision && knownVideoTrack;
    public bool IsAudioOnly => musicRevision == MediaRevision && audioOnly;
    public MusicIdentity Music { get; private set; } = new();
    long musicRevision = -1;
    DateTime nextMusicRead;
    void RefreshMusic()
    {
        if (musicRevision != MediaRevision)
        {
            musicRevision = MediaRevision; nextMusicRead = DateTime.MinValue;
            audioOnly = knownVideoTrack = false; Music = MusicIdentity.FromDidl(snapshot.Metadata);
        }
        if (media is null || DateTime.UtcNow < nextMusicRead) return;
        nextMusicRead = DateTime.UtcNow.AddSeconds(1);
        var tracks = media.Tracks;
        knownVideoTrack = tracks.Any(t => t.TrackType == TrackType.Video);
        audioOnly = tracks.Any(t => t.TrackType == TrackType.Audio) && !knownVideoTrack && !HasVideoOutput;
        if (!IsAudioOnly) return;
        var didl = MusicIdentity.FromDidl(snapshot.Metadata);
        string Value(string preferred, MetadataType type) => preferred.Length > 0 ? preferred : MusicIdentity.Clean(media.Meta(type));
        Music = new(Value(didl.Title, MetadataType.Title), Value(didl.Artist, MetadataType.Artist), Value(didl.Album, MetadataType.Album));
        if (Music.Title.Length == 0) Music = Music with { Title = MusicIdentity.Clean(snapshot.Title) };
    }
}
