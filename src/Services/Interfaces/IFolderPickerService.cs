namespace DJI_Mission_Installer.Services.Interfaces;

/// <summary>
///   Abstraction for a folder selection dialog. Returns a file system path or null if the
///   user cancels.
/// </summary>
public interface IFolderPickerService
{
  /// <param name="initialPath">Optional initial folder (if available).</param>
  /// <returns>Selected folder path, or null if cancelled.</returns>
  string? PickFolder(string? initialPath = null);
}
