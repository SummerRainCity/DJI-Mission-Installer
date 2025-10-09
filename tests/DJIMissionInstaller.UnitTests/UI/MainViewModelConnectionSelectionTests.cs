namespace DJIMissionInstaller.UnitTests.UI;

public class MainViewModelConnectionSelectionTests
{
  #region Methods

  [WpfFact]
  public async Task Initialize_When_Initial_DeviceOps_Ok_Allows_Refresh_And_Selection()
  {
    EnsureApp();

    var device = new MtpDeviceInfo("dev1", "/storage/emulated/0", "Demo");

    var deviceOps = new Mock<IDeviceOperations>();
    deviceOps.Setup(d => d.InitializeAsync()).Returns(Task.CompletedTask);
    deviceOps.Setup(d => d.GetDevices()).Returns(new List<IDeviceInfo> { device });

    var fs = new Mock<IFileSystemService>();
    fs.Setup(f => f.WatchKmzFolder());
    fs.Setup(f => f.GetKmzFiles()).Returns([]);

    var dlg  = new Mock<IDialogService>();
    var sort = new FileSortingService();

    var vm = new MainViewModel(deviceOps.Object, fs.Object, dlg.Object, sort);

    await vm.InitializeAsync();

    vm.AvailableDevices.Should().ContainSingle(d => d.DeviceId == device.DeviceId);

    await vm.RefreshDevicesCommand.ExecuteAsync(null);

    vm.SelectedDevice = device;

    deviceOps.Verify(d => d.Connect(device), Times.AtLeastOnce);
  }

  private static void EnsureApp()
  {
    if (System.Windows.Application.Current == null)
      _ = new System.Windows.Application();
  }

  #endregion
}
