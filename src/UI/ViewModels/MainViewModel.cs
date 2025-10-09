namespace DJI_Mission_Installer.UI.ViewModels;

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Devices;
using Devices.DeviceInfo;
using Devices.Operations;
using DJI_Mission_Installer.Models;
using Extensions;
using Models;
using Services;
using Services.Interfaces;

public class MainViewModel : ViewModelBase
{
  #region Properties & Fields - Non-Public

  private readonly IFileSystemService        _fileSystemService;
  private readonly IDialogService            _dialogService;
  private readonly IMapScreenshotService     _mapScreenshotService;
  private readonly IImageService             _imageService;
  private readonly IKmzReader                _kmzReader;
  private readonly IMissionTransferService   _missionTransferService;
  private readonly IConfigurationService     _configurationService;
  private readonly IFolderPickerService      _folderPickerService;
  private readonly IDeviceOperationsSwitcher _deviceSwitcher;

  private readonly IDeviceOperations    _deviceOperations;
  private          DeviceConnectionType _selectedConnection = DeviceConnectionType.Adb;
  private          IDeviceInfo?         _selectedDevice;
  private          bool                 _isLoading;
  private          string               _kmzSourceFolder = string.Empty;

  #endregion

  #region Constructors

  // Backward-compatible ctor used by existing tests; wires defaults
  public MainViewModel(
    IDeviceOperations   deviceOperations,
    IFileSystemService  fileSystemService,
    IDialogService      dialogService,
    IFileSortingService sortingService)
    : this(deviceOperations,
           fileSystemService,
           dialogService,
           sortingService,
           new EsriMapScreenshotService(),
           new ImageService(),
           new KmzReader(),
           new MissionTransferService(),
           new ConfigurationService(),
           new FolderPickerService(),
           new SwitchableDeviceOperations(DeviceConnectionType.Adb)) { }

  // Full DI-friendly ctor
  public MainViewModel(
    IDeviceOperations         deviceOperations,
    IFileSystemService        fileSystemService,
    IDialogService            dialogService,
    IFileSortingService       sortingService,
    IMapScreenshotService     mapScreenshotService,
    IImageService             imageService,
    IKmzReader                kmzReader,
    IMissionTransferService   missionTransferService,
    IConfigurationService     configurationService,
    IFolderPickerService      folderPickerService,
    IDeviceOperationsSwitcher deviceSwitcher)
  {
    _deviceOperations       = deviceOperations;
    _fileSystemService      = fileSystemService;
    _dialogService          = dialogService;
    _mapScreenshotService   = mapScreenshotService;
    _imageService           = imageService;
    _kmzReader              = kmzReader;
    _missionTransferService = missionTransferService;
    _configurationService   = configurationService;
    _folderPickerService    = folderPickerService;
    _deviceSwitcher         = deviceSwitcher;

    KmzFiles      = new FileListViewModel("KMZ Files", sortingService);
    WaypointFiles = new FileListViewModel("Device Waypoints", sortingService);

    RefreshDevicesCommand  = new AsyncRelayCommand(RefreshDevicesAsync);
    TransferFileCommand    = new AsyncRelayCommand(TransferFileAsync, CanTransferFile);
    BrowseKmzFolderCommand = new RelayCommand(BrowseKmzFolder);

    KmzFiles.SelectedItemChanged      += (_, _) => TransferFileCommand.NotifyCanExecuteChanged();
    WaypointFiles.SelectedItemChanged += (_, _) => TransferFileCommand.NotifyCanExecuteChanged();

    _fileSystemService.WatchKmzFolder();
    _fileSystemService.KmzFilesChanged += OnKmzFilesChanged;

    // Initialize the UI's notion of the KMZ source folder from the service/config.
    KmzSourceFolder = _fileSystemService.CurrentFolder;
    // Initialize connection selection from persisted preference.
    SelectedConnection = _configurationService.UseAdbByDefault ? DeviceConnectionType.Adb : DeviceConnectionType.Mtp;
    LoadKmzFiles();
  }

  #endregion

  #region Properties & Fields - Public

