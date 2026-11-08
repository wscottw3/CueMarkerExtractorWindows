using System.Globalization;
using System.Text;

namespace CueKit;

/// <summary>
/// Builds a standard `.cue` sheet from a list of chapters, using the classic
/// CD-style `MM:SS:FF` timecode (75 frames per second).
/// </summary>
public static class CueSheetGenerator
{
    public static string Generate(string mp3Filename, IReadOnlyList<Chapter> chapters)
    {
        var sb = new StringBuilder();
        sb.Append("FILE \"").Append(EscapeQuotes(mp3Filename)).Append("\" MP3\n");

        for (int index = 0; index < chapters.Count; index++)
        {
            var chapter = chapters[index];
            string trackNumber = (index + 1).ToString("D2", CultureInfo.InvariantCulture);
            string title = !string.IsNullOrEmpty(chapter.Title) ? chapter.Title! : $"Track {trackNumber}";

            sb.Append("  TRACK ").Append(trackNumber).Append(" AUDIO\n");
            sb.Append("    TITLE \"").Append(EscapeQuotes(title)).Append("\"\n");
            sb.Append("    INDEX 01 ").Append(Timecode(chapter.StartMs)).Append('\n');
        }

        return sb.ToString();
    }

    /// Converts milliseconds to `MM:SS:FF` where FF is 1/75th-of-a-second frames,
    /// the resolution used by the CUE sheet standard.
    public static string Timecode(uint ms)
    {
        long totalMs = ms;
        long minutes = totalMs / 60_000;
        long seconds = (totalMs / 1_000) % 60;
        long frames = Math.Min(74, (long)Math.Round((totalMs % 1_000) * 75.0 / 1_000.0));
        return string.Format(CultureInfo.InvariantCulture, "{0:D2}:{1:D2}:{2:D2}", minutes, seconds, frames);
    }

    private static string EscapeQuotes(string s) => s.Replace("\"", "'");
}
