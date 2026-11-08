namespace CueKit;

/// <summary>
/// A single chapter/cue marker extracted from an MP3's ID3v2 tag.
/// </summary>
public sealed class Chapter
{
    /// <summary>The raw element ID from the CHAP frame (e.g. "chp0"). Not user-facing.</summary>
    public string ElementId { get; }

    /// <summary>Start time, in milliseconds from the start of the file.</summary>
    public uint StartMs { get; }

    /// <summary>End time, in milliseconds from the start of the file.</summary>
    public uint EndMs { get; }

    /// <summary>Human-readable title, if one was embedded alongside the marker.</summary>
    public string? Title { get; }

    public Chapter(string elementId, uint startMs, uint endMs, string? title)
    {
        ElementId = elementId;
        StartMs = startMs;
        EndMs = endMs;
        Title = title;
    }
}
