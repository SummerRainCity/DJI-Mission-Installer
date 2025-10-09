namespace DJIMissionInstaller.UnitTests.Devices;

public class SwitchableDeviceOperationsTests
{
  #region Methods

  [Fact]
  public async Task Switch_Initializes_New_And_Disposes_Previous()
  {
    var adb = new FakeOps("adb");
    var mtp = new FakeOps("mtp");

    IDeviceOperations Factory(DeviceConnectionType t) => t == DeviceConnectionType.Adb ? adb : mtp;

    var sut = new SwitchableDeviceOperations(DeviceConnectionType.Adb, Factory);

    // Initialize first provider
    await sut.InitializeAsync();
    adb.Initialized.Should().BeTrue();
    adb.Disposed.Should().BeFalse();

    // Switch to MTP and initialize
    await sut.SwitchToAsync(DeviceConnectionType.Mtp, true);

    sut.CurrentType.Should().Be(DeviceConnectionType.Mtp);
    mtp.Initialized.Should().BeTrue();
    adb.Disposed.Should().BeTrue();
  }

  [Fact]
  public async Task Delegates_Calls_To_Current_Provider()
  {
    var adb = new FakeOps("adb");
    var mtp = new FakeOps("mtp");

    IDeviceOperations Factory(DeviceConnectionType t) => t == DeviceConnectionType.Adb ? adb : mtp;

    var sut = new SwitchableDeviceOperations(DeviceConnectionType.Mtp, Factory);

    await sut.InitializeAsync();
    mtp.Initialized.Should().BeTrue();

    // Switch to ADB and ensure subsequent init delegates to ADB
    await sut.SwitchToAsync(DeviceConnectionType.Adb);
    await sut.InitializeAsync();
    adb.Initialized.Should().BeTrue();
  }

  #endregion

  private sealed class FakeOps(string name) : IDeviceOperations, IDisposable
  {
    #region Constructors

    public void Dispose()
    {
      Disposed = true;
    }

    #endregion

    #region Properties & Fields - Public

    public bool Initialized { get; private set; }
    public bool Disposed    { get; private set; }

    #endregion

    #region Properties Impl - Public

    public bool         IsConnected       => false;
    /// <inheritdoc />
    public IDeviceInfo? CurrentDeviceInfo { get; }

    #endregion

    #region Methods Impl

    public override string ToString() => name;

    public Task InitializeAsync()
    {
      Initialized = true;
      return Task.CompletedTask;
    }

    public IEnumerable<IDeviceInfo> GetDevices() => Enumerable.Empty<IDeviceInfo>();
    public void Connect(IDeviceInfo deviceInfo) { }
    public void Disconnect() { }
    public string NormalizePath(string path) => path;
    public bool FileExists(string path) => false;
    public void DeleteFile(string path) { }
    public void UploadFile(Stream sourceStream, string destinationPath) { }
    public void DownloadFile(string sourcePath, string destinationPath) { }
    public bool DirectoryExists(string path) => false;
    public IEnumerable<string> GetDirectories(string path, string searchPattern, SearchOption searchOption) => Enumerable.Empty<string>();
    public DeviceFileInfo? GetFileInfo(string path) => null;

    #endregion
  }
}