  public FileListViewModel                 KmzFiles               { get; }
  public FileListViewModel                 WaypointFiles          { get; }
  public ObservableCollection<IDeviceInfo> AvailableDevices       { get; } = [];
  public IAsyncRelayCommand                RefreshDevicesCommand  { get; }
  public IAsyncRelayCommand                TransferFileCommand    { get; }
  public IRelayCommand                     BrowseKmzFolderCommand { get; }

  // Bound to radio buttons in the UI
  public DeviceConnectionType SelectedConnection
  {
    get => _selectedConnection;
    set
    {
      if (SetProperty(ref _selectedConnection, value))
      {
        // Persist preference immediately
        _configurationService.UseAdbByDefault = value == DeviceConnectionType.Adb;
        _configurationService.Save();
        // Switch provider and refresh devices
        _ = SwitchProviderAndRefreshAsync(value);
      }
    }
  }

  // Simple booleans for XAML binding convenience
  public bool IsAdbSelected
  {
    get => SelectedConnection == DeviceConnectionType.Adb;
    set
    {
      if (value) SelectedConnection = DeviceConnectionType.Adb;
      OnPropertyChanged();
    }
  }

  public bool IsMtpSelected
  {
    get => SelectedConnection == DeviceConnectionType.Mtp;
    set
    {
      if (value) SelectedConnection = DeviceConnectionType.Mtp;
      OnPropertyChanged();
    }
  }

  public bool IsLoading
  {
    get => _isLoading;
    set => SetProperty(ref _isLoading, value);
  }

  public string KmzSourceFolder
  {
    get => _kmzSourceFolder;
    private set => SetProperty(ref _kmzSourceFolder, value);
  }

  public IDeviceInfo? SelectedDevice
  {
    get => _selectedDevice;
    set
    {
      // Determine whether the actual value changed.
      var changed = _selectedDevice != value;

      if (changed)
      {
        _selectedDevice = value;
        OnPropertyChanged();
      }

      // Always refresh, even if the same device instance is set again.
      // This allows callers (and tests) to force a reload by reassigning the same value.
      _ = OnDeviceChangedAsync();
    }
  }

  #endregion

  #region Methods Impl

  protected override void OnPropertyChanged([CallerMemberName] string? propertyName = null)
  {
    base.OnPropertyChanged(propertyName);

    // If the selected device changes, update the transfer command's can-execute state
    if (propertyName == nameof(SelectedDevice))
      ((AsyncRelayCommand)TransferFileCommand).NotifyCanExecuteChanged();
  }

  #endregion

  #region Methods

  public async Task InitializeAsync()
  {
    try
    {
      IsLoading = true;
      // First try the preferred provider
      await _deviceSwitcher.SwitchToAsync(SelectedConnection, true);
      await RefreshDevicesAsync();
    }
    catch (Exception ex)
    {
      // If preferred was ADB, fall back to MTP automatically.
      if (SelectedConnection == DeviceConnectionType.Adb)
      {
        await _dialogService.ShowInfoAsync(
          "ADB unavailable",
          $"ADB initialization failed: {ex.Message}\n\nFalling back to MTP. You can change this later from the radio buttons.");

        try
        {
          SelectedConnection = DeviceConnectionType.Mtp; // persists + switches + refreshes
        }
        catch (Exception inner)
        {
          await _dialogService.ShowErrorAsync("MTP initialization failed", inner.Message);
        }
      }
      else
      {
        await _dialogService.ShowErrorAsync("Failed to initialize device operations", ex.Message);
      }
    }
    finally
    {
      IsLoading = false;
    }
  }

  private async Task RefreshDevicesAsync()
  {
    IsLoading = true;
    try
    {
      var devices = await Task.Run(() => _deviceOperations.GetDevices());
      AvailableDevices.Clear();
      foreach (var device in devices)
        AvailableDevices.Add(device);
    }
    catch (Exception ex)
    {
      await _dialogService.ShowErrorAsync($"{SelectedConnection} failed to load devices", ex.Message);
    }
    finally
    {
      IsLoading = false;
    }
  }

