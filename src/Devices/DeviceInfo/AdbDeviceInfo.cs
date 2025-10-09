namespace DJI_Mission_Installer.Devices.DeviceInfo;

using AdvancedSharpAdbClient.Models;

public class AdbDeviceInfo : IDeviceInfo
{
  #region Constructors

  public AdbDeviceInfo(DeviceData deviceData, string storagePath)
  {
    DeviceId    = deviceData.Serial;
    StoragePath = storagePath;

    // Build a more user-friendly display name
    var deviceModel = CleanupDeviceModel(deviceData.Model);
    var storageType = DetermineStorageType(storagePath);

    DisplayName = $"{deviceModel} ({storageType})";
  }

  #endregion

  #region Properties Impl - Public

  public string DeviceId    { get; }
  public string StoragePath { get; }
  public string DisplayName { get; }

  #endregion

  #region Methods

  private string CleanupDeviceModel(string model)
  {
    // Remove common prefixes/suffixes and clean up the model name
    return model
           .Replace("SM_", "Samsung ") // Samsung model prefix
           .Replace("SM-", "Samsung ")
           .Replace("_", " ") // Replace underscores with spaces
           .Trim();
  }

  private string DetermineStorageType(string path)
  {
    // Determine a user-friendly storage description
    return path.ToLowerInvariant() switch
    {
      "/storage/emulated/0" => "Internal Storage",
      "/storage/self/primary" => "Internal Storage",
      "/sdcard" => "Internal Storage",
      var p when p.Contains("sdcard") => "SD Card",
      _ => "Storage"
    };
  }

  #endregion
}
