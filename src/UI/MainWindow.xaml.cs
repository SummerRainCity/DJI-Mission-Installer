namespace DJI_Mission_Installer.UI;

using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Devices.Operations;
using Services.Interfaces;
using ViewModels;

public partial class MainWindow : Window
{
  #region Properties & Fields - Non-Public

  private readonly IDeviceOperations     _deviceOperations;
  private readonly IConfigurationService _configurationService;

  #endregion

  #region Constructors

  public MainWindow(MainViewModel viewModel, IDeviceOperations deviceOperations, IConfigurationService configurationService)
  {
    InitializeComponent();

    _deviceOperations     = deviceOperations;
    _configurationService = configurationService;

    DataContext = viewModel;

    // Initialize the default selection before we kick off initialization.
    Loaded += async (_, _) =>
    {
      try
      {
        await viewModel.InitializeAsync();
      }
      catch (Exception ex)
      {
        Debug.WriteLine($"Initialization error: {ex.Message}");
      }
    };

    // Handle application shutdown
    Application.Current.Exit += Current_Exit;
    Closing                  += MainWindow_Closing;
  }

  #endregion

  #region Methods

  private void Current_Exit(object sender, ExitEventArgs e)
  {
    Cleanup();
  }

  private void MainWindow_Closing(object? sender, CancelEventArgs e)
  {
    Cleanup();
  }

  private void Cleanup()
  {
    try
    {
      // Dispose only our device operations. AdbDeviceOperations will stop the server only if it started it.
      if (_deviceOperations is IDisposable disposable)
        disposable.Dispose();
    }
    catch (Exception ex)
    {
      Debug.WriteLine($"Error during cleanup: {ex.Message}");
    }
  }

  private void ListView_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
  {
    var scrollViewer = GetDescendantByType<ScrollViewer>((ListView)sender);
    if (scrollViewer != null)
    {
      // Adjust this value to control scroll speed
      double scrollAmount = -e.Delta * 0.8; // Makes scrolling slower
      scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + scrollAmount);
      e.Handled = true;
    }
  }

  private static T? GetDescendantByType<T>(DependencyObject? element) where T : class
  {
    if (element == null) return null;

    if (element is T)
      return element as T;

    for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
    {
      var child  = VisualTreeHelper.GetChild(element, i);
      var result = GetDescendantByType<T>(child);

      if (result != null)
        return result;
    }

    return null;
  }

  #endregion
}
