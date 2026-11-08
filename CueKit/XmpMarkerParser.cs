using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace CueKit;

/// <summary>
/// A raw marker parsed out of an XMP `xmpDM:Tracks` packet, before it's
/// decided which track type wins.
/// </summary>
public sealed record XmpMarker(string TrackType, uint StartMs, string? Name);

/// <summary>
/// Parses Adobe Audition-style cue/track markers.
///
/// Audition (and other Adobe tools) don't use ID3 CHAP frames. Instead they
/// embed an XMP packet in a `PRIV` frame (owner identifier `"XMP"`) whose
/// `xmpDM:Tracks` section holds one or more marker tracks — typically
/// "CuePoint Markers" (trackType `Cue`) and/or "CD Track Markers" (trackType
/// `Track`). Each marker's `xmpDM:startTime` is a tick count at the track's
/// `xmpDM:frameRate` (e.g. `f44100` = 44100 ticks/sec for audio-sample-rate
/// timing), not milliseconds directly.
///
/// Reference: XMP Dynamic Media namespace in the XMP Specification Part 2.
/// </summary>
public static class XmpMarkerParser
{
    /// Track types that represent single-point markers we care about.
    /// ("InOut" subclip markers are ranges, not track-split points, so
    /// they're intentionally excluded.)
    private static readonly HashSet<string> RelevantTrackTypes = new() { "Cue", "Track" };

    public static List<XmpMarker> ParseMarkers(byte[] xmpData)
    {
        var markers = new List<XmpMarker>();

        string xml;
        try
        {
            xml = Encoding.UTF8.GetString(xmpData);
        }
        catch
        {
            return markers;
        }

        // XMP packets are sometimes padded with trailing NUL/whitespace bytes
        // after the closing "<?xpacket end=...?>" (unlike Swift's XMLParser,
        // .NET's XDocument rejects a raw NUL anywhere in the document).
        if (xml.IndexOf('\0') >= 0)
        {
            xml = xml.Replace("\0", string.Empty);
        }

        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml, LoadOptions.None);
        }
        catch
        {
            return markers;
        }

        if (doc.Root != null)
        {
            Walk(doc.Root, trackType: null, frameRate: null, markers);
        }

        return markers;
    }

    /// Elements are matched by local name only (ignoring namespace prefixes),
    /// and track context (trackType/frameRate) is inherited down the tree so
    /// a nested marker `Description` still knows which track it's inside.
    private static void Walk(XElement element, string? trackType, string? frameRate, List<XmpMarker> markers)
    {
        if (element.Name.LocalName == "Description")
        {
            trackType = GetAttribute(element, "trackType") ?? trackType;
            frameRate = GetAttribute(element, "frameRate") ?? frameRate;

            string? startTimeString = GetAttribute(element, "startTime");
            if (startTimeString != null
                && double.TryParse(startTimeString, NumberStyles.Float, CultureInfo.InvariantCulture, out double startTimeTicks)
                && trackType != null && RelevantTrackTypes.Contains(trackType)
                && TicksPerSecond(frameRate) is double rate && rate > 0)
            {
                uint startMs = (uint)Math.Round(startTimeTicks / rate * 1000);
                markers.Add(new XmpMarker(trackType, startMs, GetAttribute(element, "name")));
            }
        }

        foreach (var child in element.Elements())
        {
            Walk(child, trackType, frameRate, markers);
        }
    }

    private static string? GetAttribute(XElement element, string localName) =>
        element.Attributes().FirstOrDefault(a => a.Name.LocalName == localName)?.Value;

    private static double? TicksPerSecond(string? frameRate)
    {
        if (frameRate == null || !frameRate.StartsWith("f", StringComparison.Ordinal)) return null;
        string remainder = frameRate[1..];
        int i = 0;
        while (i < remainder.Length && (char.IsDigit(remainder[i]) || remainder[i] == '.')) i++;
        string numeric = remainder[..i];
        if (double.TryParse(numeric, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && value > 0)
        {
            return value;
        }
        return null;
    }
}
