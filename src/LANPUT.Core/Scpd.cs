using System.Xml.Linq;

namespace LANPUT.Core;

public sealed record StateVariable(string Name, string Type, string Default = "", bool Events = false, string[]? Allowed = null, bool VolumeRange = false);
public sealed record Argument(string Name, string Direction, string State);
public sealed record ActionDefinition(string Name, Argument[] Args);
public static class Scpd
{
    static Argument In(string n, string s) => new(n, "in", s);
    static Argument Out(string n, string s) => new(n, "out", s);
    static ActionDefinition Act(string n, params Argument[] a) => new(n, a);
    static readonly Argument Instance = In("InstanceID", "A_ARG_TYPE_InstanceID");
    static readonly Argument Channel = In("Channel", "A_ARG_TYPE_Channel");
    public static ActionDefinition[] Actions(string service) => service switch
    {
        UpnpProtocol.Av => [
            Act("SetAVTransportURI", Instance, In("CurrentURI", "AVTransportURI"), In("CurrentURIMetaData", "AVTransportURIMetaData")),
            Act("GetTransportInfo", Instance, Out("CurrentTransportState", "TransportState"), Out("CurrentTransportStatus", "TransportStatus"), Out("CurrentSpeed", "TransportPlaySpeed")),
            Act("GetPositionInfo", Instance, Out("Track", "CurrentTrack"), Out("TrackDuration", "CurrentTrackDuration"), Out("TrackMetaData", "CurrentTrackMetaData"), Out("TrackURI", "CurrentTrackURI"), Out("RelTime", "RelativeTimePosition"), Out("AbsTime", "AbsoluteTimePosition"), Out("RelCount", "RelativeCounterPosition"), Out("AbsCount", "AbsoluteCounterPosition")),
            Act("GetMediaInfo", Instance, Out("NrTracks", "NumberOfTracks"), Out("MediaDuration", "CurrentMediaDuration"), Out("CurrentURI", "AVTransportURI"), Out("CurrentURIMetaData", "AVTransportURIMetaData"), Out("NextURI", "NextAVTransportURI"), Out("NextURIMetaData", "NextAVTransportURIMetaData"), Out("PlayMedium", "PlaybackStorageMedium"), Out("RecordMedium", "RecordStorageMedium"), Out("WriteStatus", "RecordMediumWriteStatus")),
            Act("GetDeviceCapabilities", Instance, Out("PlayMedia", "PossiblePlaybackStorageMedia"), Out("RecMedia", "PossibleRecordStorageMedia"), Out("RecQualityModes", "PossibleRecordQualityModes")),
            Act("GetTransportSettings", Instance, Out("PlayMode", "CurrentPlayMode"), Out("RecQualityMode", "CurrentRecordQualityMode")),
            Act("GetCurrentTransportActions", Instance, Out("Actions", "CurrentTransportActions")),
            Act("Play", Instance, In("Speed", "TransportPlaySpeed")), Act("Pause", Instance), Act("Stop", Instance),
            Act("Seek", Instance, In("Unit", "A_ARG_TYPE_SeekMode"), In("Target", "A_ARG_TYPE_SeekTarget")),
            Act("SetPlayMode", Instance, In("NewPlayMode", "CurrentPlayMode"))],
        UpnpProtocol.Rc => [Act("GetVolume", Instance, Channel, Out("CurrentVolume", "Volume")),
            Act("SetVolume", Instance, Channel, In("DesiredVolume", "Volume")),
            Act("GetMute", Instance, Channel, Out("CurrentMute", "Mute")), Act("SetMute", Instance, Channel, In("DesiredMute", "Mute")),
            Act("ListPresets", Instance, Out("CurrentPresetNameList", "PresetNameList")), Act("SelectPreset", Instance, In("PresetName", "A_ARG_TYPE_PresetName"))],
        UpnpProtocol.Cm => [Act("GetProtocolInfo", Out("Source", "SourceProtocolInfo"), Out("Sink", "SinkProtocolInfo")),
            Act("GetCurrentConnectionIDs", Out("ConnectionIDs", "CurrentConnectionIDs")),
            Act("GetCurrentConnectionInfo", In("ConnectionID", "A_ARG_TYPE_ConnectionID"), Out("RcsID", "A_ARG_TYPE_RcsID"), Out("AVTransportID", "A_ARG_TYPE_AVTransportID"), Out("ProtocolInfo", "A_ARG_TYPE_ProtocolInfo"), Out("PeerConnectionManager", "A_ARG_TYPE_ConnectionManager"), Out("PeerConnectionID", "A_ARG_TYPE_ConnectionID"), Out("Direction", "A_ARG_TYPE_Direction"), Out("Status", "A_ARG_TYPE_ConnectionStatus"))],
        _ => throw new ArgumentException("Unknown service")
    };
    public static StateVariable[] Variables(string service)
    {
        StateVariable[] defaults = service switch
        {
            UpnpProtocol.Av => new StateVariable[] {
                new("LastChange", "string", Events:true), new("TransportState", "string", "NO_MEDIA_PRESENT", Allowed:["STOPPED","PLAYING","PAUSED_PLAYBACK","TRANSITIONING","NO_MEDIA_PRESENT"]),
                new("TransportStatus", "string", "OK", Allowed:["OK","ERROR_OCCURRED"]), new("TransportPlaySpeed", "string", "1", Allowed:["1"]),
                new("CurrentPlayMode", "string", "NORMAL", Allowed:["NORMAL"]), new("CurrentTrack", "ui4", "0"), new("NumberOfTracks", "ui4", "0"),
                new("RelativeCounterPosition", "i4", "-1"), new("AbsoluteCounterPosition", "i4", "-1"),
                new("A_ARG_TYPE_SeekMode", "string", Allowed:["REL_TIME","ABS_TIME"]), new("A_ARG_TYPE_InstanceID", "ui4", "0") },
            UpnpProtocol.Rc => [new("LastChange", "string", Events:true), new("A_ARG_TYPE_InstanceID", "ui4", "0"),
                new("A_ARG_TYPE_Channel", "string", "Master", Allowed:["Master"]), new("Volume", "ui2", "70", VolumeRange:true),
                new("Mute", "boolean", "0"), new("PresetNameList", "string", "FactoryDefaults"), new("A_ARG_TYPE_PresetName", "string", "FactoryDefaults", Allowed:["FactoryDefaults"])],
            _ => [new("SourceProtocolInfo", "string", Events:true), new("SinkProtocolInfo", "string", UpnpProtocol.ProtocolInfo, true),
                new("CurrentConnectionIDs", "string", "0", true), new("A_ARG_TYPE_ConnectionID", "i4", "0"), new("A_ARG_TYPE_RcsID", "i4", "0"),
                new("A_ARG_TYPE_AVTransportID", "i4", "0"), new("A_ARG_TYPE_Direction", "string", "Input", Allowed:["Input","Output"]),
                new("A_ARG_TYPE_ConnectionStatus", "string", "OK", Allowed:["OK","ContentFormatMismatch","InsufficientBandwidth","UnreliableChannel","Unknown"])]
        };
        var vars = defaults.ToDictionary(v => v.Name);
        foreach (var arg in Actions(service).SelectMany(a => a.Args))
            vars.TryAdd(arg.State, new StateVariable(arg.State, arg.State.EndsWith("URI") ? "uri" : "string"));
        return vars.Values.ToArray();
    }
    public static string Document(string service)
    {
        XNamespace s = "urn:schemas-upnp-org:service-1-0";
        return new XDocument(new XElement(s + "scpd", new XElement(s + "specVersion", new XElement(s + "major", 1), new XElement(s + "minor", 0)),
            new XElement(s + "actionList", Actions(service).Select(a => new XElement(s + "action", new XElement(s + "name", a.Name),
                a.Args.Length == 0 ? null : new XElement(s + "argumentList", a.Args.Select(v => new XElement(s + "argument", new XElement(s + "name", v.Name),
                    new XElement(s + "direction", v.Direction), new XElement(s + "relatedStateVariable", v.State))))))),
            new XElement(s + "serviceStateTable", Variables(service).Select(v => new XElement(s + "stateVariable", new XAttribute("sendEvents", v.Events ? "yes" : "no"),
                new XElement(s + "name", v.Name), new XElement(s + "dataType", v.Type), v.Default.Length == 0 ? null : new XElement(s + "defaultValue", v.Default),
                v.Allowed is null ? null : new XElement(s + "allowedValueList", v.Allowed.Select(a => new XElement(s + "allowedValue", a))),
                v.VolumeRange ? new XElement(s + "allowedValueRange", new XElement(s + "minimum", 0), new XElement(s + "maximum", 100), new XElement(s + "step", 1)) : null))))).ToString();
    }
}
