using System.Text;

namespace CueKit;

/// <summary>
/// Parses chapter/cue markers out of an MP3's ID3v2 tag. Two unrelated
/// formats are recognized, since MP3 has no single standard for this:
///
/// 1. ID3v2 chapter frames — "CHAP" frames, ordered by an optional "CTOC"
///    table of contents frame. (id3.org ID3v2 Chapter Frame Addendum:
///    https://id3.org/id3v2-chapters-1.0)
/// 2. Adobe Audition-style markers — embedded as an XMP packet inside a
///    "PRIV" frame with owner identifier "XMP". Used when Audition (or
///    similar Adobe tools) save cue points/CD track markers into an MP3.
///
/// CHAP frames win when both are present; XMP markers are the fallback.
/// </summary>
public static class Id3ChapterParser
{
    public static List<Chapter> ParseChapters(byte[] data)
    {
        if (data.Length < 10) throw Id3ParseException.NotId3();
        if (data[0] != 0x49 || data[1] != 0x44 || data[2] != 0x33) // "ID3"
        {
            throw Id3ParseException.NotId3();
        }

        byte majorVersion = data[3];
        if (majorVersion != 3 && majorVersion != 4)
        {
            throw Id3ParseException.UnsupportedVersion(majorVersion);
        }

        byte flags = data[5];
        bool hasExtendedHeader = (flags & 0x40) != 0;
        long tagSize = Syncsafe(data[6], data[7], data[8], data[9]);

        long offset = 10;
        long tagEnd = Math.Min(offset + tagSize, data.LongLength);
        if (tagEnd < offset) throw Id3ParseException.TruncatedTag();

        if (hasExtendedHeader)
        {
            if (offset + 4 > data.LongLength) throw Id3ParseException.TruncatedTag();
            int o = (int)offset;
            if (majorVersion == 4)
            {
                offset += Syncsafe(data[o], data[o + 1], data[o + 2], data[o + 3]);
            }
            else
            {
                // In v2.3 the extended header size field doesn't include itself.
                offset += 4 + BigEndian(data[o], data[o + 1], data[o + 2], data[o + 3]);
            }
        }

        var chaptersById = new Dictionary<string, Chapter>();
        var appearanceOrder = new List<string>();
        List<string>? tocOrder = null;
        var xmpPayloads = new List<byte[]>();

        while (offset + 10 <= tagEnd)
        {
            int o = (int)offset;
            if (data[o] == 0 && data[o + 1] == 0 && data[o + 2] == 0 && data[o + 3] == 0)
            {
                break; // padding reached
            }

            string frameId = Encoding.ASCII.GetString(data, o, 4);
            long frameSize = majorVersion == 4
                ? Syncsafe(data[o + 4], data[o + 5], data[o + 6], data[o + 7])
                : BigEndian(data[o + 4], data[o + 5], data[o + 6], data[o + 7]);

            long frameDataStart = offset + 10;
            long frameDataEnd = frameDataStart + frameSize;
            if (frameSize < 0 || frameDataStart > tagEnd || frameDataEnd > tagEnd) break;

            var frameData = new byte[frameSize];
            Array.Copy(data, frameDataStart, frameData, 0, frameSize);

            switch (frameId)
            {
                case "CHAP":
                    var chapter = ParseChap(frameData, majorVersion);
                    if (chapter != null)
                    {
                        chaptersById[chapter.ElementId] = chapter;
                        appearanceOrder.Add(chapter.ElementId);
                    }
                    break;
                case "CTOC":
                    tocOrder ??= ParseCtocChildOrder(frameData);
                    break;
                case "PRIV":
                    var xmp = ParsePrivXmpPayload(frameData);
                    if (xmp != null) xmpPayloads.Add(xmp);
                    break;
            }

            offset = frameDataEnd;
        }

        if (chaptersById.Count > 0)
        {
            // Prefer the CTOC's declared order; append any orphaned chapters
            // (not referenced by the TOC) afterwards, sorted chronologically.
            List<string> ordered;
            if (tocOrder != null)
            {
                ordered = tocOrder.Where(chaptersById.ContainsKey).ToList();
                var known = new HashSet<string>(ordered);
                var leftovers = appearanceOrder
                    .Where(id => !known.Contains(id))
                    .OrderBy(id => chaptersById[id].StartMs)
                    .ToList();
                ordered.AddRange(leftovers);
            }
            else
            {
                ordered = appearanceOrder.OrderBy(id => chaptersById[id].StartMs).ToList();
            }
            return ordered.Select(id => chaptersById[id]).ToList();
        }

        var xmpChapters = ParseXmpChapters(xmpPayloads);
        if (xmpChapters is { Count: > 0 }) return xmpChapters;

        throw Id3ParseException.NoChaptersFound();
    }

