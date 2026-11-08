# Cue Marker Extractor (Windows)

Translates Cue Marker descriptions in Adobe Audition to an identically titled .cue file for use with StationPlaylist Studio.

Windows counterpart to the Mac app — drag in an MP3
with embedded chapter/cue markers, get a standard `.cue` sheet out. Same
underlying parsing logic (ported from Swift to C#), packaged as a native
Windows desktop app via [Avalonia UI](https://avaloniaui.net/).

## Install

Download from the repository's **Releases** page:

- `CueMarkerExtractor-Setup-<version>.exe` — installer (per-user by default,
  no admin rights needed; adds a Start menu entry and uninstaller).
- `CueMarkerExtractor-Portable-<version>.zip` — just the `.exe`, no install.

The builds aren't code-signed, so Windows SmartScreen will likely warn on
first run: click **More info → Run anyway**.

## Releasing (maintainers)

Pushing a version tag builds everything on a GitHub Windows runner and
publishes a Release with the installer and portable zip attached:

```bash
git tag v1.0.0
git push origin v1.0.0
```

The workflow ([.github/workflows/release.yml](.github/workflows/release.yml))
runs the tests, publishes the self-contained exe, compiles
[installer/CueMarkerExtractor.iss](installer/CueMarkerExtractor.iss) with
Inno Setup, then creates the release. It can also be run manually from the
Actions tab to build the artifacts without publishing.

## What it reads

MP3 has no single standard "cue point" format, so this recognizes two
unrelated ones and prefers whichever is present:

1. **ID3v2 chapters** — `CHAP` frames, ordered by an optional `CTOC`
   table-of-contents frame. Used by podcast chapter tools, `ffmpeg
   -map_metadata`, etc.
2. **Adobe Audition markers** — an XMP packet embedded in a `PRIV` frame
   (owner `"XMP"`). This is what Audition actually writes when you save
   markers into an MP3. "CD Track Markers" (`trackType="Track"`) are
   preferred when present; otherwise generic "CuePoint Markers"
   (`trackType="Cue"`) are used. "Subclip" in/out-point markers are ranges,
   not track splits, and are ignored.

## Project layout

- `CueKit/` — parsing/generation logic (`Id3ChapterParser`,
  `XmpMarkerParser`, `CueSheetGenerator`), plain C#/.NET with no UI or
  Windows-specific dependencies. This is a line-for-line port of the Mac
  app's Swift `CueKit`.
- `CueKit.Tests/` — xUnit tests covering the same cases as the Mac version
  (CTOC ordering, chronological fallback, XMP marker extraction, Track-vs-Cue
  priority, malformed/missing-tag errors) plus one regression test for a
  real-world quirk (a stray trailing NUL byte after Adobe's `<?xpacket
  end?>` that .NET's XML parser rejects but Swift's tolerates).
- `CueMarkerExtractorApp/` — the Avalonia drag-and-drop desktop app.
- `build_windows_exe.sh` — cross-publishes a self-contained, single-file
  `win-x64` executable (bundles the .NET runtime, so end users don't need
  .NET installed).

## Build & run

Requires the [.NET SDK](https://dotnet.microsoft.com/download) (10.0+).

```bash
dotnet test CueKit.Tests/CueKit.Tests.csproj   # run the unit tests
dotnet run --project CueMarkerExtractorApp     # launch locally (any OS with Avalonia support)
```

To produce the distributable Windows executable — this can be run from
macOS/Linux too, since .NET can cross-target Windows without needing to run
on it:

```bash
./build_windows_exe.sh
```

This produces `publish/win-x64/CueMarkerExtractor.exe` — a single ~100MB
file. Copy it to a Windows machine and double-click; no installer or .NET
runtime needed.

## Using the app

1. Drag an MP3 onto the dashed drop zone (or launch the `.exe` with an MP3
   path as an argument / via "Open With").
2. Detected chapters appear in a list with their timecodes.
3. Click **Export CUE File…** and choose where to save. The `.cue`
   references the original MP3 by filename (`FILE "yourfile.mp3" MP3`), so
   keep the `.cue` next to the MP3.

If the MP3 has no ID3v2 tag, or no recognizable markers, the app explains
that instead of silently producing an empty file.

## Notes on parity with the Mac version

The parsing logic was ported directly from the Swift `CueKit` and verified
against the same two real-world files (an `ffmpeg`-generated CHAP file and
an actual Adobe Audition trivia-show MP3) to confirm identical output. One
difference surfaced during that verification: .NET's `XDocument` is stricter
about invalid XML than Swift's `XMLParser`, and rejected a stray NUL byte
some real Audition files leave after the closing `<?xpacket end="w"?>`
processing instruction — fixed in `XmpMarkerParser` by stripping embedded
NUL characters before parsing (see `XmpMarkerParserTests.TolerantOfTrailingNulByte`).

## License

Copyright © 2026 Scott Westerman. Licensed under
[Creative Commons Attribution 4.0 International (CC BY 4.0)](LICENSE) — you
may use, modify, and redistribute this project, including commercially, as
long as you give appropriate credit.

The app bundles third-party libraries (such as Avalonia, MIT-licensed) that
remain under their own licenses.
