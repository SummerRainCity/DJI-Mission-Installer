namespace DJI_Mission_Installer.Services.Interfaces;

using System.IO;

public interface IFileSystemService
{
  event EventHandler KmzFilesChanged;
  void               WatchKmzFolder();
  List<FileInfo>     GetKmzFiles();

  // Change the source folder at runtime, update watchers, and raise KmzFilesChanged.
  void ChangeKmzFolder(string newFolder);

  // Expose currently watched KMZ source folder (used for UI display and tests).
  string CurrentFolder { get; }
}
