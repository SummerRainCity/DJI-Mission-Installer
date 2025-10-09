namespace DJIMissionInstaller.UnitTests.UI;

public class FileListViewModelTests
{
  #region Methods

  [Fact]
  public void Changing_SortMethod_Resorts_Immediately()
  {
    var sorting = new FileSortingService();
    var vm      = new FileListViewModel("KMZ", sorting);

    var items = new List<FileListItem>
    {
      Build("b.kmz", 2, DateTime.UtcNow.AddHours(-1)),
      Build("a.kmz", 1, DateTime.UtcNow)
    };

    vm.UpdateItems(items);
    vm.SortMethod    = "Name";
    vm.SortAscending = true;

    vm.Items.Select(i => i.DisplayName).Should().Equal("a.kmz", "b.kmz");

    vm.SortMethod    = "Date Modified";
    vm.SortAscending = false;
    vm.Items.First().DisplayName.Should().Be("a.kmz");

    static FileListItem Build(string name, int size, DateTime modified)
    {
      var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(dir);
      var path = Path.Combine(dir, name);
      File.WriteAllBytes(path, new byte[size]);
      File.SetLastWriteTime(path, modified);
      return new FileListItem(new KmzFile(new FileInfo(path)));
    }
  }

  #endregion
}
