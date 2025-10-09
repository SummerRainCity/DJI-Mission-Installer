namespace DJIMissionInstaller.UnitTests.Services;

using System.IO.Compression;
using Fixtures;

public class KmzReaderTests
{
  #region Methods

  [Fact]
  public void TryGetCenter_DjiMapper_Kmz_Returns_Center_Within_Bounds_And_Correct_Hemisphere()
  {
    // Arrange
    var kmzPath = Path.Combine(AppContext.BaseDirectory, "Data", "DjiMapper.kmz");
    File.Exists(kmzPath).Should().BeTrue("test fixture must be deployed next to test binaries");

    var sut = new KmzReader();

    // Act
    var ok = sut.TryGetCenter(kmzPath, out var lat, out var lon);

    // Assert basic success and reasonable hemisphere sanity for our fixtures (Innlandet, Norway).
    ok.Should().BeTrue("DjiMapper.kmz contains dozens of waypoints");

    lat.Should().BeGreaterThan(60.9710);
    lat.Should().BeLessThan(60.9745);
    lon.Should().BeGreaterThan(10.7330);
    lon.Should().BeLessThan(10.7370);
  }


  [Fact]
  public void TryGetCenter_DjiFlightHub_Kmz_Returns_Center_Within_Bounds_And_Correct_Hemisphere()
  {
    // Arrange
    var kmzPath = Path.Combine(AppContext.BaseDirectory, "Data", "DjiFlightHub.kmz");
    File.Exists(kmzPath).Should().BeTrue("test fixture must be deployed next to test binaries");

    var sut = new KmzReader();

    // Act
    var ok = sut.TryGetCenter(kmzPath, out var lat, out var lon);

    // Assert
    ok.Should().BeTrue("DjiFlightHub.kmz contains multiple placemarks");

    // Coordinates in sample lie tightly clustered, also near Hamar.
    lat.Should().BeGreaterThan(60.9720);
    lat.Should().BeLessThan(60.9735);
    lon.Should().BeGreaterThan(10.7372);
    lon.Should().BeLessThan(10.7380);
  }


  [Fact]
  public void TryGetCenter_EmptyOrNoCoords_Returns_False()
  {
    // Arrange: create a temporary KMZ with a syntactically valid KML root but no coordinates.
    using var tmp     = new TempDir("KMZ_EMPTY");
    var       kmzPath = tmp.Combine("empty.kmz");

    using (var zip = ZipFile.Open(kmzPath, ZipArchiveMode.Create))
    {
      var       entry  = zip.CreateEntry("waylines.wpml");
      using var writer = new StreamWriter(entry.Open());
      writer.Write(
        """
        <?xml version="1.0" encoding="UTF-8"?>
        <kml xmlns="http://www.opengis.net/kml/2.2">
          <Document>
            <Placemark>
              <name>No coordinates here</name>
              <Point>
                <coordinates></coordinates>
              </Point>
            </Placemark>
          </Document>
        </kml>
        """);
    }

    var sut = new KmzReader();

    // Act
    var ok = sut.TryGetCenter(kmzPath, out var lat, out var lon);

    // Assert
    ok.Should().BeFalse("no usable lon,lat tuples exist in this KMZ");
    lat.Should().Be(0);
    lon.Should().Be(0);
  }

  #endregion
}
