namespace DJIMissionInstaller.UnitTests.Services;

using System.IO.Compression;
using Fixtures;

public class KmzReaderCoordinateListParsingTests
{
  #region Methods

  [Fact]
  public void TryGetCenter_Parses_List_With_Altitudes_And_Whitespace()
  {
    using var tmp     = new TempDir("KMZ_COORDS");
    var       kmzPath = tmp.Combine("coords.kmz");

    // Create a synthetic KMZ containing a KML with mixed whitespace and altitude values.
    using (var zip = ZipFile.Open(kmzPath, ZipArchiveMode.Create))
    {
      var       entry = zip.CreateEntry("doc.kml");
      using var w     = new StreamWriter(entry.Open());
      w.Write(
        """
        <?xml version="1.0" encoding="UTF-8"?>
        <kml xmlns="http://www.opengis.net/kml/2.2">
          <Document>
            <Placemark>
              <LineString>
                <coordinates>
                  10.0000,60.0000,120
                  
                  11.0000,61.0000, 200
                  10.5000,60.5000
                </coordinates>
              </LineString>
            </Placemark>
          </Document>
        </kml>
        """);
    }

    var sut = new KmzReader();

    var ok = sut.TryGetCenter(kmzPath, out var lat, out var lon);

    ok.Should().BeTrue();
    lat.Should().BeApproximately((60.0000 + 61.0000 + 60.5000) / 3.0, 1e-6);
    lon.Should().BeApproximately((10.0000 + 11.0000 + 10.5000) / 3.0, 1e-6);
  }

  #endregion
}
