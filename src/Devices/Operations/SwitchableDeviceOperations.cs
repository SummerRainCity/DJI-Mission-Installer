namespace DJI_Mission_Installer.Devices.Operations
{
  using System.Diagnostics;
  using System.IO;
  using DeviceInfo;

  /// <summary>
  ///   A stable, singleton device operations façade that can switch between ADB and MTP
  ///   implementations at runtime. All <see cref="IDeviceOperations" /> calls are delegated to the
  ///   current provider instance.
  /// </summary>
  public sealed class SwitchableDeviceOperations : IDeviceOperations, IDeviceOperationsSwitcher, IDisposable
  {
    #region Properties & Fields - Non-Public

    private readonly Lock                                          _gate = new();
    private readonly Func<DeviceConnectionType, IDeviceOperations> _factory;

    private IDeviceOperations _inner;

    #endregion

    #region Constructors

    /// <summary>
    ///   Default constructor: uses <see cref="DeviceOperationsFactory.Create" /> to build
    ///   concrete providers.
    /// </summary>
    public SwitchableDeviceOperations(DeviceConnectionType initialType)
      : this(initialType, DeviceOperationsFactory.Create) { }

    /// <summary>Test-friendly constructor that accepts a custom factory delegate.</summary>
    public SwitchableDeviceOperations(DeviceConnectionType                          initialType,
                                      Func<DeviceConnectionType, IDeviceOperations> factory)
    {
      _factory    = factory ?? throw new ArgumentNullException(nameof(factory));
      _inner      = _factory(initialType);
      CurrentType = initialType;
    }

    public void Dispose()
    {
      if (_inner is IDisposable d)
        d.Dispose();
    }

    #endregion

    #region Properties Impl - Public

    /// <inheritdoc />
    public bool IsConnected => _inner.IsConnected;
    /// <inheritdoc />
    public IDeviceInfo? CurrentDeviceInfo => _inner.CurrentDeviceInfo;

    /// <inheritdoc />
    public DeviceConnectionType CurrentType { get; private set; }

    #endregion

    #region Methods Impl

    /// <inheritdoc />
    public Task InitializeAsync() => _inner.InitializeAsync();

    /// <inheritdoc />
    public IEnumerable<IDeviceInfo> GetDevices() => _inner.GetDevices();

    /// <inheritdoc />
    public void Connect(IDeviceInfo deviceInfo) => _inner.Connect(deviceInfo);

    /// <inheritdoc />
    public void Disconnect() => _inner.Disconnect();

    /// <inheritdoc />
    public string NormalizePath(string path) => _inner.NormalizePath(path);

    /// <inheritdoc />
    public bool FileExists(string path) => _inner.FileExists(path);

    /// <inheritdoc />
    public void DeleteFile(string path) => _inner.DeleteFile(path);

    /// <inheritdoc />
    public void UploadFile(Stream sourceStream, string destinationPath) => _inner.UploadFile(sourceStream, destinationPath);

    /// <inheritdoc />
    public void DownloadFile(string sourcePath, string destinationPath) => _inner.DownloadFile(sourcePath, destinationPath);

    /// <inheritdoc />
    public bool DirectoryExists(string path) => _inner.DirectoryExists(path);

    /// <inheritdoc />
    public IEnumerable<string> GetDirectories(string path, string searchPattern, SearchOption searchOption) =>
      _inner.GetDirectories(path, searchPattern, searchOption);

    /// <inheritdoc />
    public Models.DeviceFileInfo? GetFileInfo(string path) => _inner.GetFileInfo(path);

    /// <inheritdoc />
    public Task SwitchToAsync(DeviceConnectionType type, bool initialize = false)
    {
      lock (_gate)
      {
        if (type == CurrentType)
          return initialize ? _inner.InitializeAsync() : Task.CompletedTask;

        // Tear down the previous provider safely.
        try
        {
          if (_inner is IDisposable d)
            d.Dispose();
        }
        catch (Exception ex)
        {
          Debug.WriteLine($"Disposing previous provider failed: {ex.Message}");
        }

        // Replace with new provider.
        _inner      = _factory(type);
        CurrentType = type;

        return initialize ? _inner.InitializeAsync() : Task.CompletedTask;
      }
    }

    #endregion
  }
}
