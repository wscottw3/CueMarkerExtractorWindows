using CueKit;

namespace CueKit.Tests;

public class CueSheetGeneratorTests
{
    [Fact]
    public void TimecodeConversion()
    {
        Assert.Equal("00:00:00", CueSheetGenerator.Timecode(0));
        Assert.Equal("00:01:00", CueSheetGenerator.Timecode(1_000));
        Assert.Equal("01:01:38", CueSheetGenerator.Timecode(61_500)); // 0.5s ≈ 37.5 frames → 38
        Assert.Equal("62:03:00", CueSheetGenerator.Timecode(3_723_000));
    }

    [Fact]
    public void GeneratesWellFormedCueSheet()
    {
        var chapters = new List<Chapter>
        {
            new("a", 0, 30_000, "Intro"),
            new("b", 30_000, 90_000, "Verse One"),
        };

        string cue = CueSheetGenerator.Generate("episode.mp3", chapters);

        string expected =
            "FILE \"episode.mp3\" MP3\n" +
            "  TRACK 01 AUDIO\n" +
            "    TITLE \"Intro\"\n" +
            "    INDEX 01 00:00:00\n" +
            "  TRACK 02 AUDIO\n" +
            "    TITLE \"Verse One\"\n" +
            "    INDEX 01 00:30:00\n";

        Assert.Equal(expected, cue);
    }

    [Fact]
    public void MissingTitleFallsBackToTrackNumber()
    {
        var chapters = new List<Chapter> { new("a", 0, 1_000, null) };
        string cue = CueSheetGenerator.Generate("x.mp3", chapters);
        Assert.Contains("TITLE \"Track 01\"", cue);
    }

    [Fact]
    public void QuotesInTitleAreEscaped()
    {
        var chapters = new List<Chapter> { new("a", 0, 1_000, "The \"Big\" One") };
        string cue = CueSheetGenerator.Generate("x.mp3", chapters);
        Assert.Contains("TITLE \"The 'Big' One\"", cue);
    }
}
