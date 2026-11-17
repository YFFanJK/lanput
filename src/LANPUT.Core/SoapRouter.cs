using System.Xml;
using System.Xml.Linq;

namespace LANPUT.Core;

public sealed class SoapRouter(IPlayback player)
{
    public string Execute(string service, string soapAction, string body)
    {
        XElement action;
        try
        {
            XNamespace s = "http://schemas.xmlsoap.org/soap/envelope/";
            var root = XmlSafe.Parse(body).Root;
            action = root?.Name == s + "Envelope" ? root.Element(s + "Body")?.Elements().SingleOrDefault()! : null!;
            if (action is null) throw new UpnpException(402, "Invalid Args");
        }
        catch (Exception e) when (e is XmlException or InvalidOperationException) { throw new UpnpException(402, "Invalid Args"); }
        var name = action.Name.LocalName;
        if (action.Name.NamespaceName != service || soapAction.Trim().Trim('"') != $"{service}#{name}") throw new UpnpException(401, "Invalid Action");
        var definition = Scpd.Actions(service).SingleOrDefault(a => a.Name == name) ?? throw new UpnpException(401, "Invalid Action");
        var args = action.Elements().ToArray();
        if (args.Select(a => a.Name.LocalName).Distinct().Count() != args.Length ||
            !args.Select(a => a.Name.LocalName).Order().SequenceEqual(definition.Args.Where(a => a.Direction == "in").Select(a => a.Name).Order()))
            throw new UpnpException(402, "Invalid Args");
        string Get(string n) => args.Single(a => a.Name.LocalName == n).Value;
        if (service is UpnpProtocol.Av or UpnpProtocol.Rc && Get("InstanceID") != "0") throw new UpnpException(service == UpnpProtocol.Av ? 718 : 702, "Invalid InstanceID");
        if (definition.Args.Any(a => a.Name == "Channel") && Get("Channel") != "Master") throw new UpnpException(402, "Invalid Args");
        var output = new Dictionary<string, string>();
        void Put(params string[] pairs) { for (int i = 0; i < pairs.Length; i += 2) output[pairs[i]] = pairs[i + 1]; }
        var p = player.Snapshot;
        if (service == UpnpProtocol.Av)
        {
            switch (name)
            {
                case "SetAVTransportURI":
                    var raw = Get("CurrentURI"); var metadata = Get("CurrentURIMetaData");
                    if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || uri.UserInfo.Length != 0 || raw.Contains('\r') || raw.Contains('\n'))
                        throw new UpnpException(716, "Resource not found");
                    var title = "手机投屏影片";
                    if (metadata.Length != 0)
                    {
                        try { title = XmlSafe.Parse(metadata).Descendants(XName.Get("title", "http://purl.org/dc/elements/1.1/")).FirstOrDefault()?.Value ?? title; }
                        catch (XmlException) { throw new UpnpException(402, "Invalid Args"); }
                    }
                    if (string.IsNullOrWhiteSpace(title) || title.Trim() is "null" or "undefined") title = "手机投屏影片";
                    player.Load(raw, metadata, title); break;
                case "Play":
                    if (Get("Speed") != "1") throw new UpnpException(717, "Play speed not supported");
                    if (p.Uri.Length == 0) throw new UpnpException(701, "Transition not available");
                    // Retried Play must not interrupt a media-open already in progress.
                    if (p.State is not ("PLAYING" or "TRANSITIONING")) player.Play(); break;
                case "Pause":
                    if (p.State == "PAUSED_PLAYBACK") break;
                    if (p.State != "PLAYING" && !(p.State == "TRANSITIONING" && player.AllowsTransitionPause))
                        throw new UpnpException(701, "Transition not available");
                    player.Pause(); break;
                case "Stop": player.Stop(); break;
                case "Seek":
                    if (Get("Unit") is not ("REL_TIME" or "ABS_TIME")) throw new UpnpException(710, "Seek mode not supported");
                    var target = XmlSafe.ParseTime(Get("Target"));
                    if (p.Uri.Length == 0) throw new UpnpException(701, "Transition not available");
                    if (p.DurationMs > 0 && target > p.DurationMs) throw new UpnpException(711, "Illegal seek target");
                    player.Seek(target); break;
                case "SetPlayMode": if (Get("NewPlayMode") != "NORMAL") throw new UpnpException(712, "Play mode not supported"); break;
                case "GetTransportInfo": Put("CurrentTransportState", p.State, "CurrentTransportStatus", p.Status, "CurrentSpeed", "1"); break;
                case "GetPositionInfo": Put("Track", p.Uri.Length == 0 ? "0" : "1", "TrackDuration", XmlSafe.Time(p.DurationMs), "TrackMetaData", p.Metadata,
                    "TrackURI", p.Uri, "RelTime", XmlSafe.Time(p.PositionMs), "AbsTime", XmlSafe.Time(p.PositionMs), "RelCount", "-1", "AbsCount", "-1"); break;
                case "GetMediaInfo": Put("NrTracks", p.Uri.Length == 0 ? "0" : "1", "MediaDuration", XmlSafe.Time(p.DurationMs), "CurrentURI", p.Uri,
                    "CurrentURIMetaData", p.Metadata, "NextURI", "", "NextURIMetaData", "", "PlayMedium", "NETWORK", "RecordMedium", "NOT_IMPLEMENTED", "WriteStatus", "NOT_IMPLEMENTED"); break;
                case "GetDeviceCapabilities": Put("PlayMedia", "NETWORK", "RecMedia", "NOT_IMPLEMENTED", "RecQualityModes", "NOT_IMPLEMENTED"); break;
                case "GetTransportSettings": Put("PlayMode", "NORMAL", "RecQualityMode", "NOT_IMPLEMENTED"); break;
                case "GetCurrentTransportActions": Put("Actions", p.Uri.Length == 0 ? "" : "Play,Pause,Stop,Seek"); break;
            }
        }
        else if (service == UpnpProtocol.Rc)
        {
            switch (name)
            {
                case "SetVolume": if (!int.TryParse(Get("DesiredVolume"), out var v) || v is < 0 or > 100) throw new UpnpException(402, "Invalid Args"); player.SetVolume(v); break;
                case "GetVolume": Put("CurrentVolume", p.Volume.ToString(System.Globalization.CultureInfo.InvariantCulture)); break;
                case "SetMute": var m = Get("DesiredMute"); if (m is not ("0" or "1" or "true" or "false")) throw new UpnpException(402, "Invalid Args"); player.SetMute(m is "1" or "true"); break;
                case "GetMute": Put("CurrentMute", p.Muted ? "1" : "0"); break;
                case "ListPresets": Put("CurrentPresetNameList", "FactoryDefaults"); break;
                case "SelectPreset": if (Get("PresetName") != "FactoryDefaults") throw new UpnpException(701, "Invalid Name"); player.SetVolume(70); player.SetMute(false); break;
            }
        }
        else
        {
            switch (name)
            {
                case "GetProtocolInfo": Put("Source", "", "Sink", UpnpProtocol.ProtocolInfo); break;
                case "GetCurrentConnectionIDs": Put("ConnectionIDs", "0"); break;
                case "GetCurrentConnectionInfo":
                    if (Get("ConnectionID") != "0") throw new UpnpException(706, "Invalid connection reference");
                    Put("RcsID", "0", "AVTransportID", "0", "ProtocolInfo", "", "PeerConnectionManager", "", "PeerConnectionID", "-1", "Direction", "Input", "Status", "OK"); break;
            }
        }
        return UpnpProtocol.Response(service, name, output);
    }
}
