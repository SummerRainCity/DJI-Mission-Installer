namespace DJIMissionInstaller.UnitTests.UI;

public class MainViewModelStorageFallbackTests
{
  #region Methods

  [WpfFact]
  public async Task Selecting_Device_With_Stale_Storage_Alias_Falls_Back_And_Loads_Waypoints()
  {
    EnsureApp();

    // The device was previously saved with /storage/self/primary (now stale).
    var originallySelected = new MtpDeviceInfo("dev1", "/storage/self/primary", "Demo");

    // The device actually exposes /storage/emulated/0 at runtime.
    var effective = new MtpDeviceInfo("dev1", "/storage/emulated/0", "Demo");

    var deviceOps = new Mock<IDeviceOperations>();

    deviceOps.SetupGet(d => d.IsConnected).Returns(true);
    deviceOps.Setup(d => d.InitializeAsync()).Returns(Task.CompletedTask);

    // Connect should not throw; after connect, CurrentDeviceInfo returns the effective path.
    deviceOps.Setup(d => d.Connect(originallySelected));
    deviceOps.SetupGet(d => d.CurrentDeviceInfo).Returns(effective);

    deviceOps.Setup(d => d.NormalizePath(It.IsAny<string>()))
             .Returns<string>(s => s.Replace("\\", "/"));

    // Provide two slots that exist under the EFFECTIVE path.
    var guid1 = Guid.NewGuid().ToString("D");
    var guid2 = Guid.NewGuid().ToString("D");

    deviceOps.Setup(d => d.GetDirectories(
                      It.Is<string>(p => p.StartsWith($"{effective.StoragePath}/{Const.WaypointFolder.Replace("\\", "/")}")),
                      "*", SearchOption.TopDirectoryOnly))
             .Returns([
               $"{effective.StoragePath}/{Const.WaypointFolder.Replace("\\", "/")}/{guid1}",
               $"{effective.StoragePath}/{Const.WaypointFolder.Replace("\\", "/")}/{guid2}"
             ]);

    // File info and image downloads
    var kmz1 = $"{effective.StoragePath}/{Const.WaypointFolder.Replace("\\", "/")}/{guid1}/{guid1}.kmz";
    var kmz2 = $"{effective.StoragePath}/{Const.WaypointFolder.Replace("\\", "/")}/{guid2}/{guid2}.kmz";

    deviceOps.Setup(d => d.GetFileInfo(kmz1)).Returns(new DeviceFileInfo(kmz1, DateTime.UtcNow.AddMinutes(-10), 123));
    deviceOps.Setup(d => d.GetFileInfo(kmz2)).Returns(new DeviceFileInfo(kmz2, DateTime.UtcNow, 456));

    // Force preview download for both
    deviceOps.Setup(d => d.DownloadFile(It.Is<string>(s => s.EndsWith($"{guid1}.jpg")), It.IsAny<string>()));
    deviceOps.Setup(d => d.DownloadFile(It.Is<string>(s => s.EndsWith($"{guid2}.jpg")), It.IsAny<string>()));

    var fs = new Mock<IFileSystemService>();
    fs.Setup(f => f.WatchKmzFolder());
    fs.Setup(f => f.GetKmzFiles()).Returns(new List<FileInfo>());

    var dlg  = new Mock<IDialogService>();
    var sort = new FileSortingService();

    var vm = new MainViewModel(deviceOps.Object, fs.Object, dlg.Object, sort)
    {
      SelectedDevice = originallySelected
    };

    await SpinWaitAsync(() => vm.IsLoading == false, TimeSpan.FromSeconds(5));

    vm.WaypointFiles.Items.Should().HaveCount(2);
    dlg.Verify(d => d.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
  }

  [WpfFact]
  public async Task Selecting_Device_When_No_Storage_Available_Shows_Error()
  {
    EnsureApp();

    var selected = new MtpDeviceInfo("dev1", "/storage/self/primary", "Demo");

    var deviceOps = new Mock<IDeviceOperations>();
    deviceOps.SetupGet(d => d.IsConnected).Returns(false);
    deviceOps.Setup(d => d.InitializeAsync()).Returns(Task.CompletedTask);

    // Simulate operations layer failing to resolve any usable storage
    deviceOps.Setup(d => d.Connect(selected))
             .Throws(new InvalidOperationException("No accessible storage paths found on the device."));
    deviceOps.SetupGet(d => d.CurrentDeviceInfo).Returns((IDeviceInfo?)null);

    var fs = new Mock<IFileSystemService>();
    fs.Setup(f => f.WatchKmzFolder());
    fs.Setup(f => f.GetKmzFiles()).Returns(new List<FileInfo>());

    var dlg = new Mock<IDialogService>();

    // Allow async dialog to be invoked
    await Task.Delay(150);

    dlg.Verify(d => d.ShowErrorAsync("Failed to retrieve files",
                                     It.Is<string>(s => s.Contains("No accessible storage paths"))),
               Times.Once);
  }

  private static void EnsureApp()
  {
    if (System.Windows.Application.Current == null)
      _ = new System.Windows.Application();
  }

  private static async Task SpinWaitAsync(Func<bool> condition, TimeSpan timeout)
  {
    var start = DateTime.UtcNow;
    while (!condition())
    {
      if (DateTime.UtcNow - start > timeout)
        throw new TimeoutException("Condition not met within timeout.");

      await Task.Delay(25);
    }
  }

  #endregion
}
