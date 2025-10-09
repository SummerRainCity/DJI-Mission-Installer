namespace DJI_Mission_Installer.Services.Interfaces;

using Devices.Operations;
using Models;

/// <summary>
///   Orchestrates the end-to-end mission transfer: device IO, map preview generation, and
///   image upload. Stateless, accepts required collaborators at call time so tests can inject
///   fakes without re-wiring DI.
/// </summary>
public interface IMissionTransferService
{
  Task<MissionTransferOutcome> TransferAsync(
    IDeviceOperations     deviceOps,
    IMapScreenshotService mapService,
    IImageService         imageService,
    IKmzReader            kmzReader,
    KmzFile               sourceKmz,
    WaypointFileInfo      targetSlot,
    int                   zoomLevel,
    int                   previewWidth,
    int                   previewHeight,
    CancellationToken     ct = default);
}
