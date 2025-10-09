namespace DJI_Mission_Installer.Devices.Operations
{
  using System.Collections.Concurrent;
  using System.Diagnostics;
  using System.IO;
  using System.Reflection;
  using System.Runtime.InteropServices;
  using System.Threading;
  using DeviceInfo;
  using MediaDevices;
  using Models;

  /// <summary>
  ///   MTP implementation that executes ALL WPD/COM calls on a dedicated STA thread.
  ///   This prevents cross-thread COM misuse (a frequent cause of 0x80004005).
  ///   Additionally, we ensure parent directories exist before uploads and add small, bounded retries.
  /// </summary>
  public sealed class MtpDeviceOperations : IDeviceOperations, IDisposable
  {
    #region Properties & Fields - Non-Public

    private readonly SingleThreadInvoker _invoker = new("MTP-STA");

    // The active MediaDevice and its info are owned by the STA thread exclusively.
    private MediaDevice? _currentDevice;
    private IDeviceInfo? _currentDeviceInfo;

    #endregion


    #region Constructors

    public void Dispose()
    {
      try
      {
        // Always dispose on the STA thread to respect COM teardown ordering.
        _invoker.Invoke(() =>
        {
          if (_currentDevice != null)
          {
            try { _currentDevice.Disconnect(); } catch { /* best effort */ }
            try { _currentDevice.Dispose(); }   catch { /* best effort */ }
            _currentDevice = null;
          }

          _currentDeviceInfo = null;
        });
      }
      catch (Exception ex)
      {
        Debug.WriteLine($"MTP dispose error: {ex.Message}");
      }
      finally
      {
        _invoker.Dispose();
      }
    }

    #endregion


    #region Properties Impl - Public

    public bool IsConnected => _invoker.Invoke(() => _currentDevice != null && _currentDeviceInfo != null);

    public IDeviceInfo? CurrentDeviceInfo => _invoker.Invoke(() => _currentDeviceInfo);

    #endregion


    #region Methods Impl

    public Task InitializeAsync()
    {
      // Nothing to initialize; presence of the STA invoker is sufficient.
      return Task.CompletedTask;
    }


    public IEnumerable<IDeviceInfo> GetDevices()
    {
      // Enumerate devices and storages on the STA thread.
      return _invoker.Invoke(() =>
      {
        var result  = new List<IDeviceInfo>();
        var devices = MediaDevice.GetDevices();

        foreach (var device in devices)
        {
          try
          {
            device.Connect();

            try
            {
              foreach (var storage in GetStorageLocations(device))
              {
                var displayName = $"{device.FriendlyName} - {Path.GetFileName(storage)}";
                var devInfo     = new MtpDeviceInfo(device.DeviceId, storage, displayName);

                if (DirectoryExistsForDevice(device, devInfo, Const.WaypointFolder))
                {
                  result.Add(devInfo);
                }
              }
            }
            finally
            {
              device.Disconnect();
            }
          }
          catch (COMException comEx)
          {
            Debug.WriteLine($"MTP enumerate device failed: {comEx.Message} (HR=0x{comEx.HResult:X8})");
          }
          catch (Exception ex)
          {
            Debug.WriteLine($"MTP enumerate device failed: {ex.Message}");
          }
        }

        return result;
      });
    }


    public void Connect(IDeviceInfo deviceInfo)
    {
      _invoker.Invoke(() =>
      {
        // Clean up any previous device first
        if (_currentDevice != null)
        {
          try { _currentDevice.Disconnect(); } catch { /* best effort */ }
          try { _currentDevice.Dispose(); }   catch { /* best effort */ }
          _currentDevice = null;
          _currentDeviceInfo = null;
        }

        var device = MediaDevice
          .GetDevices()
          .FirstOrDefault(d => d.DeviceId == deviceInfo.DeviceId)
          ?? throw new InvalidOperationException($"Device {deviceInfo.DeviceId} not found");

        device.Connect();

        _currentDevice     = device;
        _currentDeviceInfo = MtpDeviceInfo.FromDeviceInfo(deviceInfo);
      });
    }


    public void Disconnect()
    {
      _invoker.Invoke(() =>
      {
        if (_currentDevice != null)
        {
          try { _currentDevice.Disconnect(); } catch { /* best effort */ }
          try { _currentDevice.Dispose(); }   catch { /* best effort */ }
          _currentDevice = null;
        }

        _currentDeviceInfo = null;
      });
    }


    public DeviceFileInfo? GetFileInfo(string path)
    {
      return _invoker.Invoke(() =>
      {
        EnsureConnected();

        try
        {
          var file = _currentDevice!.GetFileInfo(path);
          return new DeviceFileInfo(file.FullName, file.LastWriteTime, file.Length);
        }
        catch (COMException comEx)
        {
          Debug.WriteLine($"MTP GetFileInfo failed for '{path}': {comEx.Message} (HR=0x{comEx.HResult:X8})");
          return null;
        }
        catch
        {
          return null;
        }
      });
    }


    public string NormalizePath(string path)
    {
      // MediaDevices expects '\' paths; our Const values already use backslashes.
      return path;
    }


    public bool FileExists(string path)
    {
      return _invoker.Invoke(() =>
      {
        EnsureConnected();
        return ExecuteWithRetry(() => _currentDevice!.FileExists(path));
      });
    }


    public void DeleteFile(string path)
    {
      _invoker.Invoke(() =>
      {
        EnsureConnected();

        if (!ExecuteWithRetry(() => _currentDevice!.FileExists(path)))
          return;

        ExecuteWithRetry(() =>
        {
          _currentDevice!.DeleteFile(path);
          return true;
        });

        // Optional verification
        var stillThere = ExecuteWithRetry(() => _currentDevice!.FileExists(path));
        if (stillThere)
          throw new IOException($"Failed to delete file '{path}'.");
      });
    }


    public void UploadFile(Stream sourceStream, string destinationPath)
    {
      _invoker.Invoke(() =>
      {
        EnsureConnected();

        // Ensure parent directories exist (map_preview and waypoint path chain).
        var parent = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(parent))
        {
          EnsureDirectoryRecursive(parent);
        }

        // Reset stream if possible to guarantee position.
        if (sourceStream.CanSeek && sourceStream.Position != 0)
          sourceStream.Position = 0;

        ExecuteWithRetry(() =>
        {
          _currentDevice!.UploadFile(sourceStream, destinationPath);
          return true;
        });
      });
    }


    public void DownloadFile(string sourcePath, string destinationPath)
    {
      _invoker.Invoke(() =>
      {
        EnsureConnected();

        if (!ExecuteWithRetry(() => _currentDevice!.FileExists(sourcePath)))
          throw new FileNotFoundException($"Source file '{sourcePath}' not found on device");

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

        ExecuteWithRetry(() =>
        {
          _currentDevice!.DownloadFile(sourcePath, destinationPath);
          return true;
        });
      });
    }


    public bool DirectoryExists(string path)
    {
      return _invoker.Invoke(() =>
      {
        EnsureConnected();
        return ExecuteWithRetry(() => _currentDevice!.DirectoryExists(path));
      });
    }


    public IEnumerable<string> GetDirectories(string path, string searchPattern, SearchOption searchOption)
    {
      return _invoker.Invoke(() =>
      {
        EnsureConnected();
        return ExecuteWithRetry(() => _currentDevice!.GetDirectories(path, searchPattern, searchOption));
      });
    }

    #endregion


    #region Methods - Private helpers

    private static IEnumerable<string> GetStorageLocations(MediaDevice device)
    {
      // Typical pattern with the MediaDevices library: discover storages under the root
      var rootDirectory    = device.GetRootDirectory();
      var storageLocations = device.GetDirectories(rootDirectory.FullName, "*", SearchOption.TopDirectoryOnly);
      return storageLocations;
    }


    private static bool DirectoryExistsForDevice(MediaDevice device, IDeviceInfo deviceInfo, string relativePath)
    {
      var fullPath = Path.Combine(deviceInfo.StoragePath, relativePath);
      return device.DirectoryExists(fullPath);
    }


    private void EnsureConnected()
    {
      if (_currentDevice == null || _currentDeviceInfo == null)
        throw new InvalidOperationException("Device is not connected");
    }


    /// <summary>
    ///   Recursively ensures the full directory chain exists on the device for MTP.
    ///   Uses MediaDevice.CreateDirectory where needed.
    /// </summary>
    private void EnsureDirectoryRecursive(string fullPath)
    {
      // Normalize into segments and build up the path from the storage root.
      // Example: \Internal shared storage\Android\data\dji.go.v5\files\waypoint\map_preview
      var parts = fullPath.Split(['\\'], StringSplitOptions.RemoveEmptyEntries);

      if (parts.Length == 0)
        return;

      var accumulator = fullPath.StartsWith("\\") ? "\\" : string.Empty;

      foreach (var part in parts)
      {
        accumulator = string.IsNullOrEmpty(accumulator) ? part : $"{accumulator}\\{part}";

        var exists = ExecuteWithRetry(() => _currentDevice!.DirectoryExists(accumulator));

        if (!exists)
        {
          ExecuteWithRetry(() =>
          {
            _currentDevice!.CreateDirectory(accumulator);
            return true;
          });
        }
      }
    }


    /// <summary>
    ///   Small, bounded retry for flaky MTP operations (COM E_FAIL and intermittent IO).
    ///   Retries 3x with short backoff.
    /// </summary>
    private static T ExecuteWithRetry<T>(Func<T> action)
    {
      const int maxAttempts   = 3;
      int       attempt       = 0;
      int       delayMs       = 150;

      while (true)
      {
        try
        {
          return action();
        }
        catch (COMException ex) when (attempt < maxAttempts - 1)
        {
          Debug.WriteLine($"MTP COMException retry: {ex.Message} (HR=0x{ex.HResult:X8}), attempt {attempt + 1}/{maxAttempts}");
          Thread.Sleep(delayMs);
          attempt++;
        }
        catch (IOException ex) when (attempt < maxAttempts - 1)
        {
          Debug.WriteLine($"MTP IOException retry: {ex.Message}, attempt {attempt + 1}/{maxAttempts}");
          Thread.Sleep(delayMs);
          attempt++;
        }
      }
    }

    #endregion


    #region Nested Types - Single-thread invoker (STA)

    /// <summary>
    ///   Executes posted actions on a dedicated STA thread. All WPD/COM calls must run here.
    /// </summary>
    private sealed class SingleThreadInvoker : IDisposable
    {
      private readonly BlockingCollection<IWorkItem> _queue = new();
      private readonly Thread                         _thread;

      public SingleThreadInvoker(string name)
      {
        _thread = new Thread(Run)
        {
          Name = name,
          IsBackground = true
        };

        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
      }

      public void Dispose()
      {
        _queue.CompleteAdding();

        try
        {
          if (!_thread.Join(TimeSpan.FromSeconds(2)))
            Debug.WriteLine("MTP STA thread did not terminate within timeout.");
        }
        catch { /* best effort */ }
      }

      public void Invoke(Action action)
      {
        var item = new WorkItem(action);
        _queue.Add(item);
        item.Wait();
      }

      public T Invoke<T>(Func<T> func)
      {
        var item = new WorkItem<T>(func);
        _queue.Add(item);
        return item.WaitAndGet();
      }

      private void Run()
      {
        try
        {
          foreach (var item in _queue.GetConsumingEnumerable())
          {
            item.Execute();
          }
        }
        catch (Exception ex)
        {
          Debug.WriteLine($"MTP STA thread unhandled error: {ex.Message}");
        }
      }

      private interface IWorkItem
      {
        void Execute();
        void Wait();
      }

      private sealed class WorkItem(Action action) : IWorkItem
      {
        private readonly ManualResetEventSlim _done   = new(false);
        private          Exception?           _error;

        public void Execute()
        {
          try { action(); }
          catch (Exception ex) { _error = ex; }
          finally { _done.Set(); }
        }

        public void Wait()
        {
          _done.Wait();
          if (_error != null) throw new TargetInvocationException(_error);
        }
      }

      private sealed class WorkItem<T>(Func<T> func) : IWorkItem
      {
        private readonly ManualResetEventSlim _done = new(false);
        private          Exception?           _error;
        private          T                    _result = default!;

        public void Execute()
        {
          try { _result = func(); }
          catch (Exception ex) { _error = ex; }
          finally { _done.Set(); }
        }

        public void Wait() => WaitAndGet();

        public T WaitAndGet()
        {
          _done.Wait();

          if (_error != null) throw new TargetInvocationException(_error);

          return _result;
        }
      }
    }

    #endregion
  }
}
