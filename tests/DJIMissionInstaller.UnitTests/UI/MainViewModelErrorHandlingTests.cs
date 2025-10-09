namespace DJIMissionInstaller.UnitTests.UI;

using CommunityToolkit.Mvvm.Input;

public class MainViewModelErrorHandlingTests
{
  #region Methods

  [WpfFact]
  public async Task InitializeAsync_When_DeviceOps_Throws_Shows_Init_Error()
  {
    EnsureApp();

    var deviceOps = new Mock<IDeviceOperations>();
    deviceOps.Setup(d => d.InitializeAsync()).ThrowsAsync(new InvalidOperationException("ADB failed to start"));
    deviceOps.SetupGet(d => d.CurrentDeviceInfo).Returns((IDeviceInfo?)null);

    var fs = new Mock<IFileSystemService>();
    fs.Setup(f => f.WatchKmzFolder());
    fs.Setup(f => f.GetKmzFiles()).Returns(new List<FileInfo>());

    var dlg  = new Mock<IDialogService>();
    var sort = new FileSortingService();

    var vm = new MainViewModel(deviceOps.Object, fs.Object, dlg.Object, sort);

    await vm.InitializeAsync();

    dlg.Verify(d => d.ShowErrorAsync("Failed to initialize device operations",
                                     It.Is<string>(s => s.Contains("ADB failed to start"))),
               Times.Once);
  }


  [WpfFact]
  public async Task RefreshDevices_When_GetDevices_Throws_Shows_Error()
  {
    EnsureApp();

    var deviceOps = new Mock<IDeviceOperations>();
    deviceOps.Setup(d => d.InitializeAsync()).Returns(Task.CompletedTask);
    deviceOps.Setup(d => d.GetDevices()).Throws(new IOException("Device enumeration failed"));
    deviceOps.SetupGet(d => d.CurrentDeviceInfo).Returns((IDeviceInfo?)null);

    var fs = new Mock<IFileSystemService>();
    fs.Setup(f => f.WatchKmzFolder());
    fs.Setup(f => f.GetKmzFiles()).Returns(new List<FileInfo>());

    var dlg  = new Mock<IDialogService>();
    var sort = new FileSortingService();

    var vm = new MainViewModel(deviceOps.Object, fs.Object, dlg.Object, sort);

    await vm.InitializeAsync();

    await ((IAsyncRelayCommand)vm.RefreshDevicesCommand).ExecuteAsync(null);

    dlg.Verify(d => d.ShowErrorAsync("Failed to load devices",
                                     It.Is<string>(s => s.Contains("Device enumeration failed"))),
               Times.Once);
  }


  [WpfFact]
  public async Task Selecting_Device_When_Connect_Throws_Shows_Retrieve_Error()
  {
    EnsureApp();

    var deviceOps = new Mock<IDeviceOperations>();
    deviceOps.SetupGet(d => d.IsConnected).Returns(false);
    deviceOps.Setup(d => d.InitializeAsync()).Returns(Task.CompletedTask);
    deviceOps.Setup(d => d.GetDevices()).Returns(new List<IDeviceInfo>());
    deviceOps.SetupGet(d => d.CurrentDeviceInfo).Returns((IDeviceInfo?)null);

    var fs = new Mock<IFileSystemService>();
    fs.Setup(f => f.WatchKmzFolder());
    fs.Setup(f => f.GetKmzFiles()).Returns(new List<FileInfo>());

    var dlg  = new Mock<IDialogService>();
    var sort = new FileSortingService();

    var vm = new MainViewModel(deviceOps.Object, fs.Object, dlg.Object, sort);

    await vm.InitializeAsync();

    var dev = new MtpDeviceInfo("dev1", "/storage/emulated/0", "Demo");

    deviceOps.Setup(d => d.Connect(dev)).Throws(new InvalidOperationException("connect failure"));

    vm.SelectedDevice = dev;

    // allow async pipeline to surface dialog
    await Task.Delay(150);

    dlg.Verify(d => d.ShowErrorAsync("Failed to retrieve files",
                                     It.Is<string>(s => s.Contains("connect failure"))),
               Times.Once);
  }

  #region Helpers

  private static void EnsureApp()
  {
    if (System.Windows.Application.Current == null)
      _ = new System.Windows.Application();
  }

  #endregion

  #endregion
}
