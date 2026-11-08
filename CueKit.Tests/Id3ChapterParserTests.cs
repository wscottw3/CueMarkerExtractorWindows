using CueKit;
using static CueKit.Tests.Id3Fixtures;

namespace CueKit.Tests;

public class Id3ChapterParserTests
{
    [Fact]
    public void ParsesChaptersInCtocOrder()
    {
        // Build chapters out of order on disk; CTOC should re-sort them.
        var chap2 = MakeChapFrame("chp1", 60_000, 120_000, "Second");
        var chap1 = MakeChapFrame("chp0", 0, 60_000, "First");
        var toc = MakeCtocFrame("toc", "chp0", "chp1");

        var tag = MakeId3V3Tag(toc, chap2, chap1);

        var chapters = Id3ChapterParser.ParseChapters(tag);

        Assert.Equal(new[] { "chp0", "chp1" }, chapters.Select(c => c.ElementId));
        Assert.Equal(new[] { "First", "Second" }, chapters.Select(c => c.Title));
        Assert.Equal(0u, chapters[0].StartMs);
        Assert.Equal(60_000u, chapters[1].StartMs);
    }

    [Fact]
    public void FallsBackToChronologicalOrderWithoutCtoc()
    {
        var chapB = MakeChapFrame("b", 30_000, 60_000, "B");
        var chapA = MakeChapFrame("a", 0, 30_000, "A");

        var tag = MakeId3V3Tag(chapB, chapA);

        var chapters = Id3ChapterParser.ParseChapters(tag);

        Assert.Equal(new[] { "A", "B" }, chapters.Select(c => c.Title));
    }

    [Fact]
    public void ThrowsWhenNoId3Tag()
    {
        byte[] data = { 0x00, 0x01, 0x02, 0x03 };
        var ex = Assert.Throws<Id3ParseException>(() => Id3ChapterParser.ParseChapters(data));
        Assert.Equal(Id3ParseException.Kind.NotId3, ex.ErrorKind);
    }

    [Fact]
    public void ThrowsWhenNoChapters()
    {
        var tag = MakeId3V3Tag();
        var ex = Assert.Throws<Id3ParseException>(() => Id3ChapterParser.ParseChapters(tag));
        Assert.Equal(Id3ParseException.Kind.NoChaptersFound, ex.ErrorKind);
    }

    [Fact]
    public void ChapterWithoutTitleFallsBackToNull()
    {
        var chap = MakeChapFrame("chp0", 1_000, 2_000, null);
        var tag = MakeId3V3Tag(chap);

        var chapters = Id3ChapterParser.ParseChapters(tag);

        Assert.Single(chapters);
        Assert.Null(chapters[0].Title);
    }
}