    // MARK: - CHAP frame

    /// CHAP frame layout:
    /// Element ID (Latin-1, null-terminated) | Start time (4B) | End time (4B)
    /// | Start byte offset (4B) | End byte offset (4B) | optional sub-frames (e.g. TIT2)
    private static Chapter? ParseChap(byte[] data, byte majorVersion)
    {
        int nullIndex = Array.IndexOf(data, (byte)0);
        if (nullIndex < 0) return null;
        string elementId = Encoding.Latin1.GetString(data, 0, nullIndex);

        int offset = nullIndex + 1;
        if (offset + 16 > data.Length) return null;

        uint startMs = BigEndianU32(data, offset); offset += 4;
        uint endMs = BigEndianU32(data, offset); offset += 4;
        offset += 8; // start/end byte offsets — unused here

        string? title = ParseTitleFromSubFrames(data, offset, majorVersion);
        return new Chapter(elementId, startMs, endMs, title);
    }

    /// Scans a CHAP frame's trailing sub-frames for a TIT2 (title) frame.
    private static string? ParseTitleFromSubFrames(byte[] data, int start, byte majorVersion)
    {
        int offset = start;
        while (offset + 10 <= data.Length)
        {
            if (data[offset] == 0 && data[offset + 1] == 0 && data[offset + 2] == 0 && data[offset + 3] == 0) break;
            string subId = Encoding.ASCII.GetString(data, offset, 4);

            long subSize = majorVersion == 4
                ? Syncsafe(data[offset + 4], data[offset + 5], data[offset + 6], data[offset + 7])
                : BigEndian(data[offset + 4], data[offset + 5], data[offset + 6], data[offset + 7]);

            long subDataStart = offset + 10;
            long subDataEnd = subDataStart + subSize;
            if (subSize < 0 || subDataEnd > data.Length) break;

            if (subId == "TIT2")
            {
                var payload = new byte[subSize];
                Array.Copy(data, subDataStart, payload, 0, subSize);
                return DecodeTextFrame(payload);
            }

            offset = (int)subDataEnd;
        }
        return null;
    }

    // MARK: - CTOC frame

    /// CTOC frame layout:
    /// Element ID (Latin-1, null-terminated) | Flags (1B) | Entry count (1B)
    /// | Entry count × child Element ID (Latin-1, null-terminated) | optional sub-frames
    private static List<string>? ParseCtocChildOrder(byte[] data)
    {
        int nullIndex = Array.IndexOf(data, (byte)0);
        if (nullIndex < 0) return null;
        int offset = nullIndex + 1;
        if (offset + 2 > data.Length) return null;
        offset += 1; // flags
        int entryCount = data[offset]; offset += 1;

        var children = new List<string>();
        for (int i = 0; i < entryCount; i++)
        {
            if (offset >= data.Length) break;
            int terminator = Array.IndexOf(data, (byte)0, offset);
            if (terminator < 0) break;
            children.Add(Encoding.Latin1.GetString(data, offset, terminator - offset));
            offset = terminator + 1;
        }
        return children.Count == 0 ? null : children;
    }

