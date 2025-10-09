namespace DJI_Mission_Installer.Devices.Operations;

/// <summary>
///   Allows switching the underlying device operations provider (ADB or MTP) at runtime,
///   while keeping a stable <see cref="IDeviceOperations" /> reference for consumers.
/// </summary>
public interface IDeviceOperationsSwitcher
{
  /// <summary>The currently active provider type.</summary>
  DeviceConnectionType CurrentType { get; }

  /// <summary>
  ///   Switch to the specified provider. Implementations must create a new underlying
  ///   provider, dispose the old one (if any), and make the new provider active.
  /// </summary>
  /// <param name="type">Target connection type.</param>
  /// <param name="initialize">
  ///   If true, also call <c>InitializeAsync()</c> on the new provider
  ///   before returning.
  /// </param>
  Task SwitchToAsync(DeviceConnectionType type, bool initialize = false);
}
