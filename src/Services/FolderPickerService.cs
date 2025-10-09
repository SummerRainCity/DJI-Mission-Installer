namespace DJI_Mission_Installer.Services;

using System.IO;
using System.Runtime.InteropServices;
using Interfaces;

/// <summary>
///   Windows-native, Vista-style folder picker using IFileDialog with FOS_PICKFOLDERS.
///   This avoids WinForms and third-party packages while providing a modern UX. Docs: IFileDialog
///   options include FOS_PICKFOLDERS and FOS_FORCEFILESYSTEM.
/// </summary>
internal sealed class FolderPickerService : IFolderPickerService
{
  #region Methods Impl

  public string? PickFolder(string? initialPath = null)
  {
    IFileOpenDialog? dialog = null;
    try
    {
      dialog = (IFileOpenDialog)new FileOpenDialogRCW();

      // Configure options
      dialog.GetOptions(out var options);
      options |= FOS.FOS_PICKFOLDERS | FOS.FOS_FORCEFILESYSTEM;
      dialog.SetOptions(options);

      // Set initial folder if valid
      if (!string.IsNullOrWhiteSpace(initialPath) && Directory.Exists(initialPath))
        if (SHCreateItemFromParsingName(initialPath, IntPtr.Zero, typeof(IShellItem).GUID, out var item) == 0 && item is not null)
          dialog.SetFolder(item);

      // Show modal (ownerless is fine in WPF as app is STA)
      var hr = dialog.Show(IntPtr.Zero);
      if (hr != 0) // non-S_OK -> cancel or error
        return null;

      dialog.GetResult(out var result);
      if (result is null)
        return null;

      result.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out var pszString);
      var path = Marshal.PtrToStringUni(pszString);
      Marshal.FreeCoTaskMem(pszString);

      return path;
    }
    finally
    {
      if (dialog is not null) Marshal.FinalReleaseComObject(dialog);
    }
  }

  #endregion

  #region Win32 interop

  [ComImport]
  [Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
  private class FileOpenDialogRCW { }

  [Flags]
  private enum FOS : uint
  {
    FOS_OVERWRITEPROMPT = 0x00000002,
    FOS_STRICTFILETYPES = 0x00000004,
    FOS_NOCHANGEDIR     = 0x00000008,
    FOS_PICKFOLDERS     = 0x00000020, // Present a choice of folders (officially recommended for folder pickers)
    FOS_FORCEFILESYSTEM = 0x00000040  // Only file system items
  }

  private enum SIGDN : uint
  {
    SIGDN_FILESYSPATH = 0x80058000
  }

  [ComImport]
  [Guid("d57c7288-d4ad-4768-be02-9d969532d960")]
  [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  private interface IFileOpenDialog : IFileDialog
  {
    // IFileDialog
    [PreserveSig]
    new int Show(IntPtr parent);

    new void SetFileTypes(uint                  cFileTypes, IntPtr rgFilterSpec);
    new void SetFileTypeIndex(uint              iFileType);
    new void GetFileTypeIndex(out uint          piFileType);
    new void Advise(IntPtr                      pfde, out uint pdwCookie);
    new void Unadvise(uint                      dwCookie);
    new void SetOptions(FOS                     fos);
    new void GetOptions(out FOS                 pfos);
    new void SetDefaultFolder(IShellItem        psi);
    new void SetFolder(IShellItem               psi);
    new void GetFolder(out           IShellItem ppsi);
    new void GetCurrentSelection(out IShellItem ppsi);
    new void SetFileName(string                 pszName);
    new void GetFileName(out string             pszName);
    new void SetTitle(string                    pszTitle);
    new void SetOkButtonLabel(string            pszText);
    new void SetFileNameLabel(string            pszLabel);
    new void GetResult(out IShellItem           ppsi);
    new void AddPlace(IShellItem                psi, int fdap);
    new void SetDefaultExtension(string         pszDefaultExtension);
    new void Close(int                          hr);
    new void SetClientGuid(ref Guid             guid);
    new void ClearClientData();

    new void SetFilter(IntPtr pFilter);
    // IFileOpenDialog methods we don't need are omitted
  }

  [ComImport]
  [Guid("42f85136-db7e-439c-85f1-e4075d135fc8")]
  [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  private interface IFileDialog
  {
    [PreserveSig]
    int Show(IntPtr parent);

    void SetFileTypes(uint                  cFileTypes, IntPtr rgFilterSpec);
    void SetFileTypeIndex(uint              iFileType);
    void GetFileTypeIndex(out uint          piFileType);
    void Advise(IntPtr                      pfde, out uint pdwCookie);
    void Unadvise(uint                      dwCookie);
    void SetOptions(FOS                     fos);
    void GetOptions(out FOS                 pfos);
    void SetDefaultFolder(IShellItem        psi);
    void SetFolder(IShellItem               psi);
    void GetFolder(out           IShellItem ppsi);
    void GetCurrentSelection(out IShellItem ppsi);
    void SetFileName(string                 pszName);
    void GetFileName(out string             pszName);
    void SetTitle(string                    pszTitle);
    void SetOkButtonLabel(string            pszText);
    void SetFileNameLabel(string            pszLabel);
    void GetResult(out IShellItem           ppsi);
    void AddPlace(IShellItem                psi, int fdap);
    void SetDefaultExtension(string         pszDefaultExtension);
    void Close(int                          hr);
    void SetClientGuid(ref Guid             guid);
    void ClearClientData();
    void SetFilter(IntPtr pFilter);
  }

  [ComImport]
  [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
  [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  private interface IShellItem
  {
    void BindToHandler(IntPtr     pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
    void GetParent(out IShellItem ppsi);
    void GetDisplayName(SIGDN     sigdnName, out IntPtr ppszName);
    void GetAttributes(uint       sfgaoMask, out uint   psfgaoAttribs);
    void Compare(IShellItem       psi,       uint       hint, out int piOrder);
  }

  [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
  private static extern int SHCreateItemFromParsingName(
    [MarshalAs(UnmanagedType.LPWStr)] string             pszPath,
    IntPtr                                               pbc,
    [MarshalAs(UnmanagedType.LPStruct)]      Guid        riid,
    [MarshalAs(UnmanagedType.Interface)] out IShellItem? ppv);

  #endregion
}
