namespace DJI_Mission_Installer.Services;

using System.IO;
using Devices.Operations;
using Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Models;

/// <summary>
///   Default mission transfer implementation. Ensures idempotent device state, generates a
///   deterministic preview image, and uploads both assets.
/// </summary>
public sealed class MissionTransferService(ILogger<MissionTransferService>? logger) : IMissionTransferService
{
  #region Properties & Fields - Non-Public

  private readonly ILogger<MissionTransferService> _logger = logger ?? NullLogger<MissionTransferService>.Instance;

  #endregion

  #region Constructors

  // Backward-compatible: parameterless stays valid
  public MissionTransferService()
    : this(NullLogger<MissionTransferService>.Instance) { }

  #endregion

  #region Methods Impl

  public async Task<MissionTransferOutcome> TransferAsync(
    IDeviceOperations     deviceOps,
    IMapScreenshotService mapService,
    IImageService         imageService,
    IKmzReader            kmzReader,
    KmzFile               sourceKmz,
    WaypointFileInfo      targetSlot,
    int                   zoomLevel,
    int                   previewWidth,
    int                   previewHeight,
    CancellationToken     ct = default)
  {
    ArgumentNullException.ThrowIfNull(deviceOps);
    ArgumentNullException.ThrowIfNull(mapService);
    ArgumentNullException.ThrowIfNull(imageService);
    ArgumentNullException.ThrowIfNull(kmzReader);
    ArgumentNullException.ThrowIfNull(sourceKmz);
    ArgumentNullException.ThrowIfNull(targetSlot);

    _logger.LogInformation("Starting mission transfer: kmz={Kmz}, slot={Slot}, zoom={Zoom}, size={W}x{H}",
                           sourceKmz.Name, targetSlot.Id, zoomLevel, previewWidth, previewHeight);

    ct.ThrowIfCancellationRequested();

    // 1) Validate device directory
    var directoryPath = deviceOps.NormalizePath(Path.GetDirectoryName(targetSlot.DeviceKmzPath)!);

    var dirExists = deviceOps.DirectoryExists(directoryPath);
    _logger.LogInformation("Target slot directory exists={Exists}: {Dir}", dirExists, directoryPath);

    if (!dirExists)
      throw new DirectoryNotFoundException("Selected waypoint slot no longer exists on the device.");

    // 2) Remove previous artifacts if present
    if (deviceOps.FileExists(targetSlot.DeviceKmzPath))
    {
      _logger.LogDebug("Deleting existing device KMZ: {Path}", targetSlot.DeviceKmzPath);
      deviceOps.DeleteFile(targetSlot.DeviceKmzPath);
    }

    if (deviceOps.FileExists(targetSlot.DeviceImagePath))
    {
      _logger.LogDebug("Deleting existing device preview: {Path}", targetSlot.DeviceImagePath);
      deviceOps.DeleteFile(targetSlot.DeviceImagePath);
    }

    ct.ThrowIfCancellationRequested();

    // 3) Upload new KMZ
    await using (var kmzStream = File.OpenRead(sourceKmz.FullPath))
    {
      _logger.LogInformation("Uploading KMZ to device: {Path}", targetSlot.DeviceKmzPath);
      deviceOps.UploadFile(kmzStream, targetSlot.DeviceKmzPath);
    }

    _logger.LogInformation("Uploaded KMZ to device path {Path}", targetSlot.DeviceKmzPath);

    // 4) Build preview image (temp map + overlay -> final)
    var tempMapPath    = Path.Combine(Const.TempPath, $"{targetSlot.Id}_map.jpg");
    var finalImagePath = Path.Combine(Const.TempPath, $"{targetSlot.Id}.jpg");
    Directory.CreateDirectory(Const.TempPath);

    try
    {
      ct.ThrowIfCancellationRequested();

      // Try to derive an approximate mission center from KMZ coordinates.
      if (kmzReader.TryGetCenter(sourceKmz.FullPath, out var lat, out var lon))
      {
        _logger.LogInformation("KMZ center resolved: {Lat},{Lon}", lat, lon);

        await mapService.SaveMapScreenshotAsync(
          lat, lon, zoomLevel, tempMapPath, previewWidth, previewHeight, ct);

        // Overlay mission metadata on the map tile.
        await imageService.ProcessImageAsync(
          tempMapPath, finalImagePath, sourceKmz.Name, targetSlot.LastModified, null);
      }
      else
      {
        _logger.LogWarning("KMZ center not found. Falling back to default preview.");
        await imageService.CreateDefaultImageAsync(
          finalImagePath, sourceKmz.Name, targetSlot.LastModified, previewWidth, previewHeight);
      }
    }
    catch (OperationCanceledException)
    {
      _logger.LogWarning("Mission transfer cancelled during preview generation.");
      throw;
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Preview generation failed; falling back to default overlay.");
      await imageService.CreateDefaultImageAsync(
        finalImagePath, sourceKmz.Name, targetSlot.LastModified, previewWidth, previewHeight);
    }
    finally
    {
      try
      {
        if (File.Exists(tempMapPath)) File.Delete(tempMapPath);
      }
      catch
      {
        /* best effort */
      }
    }

    ct.ThrowIfCancellationRequested();

    // 5) Upload preview to device
    await using (var img = File.OpenRead(finalImagePath))
    {
      var previewParent = deviceOps.NormalizePath(Path.GetDirectoryName(targetSlot.DeviceImagePath)!);
      _logger.LogInformation("Uploading preview image. Parent exists={Exists} Parent={Parent} Target={Target}",
                             deviceOps.DirectoryExists(previewParent), previewParent, targetSlot.DeviceImagePath);

      deviceOps.UploadFile(img, targetSlot.DeviceImagePath);
    }

    _logger.LogInformation("Preview uploaded to device path {Path}", targetSlot.DeviceImagePath);

    return new MissionTransferOutcome(finalImagePath, true);
  }

  #endregion
}
