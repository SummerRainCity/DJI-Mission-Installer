namespace DJIMissionInstaller.UnitTests.UI;

using CommunityToolkit.Mvvm.Input;
using Fixtures;

public class MainViewModelDeviceSelectionAndMapTransferTests
{
  #region Methods

  [WpfFact]
  public async Task SelectedDevice_Reassigning_Same_Instance_Triggers_Refresh_Again()
  {
    EnsureApp();

    var deviceOps    = new Mock<IDeviceOperations>();
    var deviceInfo   = new MtpDeviceInfo("dev1", "/storage/emulated/0", "Demo");
    var connectCount = 0;

    deviceOps.SetupGet(d => d.IsConnected).Returns(true);
    deviceOps.Setup(d => d.InitializeAsync()).Returns(Task.CompletedTask);
    deviceOps.Setup(d => d.Connect(It.IsAny<IDeviceInfo>()))
             .Callback(() => Interlocked.Increment(ref connectCount));
    deviceOps.SetupGet(d => d.CurrentDeviceInfo).Returns(deviceInfo);
    deviceOps.Setup(d => d.NormalizePath(It.IsAny<string>()))
             .Returns<string>(s => s.Replace("\\", "/"));

    // No slots found -> quick no-op refresh
    deviceOps.Setup(d => d.GetDirectories(
                      It.IsAny<string>(), "*", SearchOption.TopDirectoryOnly))
             .Returns([]);

    var fs = new Mock<IFileSystemService>();

    fs.Setup(f => f.WatchKmzFolder());
    fs.Setup(f => f.GetKmzFiles()).Returns(new List<FileInfo>());

    var dlg  = new Mock<IDialogService>();
    var sort = new FileSortingService();

    var vm = new MainViewModel(deviceOps.Object, fs.Object, dlg.Object, sort)
    {
      // First assignment triggers 1st refresh
      SelectedDevice = deviceInfo
    };

    await SpinWaitAsync(() => connectCount >= 1, TimeSpan.FromSeconds(3));

    // Reassign the same instance; per implementation this should refresh again
    vm.SelectedDevice = deviceInfo;

    await SpinWaitAsync(() => connectCount >= 2, TimeSpan.FromSeconds(3));

    connectCount.Should().BeGreaterThanOrEqualTo(2);
  }


