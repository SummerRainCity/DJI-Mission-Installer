namespace DJI_Mission_Installer.Services.Interfaces;

/// <summary>
///   Abstraction to read geographic information from a DJI KMZ mission package.
///   Implementations are expected to parse both KML and DJI WPML (.wpml) members inside the KMZ
///   ZIP container.
/// </summary>
public interface IKmzReader
{
  /// <summary>
  ///   Attempts to compute a representative center for the mission contained in the
  ///   specified KMZ file by aggregating all coordinate tuples found.
  /// </summary>
  /// <param name="kmzPath">Full path to the .kmz file on disk.</param>
  /// <param name="latitude">Latitude in decimal degrees on success.</param>
  /// <param name="longitude">Longitude in decimal degrees on success.</param>
  /// <returns>
  ///   True if at least one valid coordinate tuple was found and a center could be computed;
  ///   otherwise false.
  /// </returns>
  bool TryGetCenter(string kmzPath, out double latitude, out double longitude);
}