    // MARK: - PRIV / XMP (Adobe Audition markers)

    /// PRIV frame layout: owner identifier (Latin-1, null-terminated) + raw binary data.
    private static byte[]? ParsePrivXmpPayload(byte[] data)
    {
        int nullIndex = Array.IndexOf(data, (byte)0);
        if (nullIndex < 0) return null;
        string owner = Encoding.Latin1.GetString(data, 0, nullIndex);
        if (owner != "XMP") return null;

        int start = nullIndex + 1;
        var payload = new byte[data.Length - start];
        Array.Copy(data, start, payload, 0, payload.Length);
        return payload;
    }

    private static List<Chapter>? ParseXmpChapters(List<byte[]> payloads)
    {
        var allMarkers = payloads.SelectMany(XmpMarkerParser.ParseMarkers).ToList();
        if (allMarkers.Count == 0) return null;

        // Explicit "CD Track Markers" (trackType Track) are the closer match
        // to CUE-sheet track splits; only fall back to generic cue points
        // when no track markers exist, to avoid mixing the two.
        var chosen = allMarkers.Any(m => m.TrackType == "Track")
            ? allMarkers.Where(m => m.TrackType == "Track").ToList()
            : allMarkers.Where(m => m.TrackType == "Cue").ToList();

        var sorted = chosen.OrderBy(m => m.StartMs).ToList();
        return sorted
            .Select((m, index) => new Chapter($"xmp-{index}", m.StartMs, m.StartMs, m.Name))
            .ToList();
    }

    // MARK: - Text frame decoding

    /// ID3v2 text frames start with a 1-byte text-encoding indicator:
    /// 0 = ISO-8859-1, 1 = UTF-16 with BOM, 2 = UTF-16BE (no BOM, v2.4 only),
    /// 3 = UTF-8 (v2.4 only).
    private static string? DecodeTextFrame(byte[] bytes)
    {
        if (bytes.Length == 0) return null;
        byte encodingByte = bytes[0];
        var payload = bytes.Skip(1).ToArray();

        static string Trim(string s) => s.TrimEnd('\0').Trim();

        return encodingByte switch
        {
            0 => Trim(Encoding.Latin1.GetString(payload)),
            1 => Trim(DecodeUtf16WithBom(payload)),
            2 => Trim(Encoding.BigEndianUnicode.GetString(payload)),
            3 => Trim(Encoding.UTF8.GetString(payload)),
            _ => Trim(Encoding.Latin1.GetString(payload)),
        };
    }

    private static string DecodeUtf16WithBom(byte[] payload)
    {
        if (payload.Length >= 2 && payload[0] == 0xFE && payload[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode.GetString(payload, 2, payload.Length - 2);
        }
        if (payload.Length >= 2 && payload[0] == 0xFF && payload[1] == 0xFE)
        {
            return Encoding.Unicode.GetString(payload, 2, payload.Length - 2);
        }
        return Encoding.Unicode.GetString(payload);
    }

    // MARK: - Byte helpers

    /// ID3v2 "syncsafe" integers use only the low 7 bits of each byte (the high
    /// bit is always 0) so the value can never be mistaken for a sync signal.
    private static long Syncsafe(byte b0, byte b1, byte b2, byte b3) =>
        ((long)(b0 & 0x7F) << 21) | ((long)(b1 & 0x7F) << 14) | ((long)(b2 & 0x7F) << 7) | (long)(b3 & 0x7F);

    private static long BigEndian(byte b0, byte b1, byte b2, byte b3) =>
        ((long)b0 << 24) | ((long)b1 << 16) | ((long)b2 << 8) | (long)b3;

    private static uint BigEndianU32(byte[] data, int offset) =>
        ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16)
        | ((uint)data[offset + 2] << 8) | data[offset + 3];
}
