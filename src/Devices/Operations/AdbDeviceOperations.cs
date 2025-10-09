namespace DJI_Mission_Installer.Devices.Operations;

using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.Models;
using AdvancedSharpAdbClient.Receivers;
using DeviceInfo;
using Models;

public class AdbDeviceOperations : IDeviceOperations, IDisposable
{
  #region Properties & Fields - Non-Public

  private AdbClient?   _client;
  private AdbServer?   _server;
  private DeviceData?  _currentDevice;
  private IDeviceInfo? _currentDeviceInfo;
  private bool         _initialized;

  // Track the adb.exe path used and whether this instance started the server.
  private string? _adbExePath;
  private bool    _serverStartedByUs;

  private AdbClient Client => _client ?? throw new InvalidOperationException("ADB client not initialized. Call InitializeAsync first.");

  #endregion

  #region Constructors

  public void Dispose()
  {
    Disconnect();

    try
    {
      // Only stop the global ADB server if we started it. Avoid disrupting other tools.
      if (_serverStartedByUs && !string.IsNullOrWhiteSpace(_adbExePath) && File.Exists(_adbExePath))
        TryKillOurAdbServer(_adbExePath!);
    }
    catch (Exception ex)
    {
      Debug.WriteLine($"ADB cleanup failed: {ex.Message}");
    }
  }

  #endregion

  #region Properties Impl - Public

  public bool IsConnected => _currentDevice != null && _currentDeviceInfo != null;

  // Expose the effective, post-fallback device info so callers can use its StoragePath.
  public IDeviceInfo? CurrentDeviceInfo => _currentDeviceInfo;

  #endregion

  #region Methods Impl

  public async Task InitializeAsync()
  {
    if (_initialized) return;

    await Task.Run(() =>
    {
      var adbPath = FindAdbPath();
      _adbExePath = adbPath;

      _server = new AdbServer();
      var serverResult = _server.StartServer(adbPath, false);
      _serverStartedByUs = serverResult == StartServerResult.Started;

      if (serverResult != StartServerResult.Started && serverResult != StartServerResult.AlreadyRunning)
        throw new InvalidOperationException($"Failed to start ADB server. Result: {serverResult}");

      _client      = new AdbClient();
      _initialized = true;
    });
  }

  public DeviceFileInfo? GetFileInfo(string path)
  {
    var device         = GetConnectedDevice();
    var normalizedPath = path.Replace("\\", "/");

    if (!FileExists(normalizedPath))
      return null;

    // Strategy 1: stat -c '%s %Y'
    if (TryProbeStat(device, normalizedPath, out var size, out var epoch))
      return new DeviceFileInfo(path, DateTimeOffset.FromUnixTimeSeconds(epoch).LocalDateTime, (ulong)size);

    // Strategy 2: toybox stat -c
    if (TryProbeToyboxStat(device, normalizedPath, out size, out epoch))
      return new DeviceFileInfo(path, DateTimeOffset.FromUnixTimeSeconds(epoch).LocalDateTime, (ulong)size);

    // Strategy 3: ls -nl --time-style=+%s
    if (TryProbeLs(device, normalizedPath, out size, out epoch))
      return new DeviceFileInfo(path, DateTimeOffset.FromUnixTimeSeconds(epoch).LocalDateTime, (ulong)size);

    return null;
  }

  public IEnumerable<IDeviceInfo> GetDevices()
  {
    if (!_initialized) throw new InvalidOperationException("ADB client not initialized. Call InitializeAsync first.");

    var devices = new List<IDeviceInfo>();

    foreach (var device in Client.GetDevices())
      try
      {
        foreach (var storagePath in GetDeviceStoragePaths(device))
          try
          {
            var deviceInfo   = new AdbDeviceInfo(device, storagePath);
            var waypointPath = Path.Combine(storagePath, Const.WaypointFolder).Replace("\\", "/");

            var output = ExecuteShell(device, $"ls -d {Q(waypointPath)}");
            if (!output.Contains("No such file or directory", StringComparison.OrdinalIgnoreCase))
              devices.Add(deviceInfo);
          }
          catch (Exception ex)
          {
            Debug.WriteLine($"Failed to process storage path {storagePath} for device {device.Serial}: {ex.Message}");
          }
      }
      catch (Exception ex)
      {
        Debug.WriteLine($"Failed to process device {device.Serial}: {ex.Message}");
      }

    return devices;
  }