  private async Task OnDeviceChangedAsync()
  {
    try
    {
      if (SelectedDevice == null)
      {
        WaypointFiles.Items.Clear();
        return;
      }

      IsLoading = true;
      try
      {
        await Task.Run(() =>
        {
          _deviceOperations.Connect(SelectedDevice);

          // Use the device info that the operations layer resolved (after any fallback),
          // falling back to the originally selected info if not provided.
          var effectiveDevice = _deviceOperations.CurrentDeviceInfo ?? SelectedDevice;

          var waypoints = GetDeviceWaypoints(effectiveDevice);

          foreach (var waypoint in waypoints)
            try
            {
              var imageUri = waypoint.GetLocalImageUri();

              if (imageUri != null && !File.Exists(imageUri.AbsolutePath))
              {
                // Only try to pull the preview if it actually exists on the device.
                var deviceImagePath = waypoint.GetDeviceImagePath(effectiveDevice);

                if (_deviceOperations.FileExists(deviceImagePath))
                  _deviceOperations.DownloadFile(deviceImagePath, imageUri.AbsolutePath);
              }
            }
            catch (Exception ex)
            {
              // Do not fail the whole device load due to one broken slot/preview; log and continue.
              Debug.WriteLine($"[MTP] Skipping preview download for slot {waypoint.Id}: {ex.Message}");
            }

          SafeUiInvoke(() => { WaypointFiles.UpdateItems(waypoints.Select(w => new FileListItem(w))); });
        });
      }
      catch (Exception ex)
      {
        await _dialogService.ShowErrorAsync("Failed to retrieve files", ex.Message);
      }
      finally
      {
        IsLoading = false;
      }
    }
    catch (Exception e)
    {
      Debug.WriteLine(e);
    }
  }

  private List<WaypointFileInfo> GetDeviceWaypoints(IDeviceInfo device)
  {
    WaypointFileInfo CreateWaypointFileInfo(string id)
    {
      var kmzPath        = _deviceOperations.NormalizePath(Path.Combine(device.StoragePath, Const.WaypointFolder, id, $"{id}.kmz"));
      var imagePath      = _deviceOperations.NormalizePath(Path.Combine(device.StoragePath, Const.WaypointPreviewFolder, id, $"{id}.jpg"));
      var deviceFileInfo = _deviceOperations.GetFileInfo(kmzPath);

      return new WaypointFileInfo(id, kmzPath, imagePath, deviceFileInfo?.Size ?? 0,
                                  deviceFileInfo?.LastModified ?? DateTime.MinValue);
    }

    // Coalesce null to an empty sequence to avoid NullReferenceException when mocks
    // or implementations return default(IEnumerable<string>) for "no directories".
    var directories = _deviceOperations
      .GetDirectories(
        Path.Combine(device.StoragePath, Const.WaypointFolder),
        "*",
        SearchOption.TopDirectoryOnly);

    return directories
           .Select(Path.GetFileName)
           .Where(dir => !string.IsNullOrWhiteSpace(dir) && Guid.TryParse(dir, out _))
           .Select(id => CreateWaypointFileInfo(id!))
           .ToList();
  }