  [WpfFact]
  public async Task Transfer_Uses_KmzReader_Center_To_Request_Map_Screenshot()
  {
    EnsureApp();

    using var tmp        = new TempDir("KMZ_SOURCE");
    var       kmzFile    = tmp.CreateFile("missions/centered.kmz", 64);
    var       deviceInfo = new MtpDeviceInfo("dev1", "/storage/emulated/0", "Demo");

    // Device ops: minimal happy path for a single slot
    var id        = Guid.NewGuid().ToString("D");
    var deviceOps = new Mock<IDeviceOperations>();

    deviceOps.SetupGet(d => d.IsConnected).Returns(true);
    deviceOps.Setup(d => d.InitializeAsync()).Returns(Task.CompletedTask);
    deviceOps.Setup(d => d.Connect(deviceInfo));
    deviceOps.Setup(d => d.NormalizePath(It.IsAny<string>()))
             .Returns<string>(s => s.Replace("\\", "/"));
    deviceOps.Setup(d => d.GetDevices()).Returns(new List<IDeviceInfo> { deviceInfo });
    deviceOps.SetupGet(d => d.CurrentDeviceInfo).Returns(deviceInfo);

    var kmzPath = $"/storage/emulated/0/{Const.WaypointFolder.Replace("\\", "/")}/{id}/{id}.kmz";

    deviceOps.Setup(d => d.GetDirectories(
                      It.IsAny<string>(), "*", SearchOption.TopDirectoryOnly))
             .Returns([$"/storage/emulated/0/{Const.WaypointFolder.Replace("\\", "/")}/{id}"]);

    deviceOps.Setup(d => d.GetFileInfo(It.IsAny<string>()))
             .Returns(new DeviceFileInfo(kmzPath, DateTime.UtcNow, 1));

    deviceOps.Setup(d => d.DirectoryExists(It.IsAny<string>())).Returns(true);
    deviceOps.Setup(d => d.FileExists(It.IsAny<string>())).Returns(false);
    deviceOps.Setup(d => d.UploadFile(It.IsAny<Stream>(), It.IsAny<string>()));

    // File system + dialog
    var fs = new Mock<IFileSystemService>();

    fs.Setup(f => f.WatchKmzFolder());
    fs.Setup(f => f.GetKmzFiles()).Returns([new FileInfo(kmzFile.FullName)]);

    var dlg  = new Mock<IDialogService>();
    var sort = new FileSortingService();

    // VM instance
    var vm = new MainViewModel(deviceOps.Object, fs.Object, dlg.Object, sort);
    await vm.InitializeAsync();

    vm.SelectedDevice = deviceInfo;

    await SpinWaitAsync(() => vm.WaypointFiles.Items.Any(), TimeSpan.FromSeconds(5));

    // Ensure selections
    vm.KmzFiles.SelectedItem      = vm.KmzFiles.Items.Single();
    vm.WaypointFiles.SelectedItem = vm.WaypointFiles.Items.Single();

    // Inject a fake KMZ reader that returns a known center
    var          kmzReader = new Mock<IKmzReader>();
    const double Lat       = 60.1234;
    const double Lon       = 10.5678;

    kmzReader.Setup(r => r.TryGetCenter(It.IsAny<string>(), out It.Ref<double>.IsAny, out It.Ref<double>.IsAny))
             .Callback(new TryGetCenterCallback((string _, out double lat, out double lon) =>
             {
               lat = Lat;
               lon = Lon;
             }))
             .Returns(true);

    // Intercept map and image services via private field replacement
    var mapMock   = new Mock<IMapScreenshotService>();
    var imageMock = new Mock<IImageService>();

    // When map is requested, materialize a small jpeg to the requested path and return it
    mapMock.Setup(m => m.SaveMapScreenshotAsync(
                    It.IsAny<double>(), It.IsAny<double>(), It.IsAny<int>(),
                    It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
           .Returns<double, double, int, string, int, int, CancellationToken>(async (lat, lon, _, outPath, _, _, _) =>
           {
             // Assert the KMZ reader output flowed through to map fetch call
             lat.Should().BeApproximately(Lat, 1e-6);
             lon.Should().BeApproximately(Lon, 1e-6);

             Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
             await File.WriteAllBytesAsync(outPath, [0xFF, 0xD8, 0xFF]); // JPEG header
             return outPath;
           });

    imageMock.Setup(i => i.ProcessImageAsync(
                      It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<IProgress<double>>()))
             .Returns<string, string, string, DateTime, IProgress<double>?>((_, final, _, _, _) =>
             {
               File.WriteAllBytes(final, new byte[] { 0xFF, 0xD8, 0xFF });
               return Task.FromResult(final);
             });

    // Replace private fields
    ReflectionHelper.SetPrivateField(vm, "_mapScreenshotService", mapMock.Object);
    ReflectionHelper.SetPrivateField(vm, "_imageService", imageMock.Object);
    ReflectionHelper.SetPrivateField(vm, "_kmzReader", kmzReader.Object);

    // Execute transfer
    await ((IAsyncRelayCommand)vm.TransferFileCommand).ExecuteAsync(null);

    // Verify the map was requested with the center we injected
    mapMock.Verify(m => m.SaveMapScreenshotAsync(
                     It.Is<double>(la => Math.Abs(la - Lat) < 1e-6),
                     It.Is<double>(lo => Math.Abs(lo - Lon) < 1e-6),
                     It.IsAny<int>(),
                     It.IsAny<string>(),
                     It.IsAny<int>(),
                     It.IsAny<int>(),
                     CancellationToken.None),
                   Times.AtLeastOnce);
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

  private delegate void TryGetCenterCallback(string path, out double lat, out double lon);
}
