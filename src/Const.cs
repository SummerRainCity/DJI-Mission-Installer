namespace DJI_Mission_Installer;

using System.IO;

public static class Const
{
  #region Constants & Statics

  public const string WaypointFolder        = @"Android\data\dji.go.v5\files\waypoint";
  public const string WaypointPreviewFolder = @"Android\data\dji.go.v5\files\waypoint\map_preview";
  public const string TempFolderName        = "DJI_Mission_Installer";

  public static string TempPath => Path.Combine(Path.GetTempPath(), TempFolderName);

  public const int DefaultPreviewWidth  = 800;
  public const int DefaultPreviewHeight = 480;
  public const int DefaultZoomLevel     = 15;

  #endregion
}
