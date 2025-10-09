namespace DJI_Mission_Installer;

using Devices;
using Devices.Operations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Services;
using Services.Interfaces;
using UI.ViewModels;

/// <summary>
///   Minimal composition root for WPF to centralize dependency wiring and logging. Keeps
///   constructors backward compatible to avoid test modifications.
/// </summary>
public sealed class AppHost
{
  #region Constructors

  public AppHost()
  {
    var services = new ServiceCollection();

    // Logging
    services.AddLogging(builder =>
    {
      builder.SetMinimumLevel(LogLevel.Information);
      builder.AddDebug();
    });

    // Core configuration + options
    services.AddSingleton<IConfigurationService, ConfigurationService>();

    // App services
    services.AddSingleton<IDialogService, DialogService>();
    services.AddSingleton<IFileSortingService, FileSortingService>();
    services.AddSingleton<IKmzReader, KmzReader>();
    services.AddSingleton<IMapScreenshotService, EsriMapScreenshotService>();
    services.AddSingleton<IImageService, ImageService>();
    services.AddSingleton<IFolderPickerService, FolderPickerService>();

    // File system service (needs configured folder)
    services.AddSingleton<IFileSystemService>(sp =>
                                                new FileSystemService(sp.GetRequiredService<IConfigurationService>().KmzSourceFolder));

    // Device operations: register a switchable façade so we can flip ADB/MTP at runtime.
    services.AddSingleton<SwitchableDeviceOperations>(sp =>
    {
      var cfg  = sp.GetRequiredService<IConfigurationService>();
      var type = cfg.UseAdbByDefault ? DeviceConnectionType.Adb : DeviceConnectionType.Mtp;
      return new SwitchableDeviceOperations(type);
    });

    // Bind both interfaces to the same instance.
    services.AddSingleton<IDeviceOperations>(sp => sp.GetRequiredService<SwitchableDeviceOperations>());
    services.AddSingleton<IDeviceOperationsSwitcher>(sp => sp.GetRequiredService<SwitchableDeviceOperations>());

    // Mission transfer orchestrator
    services.AddSingleton<IMissionTransferService, MissionTransferService>();

    // ViewModel with full DI ctor (backward compatible overload preserved in class)
    services.AddSingleton<MainViewModel>(sp =>
                                           new MainViewModel(
                                             sp.GetRequiredService<IDeviceOperations>(),
                                             sp.GetRequiredService<IFileSystemService>(),
                                             sp.GetRequiredService<IDialogService>(),
                                             sp.GetRequiredService<IFileSortingService>(),
                                             sp.GetRequiredService<IMapScreenshotService>(),
                                             sp.GetRequiredService<IImageService>(),
                                             sp.GetRequiredService<IKmzReader>(),
                                             sp.GetRequiredService<IMissionTransferService>(),
                                             sp.GetRequiredService<IConfigurationService>(),
                                             sp.GetRequiredService<IFolderPickerService>(),
                                             sp.GetRequiredService<IDeviceOperationsSwitcher>()));

    Services = services.BuildServiceProvider();
  }

  #endregion

  #region Properties & Fields - Public

  public IServiceProvider Services { get; }

  #endregion
}
