namespace DJIMissionInstaller.UnitTests.Services;

using Fixtures;

public class FileSystemServiceTests
{
  #region Methods

  [Fact(Timeout = 15000)]
  public async Task WatchKmzFolder_Raises_Event_On_Create_Rename_Delete()
  {
    using var tmp = new TempDir("KMZROOT");
    var       sut = new FileSystemService(tmp.Path);

    try
    {
      sut.WatchKmzFolder();

      var events = 0;
      var mre    = new ManualResetEventSlim(false);
      sut.KmzFilesChanged += (_, _) =>
      {
        events++;
        mre.Set();
      };

      // Create
      var file = tmp.CreateFile("x/test1.kmz");
      await Task.Run(() => mre.Wait(TimeSpan.FromSeconds(5)));
      mre.IsSet.Should().BeTrue("Created must raise");
      mre.Reset();

      // Rename
      var renamed = tmp.Combine("x/test2.kmz");
      File.Move(file.FullName, renamed);
      await Task.Run(() => mre.Wait(TimeSpan.FromSeconds(5)));
      mre.IsSet.Should().BeTrue("Renamed must raise");
      mre.Reset();

      // Delete
      File.Delete(renamed);
      await Task.Run(() => mre.Wait(TimeSpan.FromSeconds(5)));

      events.Should().BeGreaterThanOrEqualTo(3);
    }
    finally
    {
      sut.Dispose();
    }
  }

  [Fact]
  public void GetKmzFiles_Returns_All_Kmz_Recursively()
  {
    using var tmp = new TempDir("KMZROOT");
    tmp.CreateFile("a/b/c1.kmz");
    tmp.CreateFile("a/c2.kmz");
    tmp.CreateFile("a/notthis.txt");

    var sut   = new FileSystemService(tmp.Path);
    var files = sut.GetKmzFiles();

    files.Should().HaveCount(2);
    files.All(f => f.Extension.Equals(".kmz", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
  }

  #endregion
}
