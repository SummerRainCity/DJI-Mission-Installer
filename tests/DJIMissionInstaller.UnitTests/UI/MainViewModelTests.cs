namespace DJIMissionInstaller.UnitTests.UI;

using CommunityToolkit.Mvvm.Input;
using Fixtures;

public class MainViewModelTests
{
  #region Methods

  [WpfFact]
  public async Task InitializeAsync_Populates_Available_Devices()
  {
    EnsureApp();

    var devices = new List<IDeviceInfo>
    {
      new MtpDeviceInfo("dev1", "/storage/emulated/0", "Demo Device")
    };

    var deviceOps = new Mock<IDeviceOperations>();
    deviceOps.Setup(d => d.InitializeAsync()).Returns(Task.CompletedTask);
    deviceOps.Setup(d => d.GetDevices()).Returns(devices);

    // CurrentDeviceInfo when connected (not used in this test but required by interface)
    deviceOps.SetupGet(d => d.CurrentDeviceInfo).Returns((IDeviceInfo?)null);

    var fs = new Mock<IFileSystemService>();
    fs.Setup(f => f.WatchKmzFolder()); // no-op
    fs.Setup(f => f.GetKmzFiles()).Returns(new List<FileInfo>());

    var dlg  = new Mock<IDialogService>();
    var sort = new FileSortingService();

    var vm = new MainViewModel(deviceOps.Object, fs.Object, dlg.Object, sort);

    await vm.InitializeAsync();

    vm.AvailableDevices.Should().HaveCount(1);
    vm.AvailableDevices.Single().DisplayName.Should().Be("Demo Device");
  }

  [WpfFact]
  public async Task Device_Change_Loads_Waypoints_And_Downloads_Previews()
  {
    EnsureApp();

    // Arrange device and storage
    var deviceInfo = new MtpDeviceInfo("dev1", "/storage/emulated/0", "Demo");

    var guid1 = Guid.NewGuid().ToString("D");
    var guid2 = Guid.NewGuid().ToString("D");

    var kmz1 = $"/storage/emulated/0/{Const.WaypointFolder.Replace("\\", "/")}/{guid1}/{guid1}.kmz";
    var kmz2 = $"/storage/emulated/0/{Const.WaypointFolder.Replace("\\", "/")}/{guid2}/{guid2}.kmz";

    var deviceOps = new Mock<IDeviceOperations>();
    deviceOps.SetupGet(d => d.IsConnected).Returns(true);
    deviceOps.Setup(d => d.InitializeAsync()).Returns(Task.CompletedTask);
    deviceOps.Setup(d => d.Connect(deviceInfo));
    deviceOps.SetupGet(d => d.CurrentDeviceInfo).Returns(deviceInfo);
    deviceOps.Setup(d => d.NormalizePath(It.IsAny<string>())).Returns<string>(s => s.Replace("\\", "/"));
    deviceOps.Setup(d => d.GetDirectories(
                      It.IsAny<string>(), "*", SearchOption.TopDirectoryOnly))
             .Returns([
               $"{deviceInfo.StoragePath}/{Const.WaypointFolder.Replace("\\", "/")}/{guid1}",
               $"{deviceInfo.StoragePath}/{Const.WaypointFolder.Replace("\\", "/")}/{guid2}"
             ]);

    deviceOps.Setup(d => d.GetFileInfo(kmz1)).Returns(new DeviceFileInfo(kmz1, DateTime.UtcNow.AddDays(-1), 123));
    deviceOps.Setup(d => d.GetFileInfo(kmz2)).Returns(new DeviceFileInfo(kmz2, DateTime.UtcNow, 456));

    // Preview image for guid1 exists locally; guid2 does not -> should download guid2
    var localPreview1 = Path.Combine(Const.TempPath, $"{guid1}.jpg");
    Directory.CreateDirectory(Const.TempPath);
    await File.WriteAllBytesAsync(localPreview1, [0xFF, 0xD8, 0xFF]);

    deviceOps.Setup(d => d.DownloadFile(
                      It.Is<string>(sp => sp.EndsWith($"{guid2}.jpg")),
                      It.Is<string>(dp => dp.EndsWith($"{guid2}.jpg"))));

    var fs = new Mock<IFileSystemService>();
    fs.Setup(f => f.WatchKmzFolder());
    fs.Setup(f => f.GetKmzFiles()).Returns(new List<FileInfo>());

    var dlg  = new Mock<IDialogService>();
    var sort = new FileSortingService();

    var vm = new MainViewModel(deviceOps.Object, fs.Object, dlg.Object, sort)
    {
      SelectedDevice = deviceInfo
    };

    // Allow background work to complete
    await SpinWaitAsync(() => vm.IsLoading == false, TimeSpan.FromSeconds(5));

    vm.WaypointFiles.Items.Should().HaveCount(2);

    deviceOps.Verify(d => d.DownloadFile(
                       It.Is<string>(s => s.EndsWith($"{guid2}.jpg")),
                       It.Is<string>(s => s.EndsWith($"{guid2}.jpg"))),
                     Times.Once);
  }