  public void Connect(IDeviceInfo deviceInfo)
  {
    if (IsConnected)
      Disconnect();

    var device = Client.GetDevices()
                       .FirstOrDefault(d => d.Serial == deviceInfo.DeviceId);

    if (device == null)
      throw new InvalidOperationException($"Device {deviceInfo.DeviceId} not found");

    // Re-scan available paths on the device *now*, because mounts/aliases can change between runs.
    var availablePaths = GetDeviceStoragePaths(device).Select(NormalizePath).ToList();

    if (availablePaths.Count == 0)
      throw new InvalidOperationException("No accessible storage paths found on the device.");

    // Resolve the best storage path. Prefer emulated/0, then self/primary, then /sdcard.
    var requested   = NormalizePath(deviceInfo.StoragePath);
    var chosenPath  = ResolveBestStoragePath(requested, availablePaths);

    _currentDevice     = device;

    // Rebuild the user-friendly info with the actual path we will use now.
    _currentDeviceInfo = new AdbDeviceInfo(device, chosenPath);
  }

  public void UploadFile(Stream sourceStream, string destinationPath)
  {
    var device         = GetConnectedDevice();
    var normalizedPath = destinationPath.Replace("\\", "/");

    // Ensure the directory exists
    var directory = Path.GetDirectoryName(normalizedPath)!.Replace("\\", "/");
    ExecuteShell(device, $"mkdir -p {Q(directory)}");

    // Upload the file directly from the source stream
    using var sync = new SyncService(Client, device);
    sync.Push(
      sourceStream,
      normalizedPath,
      UnixFileStatus.Regular | UnixFileStatus.UserRead | UnixFileStatus.UserWrite,
      DateTime.Now,
      null);
  }

  public void DownloadFile(string sourcePath, string destinationPath)
  {
    var device         = GetConnectedDevice();
    var normalizedPath = sourcePath.Replace("\\", "/");

    if (!FileExists(normalizedPath))
      throw new FileNotFoundException($"Source file {normalizedPath} not found on device");

    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

    using var sync   = new SyncService(Client, device);
    using var stream = File.Create(destinationPath);
    sync.Pull(normalizedPath, stream, null);
  }

  public bool FileExists(string path)
  {
    var device         = GetConnectedDevice();
    var normalizedPath = path.Replace("\\", "/");

    var output = ExecuteShell(device, $"test -f {Q(normalizedPath)} && echo 'exists'");
    return output.Contains("exists");
  }

  public void DeleteFile(string path)
  {
    var device         = GetConnectedDevice();
    var normalizedPath = path.Replace("\\", "/");

    if (!FileExists(normalizedPath))
      return; // File doesn't exist, nothing to delete

    _ = ExecuteShell(device, $"rm {Q(normalizedPath)}");

    // Verify deletion
    if (FileExists(normalizedPath))
      throw new IOException($"Failed to delete file {normalizedPath}");
  }

  public bool DirectoryExists(string path)
  {
    var device         = GetConnectedDevice();
    var normalizedPath = path.Replace("\\", "/");

    var output = ExecuteShell(device, $"test -d {Q(normalizedPath)} && echo 'exists'");
    return output.Contains("exists");
  }

  public IEnumerable<string> GetDirectories(string path, string searchPattern, SearchOption searchOption)
  {
    var device         = GetConnectedDevice();
    var normalizedPath = path.Replace("\\", "/");

    if (!DirectoryExists(normalizedPath))
      return [];

    var command = searchOption == SearchOption.AllDirectories
      ? $"find {Q(normalizedPath)} -type d"
      : $"find {Q(normalizedPath)} -maxdepth 1 -type d";

    var output = ExecuteShell(device, command);

    return output
           .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
           .Where(dir => MatchesPattern(Path.GetFileName(dir), searchPattern));
  }

