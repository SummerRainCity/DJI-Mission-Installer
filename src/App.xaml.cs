namespace DJI_Mission_Installer;

using System.Diagnostics;
using System.IO;
using System.Windows;
using Devices.Operations;
using Microsoft.Extensions.DependencyInjection;
using Services;
using Services.Interfaces;
using UI;
using UI.ViewModels;

public partial class App : Application
{
  #region Properties & Fields - Non-Public

  private readonly IConfigurationService _configurationService;

  #endregion

  #region Constructors

  public App()
  {
    _configurationService        =  new ConfigurationService();
    DispatcherUnhandledException += App_DispatcherUnhandledException;
  }

  #endregion

  #region Methods Impl

  protected override void OnStartup(StartupEventArgs e)
  {
    try
    {
      base.OnStartup(e);

      // Create temp folder if it doesn't exist
      if (!Directory.Exists(Const.TempPath))
        Directory.CreateDirectory(Const.TempPath);

      // Create KMZ source folder if it doesn't exist
      if (!Directory.Exists(_configurationService.KmzSourceFolder))
        Directory.CreateDirectory(_configurationService.KmzSourceFolder);

      // Resolve everything via DI
      var host       = new AppHost();
      var services   = host.Services;
      var viewModel  = services.GetRequiredService<MainViewModel>();
      var deviceOps  = services.GetRequiredService<IDeviceOperations>();
      var cfgService = services.GetRequiredService<IConfigurationService>();

      var mainWindow = new MainWindow(viewModel, deviceOps, cfgService);
      mainWindow.Show();
    }
    catch (Exception ex)
    {
      Debug.WriteLine($"Startup error: {ex.Message}\n\nStack trace:\n{ex.StackTrace}");
      Shutdown(-1);
    }
  }

  #endregion

  #region Methods

  private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
  {
    Debug.WriteLine($"An error occurred: {e.Exception.Message}\n\nStack trace:\n{e.Exception.StackTrace}");
    e.Handled = true;
  }

  #endregion
}
