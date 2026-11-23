using System.Xml.Linq;

namespace LANPUT.Core;

public static class UpnpProtocol
{
    public const string Av = "urn:schemas-upnp-org:service:AVTransport:1";
    public const string Rc = "urn:schemas-upnp-org:service:RenderingControl:1";
    public const string Cm = "urn:schemas-upnp-org:service:ConnectionManager:1";
    public const string DeviceType = "urn:schemas-upnp-org:device:MediaRenderer:1";
    public static readonly string[] Services = [Av, Rc, Cm];
    public const string ProtocolInfo = "http-get:*:video/mp4:*,http-get:*:video/mpeg:*,http-get:*:video/x-matroska:*,http-get:*:application/vnd.apple.mpegurl:*,http-get:*:application/x-mpegURL:*,http-get:*:audio/mpeg:*";
    public static string Key(string service) => service.Split(':')[3];
    public static string Description(string name, Guid id, Uri origin)
    {
        XNamespace d = "urn:schemas-upnp-org:device-1-0";
        return new XDocument(new XElement(d + "root", new XElement(d + "specVersion", new XElement(d + "major", 1), new XElement(d + "minor", 0)),
            new XElement(d + "URLBase", origin.ToString()), new XElement(d + "device",
                new XElement(d + "deviceType", DeviceType), new XElement(d + "friendlyName", name),
                new XElement(d + "manufacturer", "LANPUT Community"), new XElement(d + "modelName", ReceiverIdentity.Model),
                new XElement(d + "modelNumber", ReceiverIdentity.Version), new XElement(d + "UDN", $"uuid:{id}"),
                new XElement(d + "serviceList", Services.Select(s => new XElement(d + "service",
                    new XElement(d + "serviceType", s), new XElement(d + "serviceId", $"urn:upnp-org:serviceId:{Key(s)}"),
                    new XElement(d + "SCPDURL", $"/{Key(s)}/scpd.xml"), new XElement(d + "controlURL", $"/{Key(s)}/control"),
                    new XElement(d + "eventSubURL", $"/{Key(s)}/event"))))))).ToString();
    }
    public static string Response(string service, string action, IEnumerable<KeyValuePair<string, string>> values)
    {
        XNamespace soap = "http://schemas.xmlsoap.org/soap/envelope/";
        return new XDocument(new XElement(soap + "Envelope", new XAttribute(XNamespace.Xmlns + "s", soap),
            new XAttribute(soap + "encodingStyle", "http://schemas.xmlsoap.org/soap/encoding/"),
            new XElement(soap + "Body", new XElement(XName.Get(action + "Response", service),
                values.Select(v => new XElement(v.Key, v.Value)))))).ToString();
    }
    public static string Fault(int code, string description)
    {
        XNamespace soap = "http://schemas.xmlsoap.org/soap/envelope/";
        XNamespace control = "urn:schemas-upnp-org:control-1-0";
        return new XDocument(new XElement(soap + "Envelope", new XAttribute(XNamespace.Xmlns + "s", soap),
            new XElement(soap + "Body", new XElement(soap + "Fault", new XElement("faultcode", "s:Client"),
                new XElement("faultstring", "UPnPError"), new XElement("detail", new XElement(control + "UPnPError",
                    new XElement(control + "errorCode", code), new XElement(control + "errorDescription", description))))))).ToString();
    }
    public static string EventBody(string service, PlaybackSnapshot p)
    {
        XNamespace e = "urn:schemas-upnp-org:event-1-0";
        XElement property;
        if (service == Cm)
            return new XDocument(new XElement(e + "propertyset", new XAttribute(XNamespace.Xmlns + "e", e),
                new XElement(e + "property", new XElement("SourceProtocolInfo", "")),
                new XElement(e + "property", new XElement("SinkProtocolInfo", ProtocolInfo)),
                new XElement(e + "property", new XElement("CurrentConnectionIDs", "0")))).ToString();
        else
        {
            XNamespace a = service == Av ? "urn:schemas-upnp-org:metadata-1-0/AVT/" : "urn:schemas-upnp-org:metadata-1-0/RCS/";
            XElement Val(string name, object value, bool channel = false) => new(a + name,
                channel ? new XAttribute("channel", "Master") : null, new XAttribute("val", value));
            var instance = new XElement(a + "InstanceID", new XAttribute("val", "0"));
            if (service == Av) instance.Add(Val("TransportState", p.State), Val("TransportStatus", p.Status),
                Val("CurrentTransportActions", p.Uri.Length == 0 ? "" : "Play,Pause,Stop,Seek"),
                Val("AVTransportURI", p.Uri), Val("AVTransportURIMetaData", p.Metadata), Val("CurrentTrackURI", p.Uri),
                Val("CurrentTrackMetaData", p.Metadata), Val("CurrentTrackDuration", XmlSafe.Time(p.DurationMs)),
                Val("NumberOfTracks", p.Uri.Length == 0 ? 0 : 1), Val("CurrentTrack", p.Uri.Length == 0 ? 0 : 1));
            else instance.Add(Val("Volume", p.Volume, true), Val("Mute", p.Muted ? 1 : 0, true));
            var change = new XElement(a + "Event", instance);
            property = new XElement(e + "property", new XElement("LastChange", change.ToString(SaveOptions.DisableFormatting)));
        }
        return new XDocument(new XElement(e + "propertyset", new XAttribute(XNamespace.Xmlns + "e", e), property)).ToString();
    }
}