  private async Task TransferFileAsync()
  {
    if (!CanTransferFile())
      return;

    var kmzFile      = (KmzFile)KmzFiles.SelectedItem!.FileInfo;
    var waypointFile = (WaypointFileInfo)WaypointFiles.SelectedItem!.FileInfo;

    try
    {
      await Task.Run(async () =>
      {
        // Dependencies are passed at call-time so tests that swap private fields still take effect.
        await _missionTransferService.TransferAsync(
          _deviceOperations,
          _mapScreenshotService,
          _imageService,
          _kmzReader,
          kmzFile,
          waypointFile,
          Const.DefaultZoomLevel,
          Const.DefaultPreviewWidth,
          Const.DefaultPreviewHeight,
          CancellationToken.None);
      });

      await _dialogService.ShowInfoAsync("Success", $"Successfully transferred {kmzFile.Name} to the device.");

      // Refresh the waypoint files list after successful transfer
      await OnDeviceChangedAsync();
    }
    catch (Exception ex)
    {
      // Extra diagnostics surfaced to user and Debug window
      var hrHex = ex is System.Runtime.InteropServices.COMException cex
        ? $"0x{cex.HResult:X8}"
        : "n/a";

      var deviceName = SelectedDevice?.DisplayName ?? _deviceOperations.CurrentDeviceInfo?.DisplayName ?? "(no device)";
      var pathsInfo  = $"KMZ='{kmzFile.FullPath}' → DeviceKMZ='{waypointFile.DeviceKmzPath}', Preview='{waypointFile.DeviceImagePath}'";

      Debug.WriteLine(
        $"[TransferError] Device={deviceName} HR={hrHex} Type={ex.GetType().Name} Message={ex.Message} Paths={pathsInfo}\n{ex}");

      await _dialogService.ShowErrorAsync(
        "Failed to transfer file",
        $"Device: {deviceName}\n" +
        $"Error: {ex.GetType().Name}\n" +
        $"HRESULT: {hrHex}\n" +
        $"Message: {ex.Message}\n\n" +
        $"{pathsInfo}");
    }
  }

  private bool CanTransferFile() =>
    _deviceOperations.IsConnected &&
    SelectedDevice != null &&
    KmzFiles.SelectedItem?.FileInfo is KmzFile &&
    WaypointFiles.SelectedItem?.FileInfo is WaypointFileInfo;

  private void OnKmzFilesChanged(object? sender, EventArgs e)
  {
    LoadKmzFiles();
  }

  private void LoadKmzFiles()
  {
    var files = _fileSystemService.GetKmzFiles()
                                  .Select(f => new FileListItem(new KmzFile(f)))
                                  .ToList();

    // Keep the displayed folder text in sync with the service
    KmzSourceFolder = _fileSystemService.CurrentFolder;

    SafeUiInvoke(() => { KmzFiles.UpdateItems(files); });
  }

  private void BrowseKmzFolder()
  {
    try
    {
      var current = string.IsNullOrWhiteSpace(KmzSourceFolder)
        ? _configurationService.KmzSourceFolder
        : KmzSourceFolder;

      var chosen = _folderPickerService.PickFolder(current);
      if (string.IsNullOrWhiteSpace(chosen))
        return;

      // Update config, persist, and retarget file-watcher + list.
      _configurationService.KmzSourceFolder = chosen;
      _configurationService.Save(); // Update appSettings on disk. See MS docs.  (Saves and RefreshSection).

      _fileSystemService.ChangeKmzFolder(chosen);
      KmzSourceFolder = chosen;
      LoadKmzFiles();
    }
    catch (Exception ex)
    {
      _ = _dialogService.ShowErrorAsync("Folder Selection Failed", ex.Message);
    }
  }

  private async Task SwitchProviderAndRefreshAsync(DeviceConnectionType type)
  {
    try
    {
      IsLoading = true;
      await _deviceSwitcher.SwitchToAsync(type, true);
      await RefreshDevicesAsync();
    }
    catch (Exception ex)
    {
      await _dialogService.ShowErrorAsync($"Failed to switch to {type}", ex.Message);
    }
    finally
    {
      IsLoading = false;
    }
  }

  /// <summary>
  ///   Safely executes <paramref name="action" /> on the UI thread if a Dispatcher is
  ///   available. If already on the UI thread, runs inline. If the Dispatcher is shutting down or
  ///   missing (common in unit tests), executes inline to avoid TaskCanceledException.
  /// </summary>
  private static void SafeUiInvoke(Action action)
  {
    var dispatcher = Application.Current?.Dispatcher;

    if (dispatcher == null)
    {
      action();
      return;
    }

    if (dispatcher.CheckAccess())
    {
      action();
      return;
    }

    if (dispatcher is { HasShutdownStarted: false, HasShutdownFinished: false })
    {
      dispatcher.Invoke(action);
      return;
    }

    // Dispatcher shutting down: best-effort inline execution for graceful teardown.
    action();
  }

  #endregion
}
