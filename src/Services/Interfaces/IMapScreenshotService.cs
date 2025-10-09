namespace DJI_Mission_Installer.Services.Interfaces;

public interface IMapScreenshotService
{
  Task<string> SaveMapScreenshotAsync(double latitude,
                                      double longitude,
                                      int    zoomLevel,
                                      string outputPath,
                                      int    width  = 640,
                                      int    height = 640,
                                      CancellationToken ct = default);
}
