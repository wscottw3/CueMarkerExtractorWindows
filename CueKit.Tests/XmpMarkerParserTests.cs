using System.Text;
using CueKit;
using static CueKit.Tests.Id3Fixtures;

namespace CueKit.Tests;

public class XmpMarkerParserTests
{
    [Fact]
    public void ParsesCuePointMarkers()
    {
        const string xmp = """
        <x:xmpmeta xmlns:x="adobe:ns:meta/">
         <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
          <rdf:Description rdf:about="" xmlns:xmpDM="http://ns.adobe.com/xmp/1.0/DynamicMedia/">
           <xmpDM:Tracks>
            <rdf:Bag>
             <rdf:li>
              <rdf:Description xmpDM:trackName="CuePoint Markers" xmpDM:trackType="Cue" xmpDM:frameRate="f44100">
               <xmpDM:markers>
                <rdf:Seq>
                 <rdf:li>
                  <rdf:Description xmpDM:startTime="436105" xmpDM:name="Joe Cocker &amp; Jennifer Warnes"/>
                 </rdf:li>
                 <rdf:li>
                  <rdf:Description xmpDM:startTime="882000" xmpDM:name="Second Song"/>
                 </rdf:li>
                </rdf:Seq>
               </xmpDM:markers>
              </rdf:Description>
             </rdf:li>
            </rdf:Bag>
           </xmpDM:Tracks>
          </rdf:Description>
         </rdf:RDF>
        </x:xmpmeta>
        """;

        var markers = XmpMarkerParser.ParseMarkers(Encoding.UTF8.GetBytes(xmp));

        Assert.Equal(2, markers.Count);
        Assert.Equal("Cue", markers[0].TrackType);
        Assert.Equal("Joe Cocker & Jennifer Warnes", markers[0].Name); // entity decoded
        Assert.Equal(9889u, markers[0].StartMs); // 436105 / 44100 * 1000, rounded
        Assert.Equal(20000u, markers[1].StartMs); // 882000 / 44100 * 1000
    }

    [Fact]
    public void TolerantOfTrailingNulByte()
    {
        // Real Adobe Audition files can have a stray NUL after "<?xpacket end=...?>".
        const string xmp = """
        <rdf:Description xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#" xmlns:xmpDM="http://ns.adobe.com/xmp/1.0/DynamicMedia/">
         <xmpDM:Tracks><rdf:Bag>
          <rdf:li>
           <rdf:Description xmpDM:trackType="Cue" xmpDM:frameRate="f44100">
            <xmpDM:markers><rdf:Seq><rdf:li>
             <rdf:Description xmpDM:startTime="44100" xmpDM:name="A Cue Point"/>
            </rdf:li></rdf:Seq></xmpDM:markers>
           </rdf:Description>
          </rdf:li>
         </rdf:Bag></xmpDM:Tracks>
        </rdf:Description>
        """;
        var bytes = Encoding.UTF8.GetBytes(xmp).Concat(new byte[] { 0x00 }).ToArray();

        var markers = XmpMarkerParser.ParseMarkers(bytes);

        Assert.Single(markers);
        Assert.Equal("A Cue Point", markers[0].Name);
    }

    [Fact]
    public void IgnoresSubclipInOutMarkers()
    {
        const string xmp = """
        <rdf:Description xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#" xmlns:xmpDM="http://ns.adobe.com/xmp/1.0/DynamicMedia/">
         <xmpDM:Tracks><rdf:Bag>
          <rdf:li>
           <rdf:Description xmpDM:trackType="InOut" xmpDM:frameRate="f44100">
            <xmpDM:markers><rdf:Seq><rdf:li>
             <rdf:Description xmpDM:startTime="1000" xmpDM:name="Selection"/>
            </rdf:li></rdf:Seq></xmpDM:markers>
           </rdf:Description>
          </rdf:li>
         </rdf:Bag></xmpDM:Tracks>
        </rdf:Description>
        """;

        var markers = XmpMarkerParser.ParseMarkers(Encoding.UTF8.GetBytes(xmp));
        Assert.Empty(markers);
    }

    [Fact]
    public void TrackMarkersTakePriorityOverCueMarkersInFullParse()
    {
        // When a file has both types, ID3ChapterParser should prefer the
        // explicit CD "Track" markers over generic "Cue" points.
        const string xmp = """
        <rdf:Description xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#" xmlns:xmpDM="http://ns.adobe.com/xmp/1.0/DynamicMedia/">
         <xmpDM:Tracks><rdf:Bag>
          <rdf:li>
           <rdf:Description xmpDM:trackType="Cue" xmpDM:frameRate="f44100">
            <xmpDM:markers><rdf:Seq><rdf:li>
             <rdf:Description xmpDM:startTime="44100" xmpDM:name="A Cue Point"/>
            </rdf:li></rdf:Seq></xmpDM:markers>
           </rdf:Description>
          </rdf:li>
          <rdf:li>
           <rdf:Description xmpDM:trackType="Track" xmpDM:frameRate="f44100">
            <xmpDM:markers><rdf:Seq>
             <rdf:li><rdf:Description xmpDM:startTime="0" xmpDM:name="Track One"/></rdf:li>
             <rdf:li><rdf:Description xmpDM:startTime="88200" xmpDM:name="Track Two"/></rdf:li>
            </rdf:Seq></xmpDM:markers>
           </rdf:Description>
          </rdf:li>
         </rdf:Bag></xmpDM:Tracks>
        </rdf:Description>
        """;

        var tag = MakeId3V3Tag(MakePrivFrame("XMP", Encoding.UTF8.GetBytes(xmp)));
        var chapters = Id3ChapterParser.ParseChapters(tag);

        Assert.Equal(new[] { "Track One", "Track Two" }, chapters.Select(c => c.Title));
    }

    [Fact]
    public void FullFileWithNoChapOrXmpThrows()
    {
        var tag = MakeId3V3Tag();
        var ex = Assert.Throws<Id3ParseException>(() => Id3ChapterParser.ParseChapters(tag));
        Assert.Equal(Id3ParseException.Kind.NoChaptersFound, ex.ErrorKind);
    }
}
