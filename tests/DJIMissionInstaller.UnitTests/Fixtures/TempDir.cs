namespace DJIMissionInstaller.UnitTests.Fixtures;

/// <summary>Creates and cleans a unique temporary directory for file-system based tests.</summary>
public sealed class TempDir : IDisposable
{
  #region Constructors

  public TempDir(string? prefix = null)
  {
    var root = System.IO.Path.GetTempPath();
    var name = $"{prefix ?? "DJI_MI"}_{Guid.NewGuid():N}";
    Path = System.IO.Path.Combine(root, name);
    Directory.CreateDirectory(Path);
  }

  public void Dispose()
  {
    try
    {
      Directory.Delete(Path, true);
    }
    catch
    {
      /* best effort */
    }
  }

  #endregion

  #region Properties & Fields - Public

  public string Path { get; }

  #endregion

  #region Methods

  public string Combine(params string[] parts) =>
    System.IO.Path.Combine(new[] { Path }.Concat(parts).ToArray());

  public FileInfo CreateFile(string relative, int bytes = 128)
  {
    var full = Combine(relative);
    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
    File.WriteAllBytes(full, Enumerable.Range(0, bytes).Select(_ => (byte)0x42).ToArray());
    return new FileInfo(full);
  }

  #endregion
}
