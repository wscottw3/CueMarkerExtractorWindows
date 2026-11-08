namespace CueKit;

/// <summary>
/// Raised when an MP3's ID3v2 tag can't be read, or contains no recognizable
/// chapter/cue markers.
/// </summary>
public sealed class Id3ParseException : Exception
{
    public enum Kind
    {
        NotId3,
        UnsupportedVersion,
        NoChaptersFound,
        TruncatedTag
    }

    public Kind ErrorKind { get; }

    private Id3ParseException(Kind kind, string message) : base(message)
    {
        ErrorKind = kind;
    }

    public static Id3ParseException NotId3() => new(
        Kind.NotId3,
        "This file doesn't start with an ID3v2 tag, so there are no embedded markers to read.");

    public static Id3ParseException UnsupportedVersion(byte major) => new(
        Kind.UnsupportedVersion,
        $"This file uses ID3v2.{major}, which isn't supported (only ID3v2.3 and ID3v2.4 chapters are).");

    public static Id3ParseException NoChaptersFound() => new(
        Kind.NoChaptersFound,
        "No chapter or cue markers were found in this file's tag (checked ID3v2 CHAP frames and Adobe Audition-style XMP markers).");

    public static Id3ParseException TruncatedTag() => new(
        Kind.TruncatedTag,
        "This file's ID3 tag looks corrupted or truncated.");
}
