namespace CueMarkerExtractorApp;

/// <summary>Display-ready row for the chapter list (Avalonia's compiled
/// bindings need a concrete type, not an anonymous one).</summary>
public sealed class ChapterRow
{
    public int Index { get; init; }
    public string Title { get; init; } = "";
    public string Timecode { get; init; } = "";
}
