namespace DJI_Mission_Installer.Services;

using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;
using Interfaces;

/// <summary>
///   Production-ready KMZ reader that understands DJI Waypoints 3.0 packages. It opens the
///   KMZ as a ZIP archive and parses every member with extension ".kml" or ".wpml". Coordinates
///   are extracted from any &lt;coordinates&gt; element, ignoring XML namespaces, following the
///   KML order: longitude,latitude[,altitude].
/// </summary>
public sealed class KmzReader : IKmzReader
{
  #region Methods Impl

  /// <inheritdoc />
  public bool TryGetCenter(string kmzPath, out double latitude, out double longitude)
  {
    latitude  = 0;
    longitude = 0;

    // Validate input up front to avoid surprises later.
    if (string.IsNullOrWhiteSpace(kmzPath) || !File.Exists(kmzPath))
      return false;

    // Accumulators for arithmetic mean (robust enough for tightly clustered missions).
    double sumLat = 0;
    double sumLon = 0;
    long   count  = 0;

    try
    {
      using var fs   = File.OpenRead(kmzPath);
      using var zip  = new ZipArchive(fs, ZipArchiveMode.Read, false);
      var       entz = zip.Entries;

      foreach (var entry in entz)
      {
        // We only care about textual KML-like members. DJI missions often ship
        // both "template.kml" and "waylines.wpml" (sometimes under subfolders).
        if (!entry.FullName.EndsWith(".kml", StringComparison.OrdinalIgnoreCase) &&
            !entry.FullName.EndsWith(".wpml", StringComparison.OrdinalIgnoreCase))
          continue;

        // Parse XML safely. If any member is malformed we skip it and keep going.
        using var es  = entry.Open();
        var       doc = LoadXml(es);
        if (doc is null)
          continue;

        foreach (var coord in ExtractCoordinates(doc))
        {
          sumLat += coord.lat;
          sumLon += coord.lon;
          count++;
        }
      }
    }
    catch
    {
      // I/O or ZIP format issues -> treat as "not found"
      return false;
    }

    if (count == 0)
      return false;

    latitude  = sumLat / count;
    longitude = sumLon / count;

    return true;
  }

  #endregion

  #region Methods

  /// <summary>Loads an XML document from a stream without throwing for common recoverable errors.</summary>
  private static XDocument? LoadXml(Stream stream)
  {
    try
    {
      // Let XDocument infer encoding and ignore namespaces downstream by using LocalName.
      return XDocument.Load(stream, LoadOptions.None);
    }
    catch
    {
      return null;
    }
  }

  /// <summary>
  ///   Enumerates all lon/lat tuples found in &lt;coordinates&gt; elements across the
  ///   document. Parsing follows the KML convention: "lon,lat[,alt]". Whitespace-delimited lists
  ///   and newlines are handled; culture is invariant.
  /// </summary>
  private static IEnumerable<(double lat, double lon)> ExtractCoordinates(XDocument doc)
  {
    var inv = CultureInfo.InvariantCulture;

    // Select ANY element whose local name is "coordinates", regardless of namespace.
    var coordinateBlocks = doc
                           .Descendants()
                           .Where(e => string.Equals(e.Name.LocalName, "coordinates", StringComparison.OrdinalIgnoreCase))
                           .Select(e => e.Value.Trim())
                           .Where(v => v.Length > 0);

    foreach (var block in coordinateBlocks)
    {
      // The content can be one tuple or a list of tuples separated by whitespace.
      var tokens = block
        .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

      foreach (var token in tokens)
      {
        var parts = token.Split(',', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
          continue;

        if (double.TryParse(parts[0], NumberStyles.Float, inv, out var lon) &&
            double.TryParse(parts[1], NumberStyles.Float, inv, out var lat))
          yield return (lat, lon);
      }
    }
  }

  #endregion
}