  [WpfFact]
  public async Task Transfer_Deletes_Old_Files_Uploads_New_And_Shows_Success()
  {
    EnsureApp();

    using var tmp     = new TempDir("KMZIN");
    var       kmzFile = tmp.CreateFile("mission/test.kmz", 256);

    // Build mocks
    var deviceInfo = new MtpDeviceInfo("dev1", "/storage/emulated/0", "Demo");
    var deviceOps  = new Mock<IDeviceOperations>();

    deviceOps.SetupGet(d => d.IsConnected).Returns(true);
    deviceOps.Setup(d => d.DirectoryExists(It.IsAny<string>())).Returns(true);
    deviceOps.Setup(d => d.FileExists(It.IsAny<string>())).Returns(true);
    deviceOps.Setup(d => d.DeleteFile(It.IsAny<string>()));
    deviceOps.Setup(d => d.UploadFile(It.IsAny<Stream>(), It.IsAny<string>()));
    deviceOps.Setup(d => d.InitializeAsync()).Returns(Task.CompletedTask);
    deviceOps.Setup(d => d.GetDevices()).Returns(new List<IDeviceInfo> { deviceInfo });
    deviceOps.Setup(d => d.Connect(deviceInfo));
    deviceOps.Setup(d => d.NormalizePath(It.IsAny<string>())).Returns<string>(s => s.Replace("\\", "/"));

    var fs = new Mock<IFileSystemService>();
    fs.Setup(f => f.WatchKmzFolder());
    fs.Setup(f => f.GetKmzFiles()).Returns([new FileInfo(kmzFile.FullName)]);

    var dlg = new Mock<IDialogService>();
    dlg.Setup(d => d.ShowInfoAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

    var sort = new FileSortingService();

    var vm = new MainViewModel(deviceOps.Object, fs.Object, dlg.Object, sort);
    await vm.InitializeAsync();

    vm.SelectedDevice = deviceInfo;

    // Prepare waypoint slots
    var id = Guid.NewGuid().ToString("D");
    deviceOps.Setup(d => d.GetDirectories(
                      It.IsAny<string>(), "*", SearchOption.TopDirectoryOnly))
             .Returns([$"{deviceInfo.StoragePath}/{Const.WaypointFolder.Replace("\\", "/")}/{id}"]);

    deviceOps.Setup(d => d.GetFileInfo(It.Is<string>(p => p.EndsWith($"{id}.kmz"))))
             .Returns(new DeviceFileInfo($".../{id}.kmz", DateTime.UtcNow, 1));

    // Force the waypoint list to refresh
    vm.SelectedDevice = deviceInfo;
    await SpinWaitAsync(() => vm.WaypointFiles.Items.Any(), TimeSpan.FromSeconds(5));

    // Ensure selections
    vm.KmzFiles.SelectedItem      = vm.KmzFiles.Items.Single();
    vm.WaypointFiles.SelectedItem = vm.WaypointFiles.Items.Single();

    // Swap in test doubles for map/image services to avoid network/graphics
    var mapMock   = new Mock<IMapScreenshotService>();
    var imageMock = new Mock<IImageService>();

    mapMock.Setup(m => m.SaveMapScreenshotAsync(
                    It.IsAny<double>(), It.IsAny<double>(), It.IsAny<int>(),
                    It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
           .Returns<double, double, int, string, int, int, CancellationToken>(async (_, _, _, outPath, _, _, _) =>
           {
             Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
             await File.WriteAllBytesAsync(outPath, [0xFF, 0xD8, 0xFF]); // tiny jpeg header
             await Task.Yield();
             return outPath;
           });

    imageMock.Setup(i => i.ProcessImageAsync(
                      It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<IProgress<double>>()))
             .Returns<string, string, string, DateTime, IProgress<double>?>((_, final, _, _, _) =>
             {
               File.WriteAllBytes(final, new byte[] { 0xFF, 0xD8, 0xFF });
               return Task.FromResult(final);
             });

    imageMock.Setup(i => i.CreateDefaultImageAsync(
                      It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>()))
             .Returns<string, string, DateTime, int, int>((final, _, _, _, _) =>
             {
               Directory.CreateDirectory(Path.GetDirectoryName(final)!);
               File.WriteAllBytes(final, new byte[] { 0xFF, 0xD8, 0xFF });
               return Task.FromResult(final);
             });

    // replace private fields
    ReflectionHelper.SetPrivateField(vm, "_mapScreenshotService", mapMock.Object);
    ReflectionHelper.SetPrivateField(vm, "_imageService", imageMock.Object);

    await ((IAsyncRelayCommand)vm.TransferFileCommand).ExecuteAsync(null);

    deviceOps.Verify(d => d.DeleteFile(It.Is<string>(p => p.EndsWith($"{id}.kmz"))), Times.Once);
    deviceOps.Verify(d => d.UploadFile(It.IsAny<Stream>(), It.Is<string>(p => p.EndsWith($"{id}.kmz"))), Times.Once);
    deviceOps.Verify(d => d.DeleteFile(It.Is<string>(p => p.EndsWith($"{id}.jpg"))), Times.Once);
    deviceOps.Verify(d => d.UploadFile(It.IsAny<Stream>(), It.Is<string>(p => p.EndsWith($"{id}.jpg"))), Times.Once);

    dlg.Verify(d => d.ShowInfoAsync("Success", It.Is<string>(s => s.Contains(".kmz"))), Times.Once);
  }

  private static void EnsureApp()
  {
    if (System.Windows.Application.Current == null)
      // Safe within [WpfFact]/STA context.
      _ = new System.Windows.Application();
  }

  private static async Task SpinWaitAsync(Func<bool> condition, TimeSpan timeout)
  {
    var start = DateTime.UtcNow;
    while (!condition())
    {
      if (DateTime.UtcNow - start > timeout)
        throw new TimeoutException("Condition not met within timeout.");

      await Task.Delay(50);
    }
  }

  #endregion
}