  public void Disconnect()
  {
    _currentDevice     = null;
    _currentDeviceInfo = null;
  }

  public string NormalizePath(string path)
  {
    return path.Replace("\\", "/");
  }

  #endregion

  #region Methods

  private IEnumerable<string> GetDeviceStoragePaths(DeviceData device)
  {
    var paths = new HashSet<string>();

    foreach (var path in PrimaryAliases)
      try
      {
        // First verify the path exists
        var output = ExecuteShell(device, $"test -d {Q(path)} && echo 'exists'");
        if (!output.Contains("exists"))
          continue;

        // Then verify we can list contents (confirms permissions)
        output = ExecuteShell(device, $"ls {Q(path)}/");
        if (!output.Contains("Permission denied") && !string.IsNullOrWhiteSpace(output))
        {
          // Additional write test
          var testPath = $"{path}/.dji_test_write";
          output = ExecuteShell(device, $"touch {Q(testPath)} && rm {Q(testPath)} && echo 'writable'");

          if (output.Contains("writable"))
          {
            paths.Add(path);
            Debug.WriteLine($"Found valid storage path: {path}");

            // do not break; collect all valid candidates so we can choose the best later.
          }
        }
      }
      catch (Exception ex)
      {
        Debug.WriteLine($"Failed to verify path {path}: {ex.Message}");
        // Continue checking other paths
      }

    // If no primary storage found, try to detect external SD card
    if (paths.Count == 0)
      try
      {
        var output = ExecuteShell(device, "ls -d /storage/*/");

        foreach (var p in output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                                .Select(p => p.TrimEnd('/')))
        {
          // Skip already known paths
          if (PrimaryAliases.Contains(p)) continue;

          // Verify this new path
          var canList = ExecuteShell(device, $"ls {Q(p)}/");
          if (!canList.Contains("Permission denied"))
          {
            paths.Add(p);
            Debug.WriteLine($"Found additional storage path: {p}");
          }
        }
      }
      catch (Exception ex)
      {
        Debug.WriteLine($"Failed to detect additional storage: {ex.Message}");
      }

    return paths;
  }

  private bool MatchesPattern(string name, string pattern)
  {
    var regex = "^" + Regex.Escape(pattern)
                           .Replace("\\*", ".*")
                           .Replace("\\?", ".") + "$";

    return Regex.IsMatch(name, regex, RegexOptions.IgnoreCase);
  }

  private static readonly string[] PrimaryAliases =
  [
    // Primary storage locations (in order of preference)
    "/storage/emulated/0",   // Modern Android primary storage
    "/storage/self/primary", // Symbolic link to primary storage
    "/sdcard",               // Universal symbolic link

    // Legacy paths (fallback)
    "/storage/sdcard0",        // Old Android primary storage
    "/storage/emulated/legacy" // Legacy Android storage
  ];

  // Pick a stable, best storage alias.
  private static string ResolveBestStoragePath(string requested, IEnumerable<string> available)
  {
    var availableSet = new HashSet<string>(available, StringComparer.Ordinal);

    if (availableSet.Contains(requested))
      return requested;

    // If the requested was one of the common aliases, prefer our fixed priority list.
    if (PrimaryAliases.Contains(requested, StringComparer.Ordinal))
    {
      foreach (var alias in PrimaryAliases)
        if (availableSet.Contains(alias))
          return alias;
    }
    else
    {
      // Requested something else — still try our preferred aliases first.
      foreach (var alias in PrimaryAliases)
        if (availableSet.Contains(alias))
          return alias;
    }

    // Otherwise take the first available candidate.
    var first = available.FirstOrDefault();
    if (!string.IsNullOrEmpty(first))
      return first;

    throw new InvalidOperationException("No accessible storage paths found on the device.");
  }

