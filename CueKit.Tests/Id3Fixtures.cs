using System.Text;

namespace CueKit.Tests;

/// <summary>Builders for raw ID3v2.3 tag bytes, used across test files.</summary>
internal static class Id3Fixtures
{
    /// Builds a minimal valid ID3v2.3 tag containing the given already-encoded frames.
    public static byte[] MakeId3V3Tag(params byte[][] frames)
    {
        using var body = new MemoryStream();
        foreach (var frame in frames) body.Write(frame);

        using var tag = new MemoryStream();
        tag.Write(new byte[] { 0x49, 0x44, 0x33 }); // "ID3"
        tag.Write(new byte[] { 0x03, 0x00 });       // version 2.3.0
        tag.WriteByte(0x00);                        // flags
        tag.Write(SyncsafeBytes((int)body.Length));  // tag size
        tag.Write(body.ToArray());
        return tag.ToArray();
    }

    /// Encodes one full frame: 4-byte ID + 4-byte big-endian size + 2 flag bytes + payload.
    public static byte[] MakeFrame(string id, byte[] payload)
    {
        using var frame = new MemoryStream();
        frame.Write(Encoding.ASCII.GetBytes(id));
        frame.Write(BigEndianBytes(payload.Length));
        frame.Write(new byte[] { 0x00, 0x00 }); // flags
        frame.Write(payload);
        return frame.ToArray();
    }

    public static byte[] MakeChapFrame(string elementId, uint startMs, uint endMs, string? title)
    {
        using var payload = new MemoryStream();
        payload.Write(Encoding.ASCII.GetBytes(elementId));
        payload.WriteByte(0x00);
        payload.Write(BigEndianU32Bytes(startMs));
        payload.Write(BigEndianU32Bytes(endMs));
        payload.Write(BigEndianU32Bytes(0xFFFFFFFF)); // start byte offset (unused)
        payload.Write(BigEndianU32Bytes(0xFFFFFFFF)); // end byte offset (unused)

        if (title != null)
        {
            using var titlePayload = new MemoryStream();
            titlePayload.WriteByte(0x00); // ISO-8859-1 encoding byte
            titlePayload.Write(Encoding.ASCII.GetBytes(title));
            payload.Write(MakeFrame("TIT2", titlePayload.ToArray()));
        }

        return MakeFrame("CHAP", payload.ToArray());
    }

    public static byte[] MakeCtocFrame(string elementId, params string[] childIds)
    {
        using var payload = new MemoryStream();
        payload.Write(Encoding.ASCII.GetBytes(elementId));
        payload.WriteByte(0x00);
        payload.WriteByte(0x03); // flags: top-level + ordered
        payload.WriteByte((byte)childIds.Length);
        foreach (var child in childIds)
        {
            payload.Write(Encoding.ASCII.GetBytes(child));
            payload.WriteByte(0x00);
        }
        return MakeFrame("CTOC", payload.ToArray());
    }

    public static byte[] MakePrivFrame(string owner, byte[] payload)
    {
        using var framePayload = new MemoryStream();
        framePayload.Write(Encoding.ASCII.GetBytes(owner));
        framePayload.WriteByte(0x00);
        framePayload.Write(payload);
        return MakeFrame("PRIV", framePayload.ToArray());
    }

    private static byte[] SyncsafeBytes(int value) => new[]
    {
        (byte)((value >> 21) & 0x7F),
        (byte)((value >> 14) & 0x7F),
        (byte)((value >> 7) & 0x7F),
        (byte)(value & 0x7F),
    };

    private static byte[] BigEndianBytes(int value) => new[]
    {
        (byte)((value >> 24) & 0xFF),
        (byte)((value >> 16) & 0xFF),
        (byte)((value >> 8) & 0xFF),
        (byte)(value & 0xFF),
    };

    private static byte[] BigEndianU32Bytes(uint value) => new[]
    {
        (byte)((value >> 24) & 0xFF),
        (byte)((value >> 16) & 0xFF),
        (byte)((value >> 8) & 0xFF),
        (byte)(value & 0xFF),
    };
}
