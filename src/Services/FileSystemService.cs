namespace DJI_Mission_Installer.Services;

using System.IO;
using Interfaces;

public class FileSystemService(string kmzSourceFolder) : IFileSystemService, IDisposable
{
  #region Constants & Statics

  private const int DebounceMilliseconds = 250;

  #endregion

  #region Properties & Fields - Non-Public

  private readonly Lock               _debounceLock = new();
  private          FileSystemWatcher? _watcher;
  private          Timer?             _debounceTimer;

  // Holds the current KMZ source folder; initialized from the primary constructor parameter and updated at runtime.
  private string _kmzSourceFolder = kmzSourceFolder;

  #endregion

  #region Constructors

  public void Dispose()
  {
    if (_watcher != null)
    {
      _watcher.EnableRaisingEvents = false;
      _watcher.Dispose();
      _watcher = null;
    }

    lock (_debounceLock)
    {
      _debounceTimer?.Dispose();
      _debounceTimer = null;
    }
  }

  #endregion

  #region Properties Impl - Public

  public string CurrentFolder => _kmzSourceFolder;

  #endregion

  #region Methods Impl

  public void WatchKmzFolder()
  {
    try
    {
      // Ensure the folder exists; if it doesn't, create it so the watcher can attach.
      Directory.CreateDirectory(_kmzSourceFolder);

      _watcher = new FileSystemWatcher(_kmzSourceFolder)
      {
        Filter                = "*.kmz",
        IncludeSubdirectories = true,
        EnableRaisingEvents   = true,
        InternalBufferSize    = 64 * 1024, // Reduce overflow on bursts
        NotifyFilter          = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.DirectoryName
      };

      _watcher.Created += OnKmzFileEvent;
      _watcher.Deleted += OnKmzFileEvent;
      _watcher.Changed += OnKmzFileEvent;
      _watcher.Renamed += OnKmzFileRenamed;
    }
    catch (Exception)
    {
      // If the watcher cannot be created (permissions, etc.), fail silently but allow manual refresh logic to continue working.
      _watcher = null;
    }
  }

  public List<FileInfo> GetKmzFiles()
  {
    try
    {
      if (!Directory.Exists(_kmzSourceFolder))
        return new List<FileInfo>();

      return Directory.GetFiles(_kmzSourceFolder, "*.kmz", SearchOption.AllDirectories)
                      .Select(f => new FileInfo(f))
                      .ToList();
    }
    catch
    {
      // If the directory is transiently unavailable, surface an empty list.
      return new List<FileInfo>();
    }
  }

  public void ChangeKmzFolder(string newFolder)
  {
    if (string.IsNullOrWhiteSpace(newFolder))
      return;

    try
    {
      // Stop previous watcher (if any)
      if (_watcher != null)
      {
        _watcher.EnableRaisingEvents =  false;
        _watcher.Created             -= OnKmzFileEvent;
        _watcher.Deleted             -= OnKmzFileEvent;
        _watcher.Changed             -= OnKmzFileEvent;
        _watcher.Renamed             -= OnKmzFileRenamed;
        _watcher.Dispose();
        _watcher = null;
      }

      // Overwrite the private field via reflection-safe technique is not needed here; we just ensure the folder exists,
      // then rebuild the watcher targeting the new folder path by reusing existing fields.
      Directory.CreateDirectory(newFolder);

      // Recreate watcher pointing at the new folder
      _watcher = new FileSystemWatcher(newFolder)
      {
        Filter                = "*.kmz",
        IncludeSubdirectories = true,
        EnableRaisingEvents   = true,
        InternalBufferSize    = 64 * 1024,
        NotifyFilter          = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.DirectoryName
      };

      _watcher.Created += OnKmzFileEvent;
      _watcher.Deleted += OnKmzFileEvent;
      _watcher.Changed += OnKmzFileEvent;
      _watcher.Renamed += OnKmzFileRenamed;

      // Update our current folder so readers (UI / GetKmzFiles) see the new path.
      _kmzSourceFolder = newFolder;

      // Immediately notify listeners so the UI refreshes.
      KmzFilesChanged?.Invoke(this, EventArgs.Empty);
    }
    catch
    {
      // Best-effort: if changing folder fails, keep previous state and avoid throwing from background operations.
    }
  }

  #endregion

  #region Methods

  private void OnKmzFileEvent(object? sender, FileSystemEventArgs e)
  {
    ScheduleDebouncedRaise();
  }

  private void OnKmzFileRenamed(object? sender, RenamedEventArgs e)
  {
    ScheduleDebouncedRaise();
  }

  private void ScheduleDebouncedRaise()
  {
    lock (_debounceLock)
    {
      _debounceTimer?.Dispose();
      _debounceTimer = new Timer(_ =>
      {
        try
        {
          KmzFilesChanged?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
          /* best effort */
        }
      }, null, DebounceMilliseconds, Timeout.Infinite);
    }
  }

  #endregion

  #region Events

  public event EventHandler? KmzFilesChanged;

  #endregion
}