  private static string FindAdbPath()
  {
    var possiblePaths = new[]
    {
      "adb.exe",
      Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "adb.exe"),
      Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "adb.exe"),
      @"C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe",
      @"C:\Program Files\Android\android-sdk\platform-tools\adb.exe"
    };

    return possiblePaths.FirstOrDefault(File.Exists)
      ?? throw new FileNotFoundException(
        "adb.exe not found. Please ensure Android SDK Platform Tools are installed or copy adb.exe to the application directory.");
  }

  private DeviceData GetConnectedDevice()
  {
    if (!IsConnected || _currentDevice == null)
      throw new InvalidOperationException("No device connected");

    var serial = ((DeviceData)_currentDevice).Serial;

    // Verify device is still connected
    if (Client.GetDevices().All(d => d.Serial != serial))
    {
      Disconnect();
      throw new InvalidOperationException("Device has been disconnected");
    }

    return (DeviceData)_currentDevice;
  }

  private string ExecuteShell(DeviceData device, string command)
  {
    var receiver = new ConsoleOutputReceiver();
    Client.ExecuteRemoteCommand(command, device, receiver, Encoding.UTF8);
    return receiver.ToString();
  }

  // Quote for POSIX shell: close existing single quotes, insert '"'"', reopen.
  private static string Q(string? path)
  {
    if (path is null) return "''";

    return "'" + path.Replace("'", "'\"'\"'") + "'";
  }

  private bool TryProbeStat(DeviceData device, string normalizedPath, out long size, out long epoch)
  {
    size  = 0;
    epoch = 0;
    try
    {
      var output = ExecuteShell(device, $"stat -c '%s %Y' {Q(normalizedPath)}").Trim();
      var parts  = output.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
      if (parts.Length >= 2 && long.TryParse(parts[0], out size) && long.TryParse(parts[1], out epoch))
        return true;
    }
    catch
    {
      /* ignore */
    }

    return false;
  }

  private bool TryProbeToyboxStat(DeviceData device, string normalizedPath, out long size, out long epoch)
  {
    size  = 0;
    epoch = 0;
    try
    {
      var output = ExecuteShell(device, $"toybox stat -c '%s %Y' {Q(normalizedPath)}").Trim();
      var parts  = output.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
      if (parts.Length >= 2 && long.TryParse(parts[0], out size) && long.TryParse(parts[1], out epoch))
        return true;
    }
    catch
    {
      /* ignore */
    }

    return false;
  }

  private bool TryProbeLs(DeviceData device, string normalizedPath, out long size, out long epoch)
  {
    size  = 0;
    epoch = 0;

    try
    {
      // Busybox ls often supports --time-style, toolbox may not; best-effort parse.
      var output = ExecuteShell(device, $"ls -nl --time-style=+%s {Q(normalizedPath)}");
      // Typical line: -rw-rw---- 1 1023 1023 12345 1719932457 /path/file
      var line = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
      if (line is null) return false;

      var tokens = Regex.Split(line.Trim(), @"\s+");
      // Expect at least: perms, links, uid, gid, size, epoch, path
      if (tokens.Length >= 7 && long.TryParse(tokens[4], out size) && long.TryParse(tokens[5], out epoch))
        return true;
    }
    catch
    {
      /* ignore */
    }

    return false;
  }

  private void TryKillOurAdbServer(string adbPath)
  {
    try
    {
      var psi = new ProcessStartInfo
      {
        FileName        = adbPath,
        Arguments       = "kill-server",
        CreateNoWindow  = true,
        UseShellExecute = false,
        WindowStyle     = ProcessWindowStyle.Hidden
      };

      using var p = Process.Start(psi);
      p?.WaitForExit(2000);
    }
    catch (Exception ex)
    {
      Debug.WriteLine($"Failed to stop our ADB server: {ex.Message}");
    }
  }

  #endregion
}
