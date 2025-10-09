namespace DJIMissionInstaller.UnitTests.UI;

public class ConnectionSwitchingViewModelTests
{
  #region Methods

  [WpfFact]
  public async Task Initializes_With_Preference_And_Falls_Back_When_Adb_Fails()
  {
    EnsureApp();

    // Arrange: configuration prefers ADB
    var cfg = new ConfigurationService();
    cfg.UseAdbByDefault = true; // persisted preference

    // A wrapper ops + switcher that we can drive
    var adbOps = new Mock<IDeviceOperations>();
    var mtpOps = new Mock<IDeviceOperations>();

    // ADB throws on initialize to trigger fallback
    adbOps.Setup(d => d.InitializeAsync()).ThrowsAsync(new InvalidOperationException("adb.exe not found"));

    mtpOps.Setup(d => d.InitializeAsync()).Returns(Task.CompletedTask);
    mtpOps.Setup(d => d.GetDevices()).Returns(Enumerable.Empty<IDeviceInfo>());

    var current  = DeviceConnectionType.Adb;
    var switcher = new Mock<IDeviceOperationsSwitcher>();
    switcher.SetupGet(s => s.CurrentType).Returns(() => current);
    switcher.Setup(s => s.SwitchToAsync(It.IsAny<DeviceConnectionType>(), It.IsAny<bool>()))
            .Returns<DeviceConnectionType, bool>(async (t, init) =>
            {
              current = t;
              if (init)
              {
                if (t == DeviceConnectionType.Adb) await adbOps.Object.InitializeAsync();
                else await mtpOps.Object.InitializeAsync();
              }
            });

    // façade that delegates depending on current mode (just for this test)
    var opsFacade = new Mock<IDeviceOperations>();
    opsFacade.Setup(d => d.InitializeAsync()).Returns(async () =>
    {
      if (current == DeviceConnectionType.Adb) await adbOps.Object.InitializeAsync();
      else await mtpOps.Object.InitializeAsync();
    });
    opsFacade.Setup(d => d.GetDevices()).Returns(() =>
    {
      return current == DeviceConnectionType.Adb
        ? Enumerable.Empty<IDeviceInfo>()
        : mtpOps.Object.GetDevices();
    });

    var fs = new Mock<IFileSystemService>();
    fs.Setup(f => f.WatchKmzFolder());
    fs.Setup(f => f.GetKmzFiles()).Returns(new List<FileInfo>());

    var dlg  = new Mock<IDialogService>();
    var sort = new FileSortingService();

    var vm = new MainViewModel(opsFacade.Object, fs.Object, dlg.Object, sort,
                               new EsriMapScreenshotService(), new ImageService(),
                               new KmzReader(), new MissionTransferService(), cfg, new FolderPickerService(), switcher.Object);

    // Act
    await vm.InitializeAsync();

    // Assert: VM fell back to MTP and persisted preference updated
    vm.IsMtpSelected.Should().BeTrue();
    cfg.UseAdbByDefault.Should().BeFalse();
    dlg.Verify(d => d.ShowErrorAsync(It.Is<string>(t => t.Contains("ADB unavailable")), It.IsAny<string>()), Times.Once);
  }

  [WpfFact]
  public async Task Changing_RadioButton_Switches_Provider_And_Refreshes_Devices()
  {
    EnsureApp();

    var cfg = new ConfigurationService();
    cfg.UseAdbByDefault = false;

    var switcher = new Mock<IDeviceOperationsSwitcher>();
    switcher.SetupGet(s => s.CurrentType).Returns(DeviceConnectionType.Mtp);
    switcher.Setup(s => s.SwitchToAsync(DeviceConnectionType.Adb, true)).Returns(Task.CompletedTask);

    var ops = new Mock<IDeviceOperations>();
    ops.Setup(d => d.InitializeAsync()).Returns(Task.CompletedTask);
    ops.Setup(d => d.GetDevices()).Returns(Enumerable.Empty<IDeviceInfo>());

    var fs = new Mock<IFileSystemService>();
    fs.Setup(f => f.WatchKmzFolder());
    fs.Setup(f => f.GetKmzFiles()).Returns(new List<FileInfo>());

    var dlg  = new Mock<IDialogService>();
    var sort = new FileSortingService();

    var vm = new MainViewModel(ops.Object, fs.Object, dlg.Object, sort,
                               new EsriMapScreenshotService(), new ImageService(),
                               new KmzReader(), new MissionTransferService(), cfg, new FolderPickerService(), switcher.Object);

    await vm.InitializeAsync();

    // Flip to ADB via bound property
    vm.IsAdbSelected = true;

    switcher.Verify(s => s.SwitchToAsync(DeviceConnectionType.Adb, true), Times.AtLeastOnce);
  }

  private static void EnsureApp()
  {
    if (System.Windows.Application.Current == null)
      _ = new System.Windows.Application();
  }

  #endregion
}
