namespace DJIMissionInstaller.UnitTests.Services;

using Fixtures;

public class FileSortingServiceTests
{
  #region Methods

  [Fact]
  public void Sort_By_Name_Uses_Natural_Order()
  {
    using var tmp = new TempDir();
    var       f1  = tmp.CreateFile("A/file2.kmz", 10);
    var       f2  = tmp.CreateFile("A/file10.kmz", 10);
    var       f3  = tmp.CreateFile("A/file1.kmz", 10);

    var items = new[]
    {
      new FileListItem(new KmzFile(new FileInfo(f1.FullName))),
      new FileListItem(new KmzFile(new FileInfo(f2.FullName))),
      new FileListItem(new KmzFile(new FileInfo(f3.FullName)))
    };

    var sut    = new FileSortingService();
    var sorted = sut.SortFiles(items, "Name", true).ToList();

    sorted.Select(i => i.DisplayName).Should().Equal("file1.kmz", "file2.kmz", "file10.kmz");
  }

  [Fact]
  public void Sort_By_Size_Descending()
  {
    using var tmp   = new TempDir();
    var       small = tmp.CreateFile("s.kmz", 5);
    var       big   = tmp.CreateFile("b.kmz", 500);

    var items = new[]
    {
      new FileListItem(new KmzFile(new FileInfo(small.FullName))),
      new FileListItem(new KmzFile(new FileInfo(big.FullName)))
    };

    var sut    = new FileSortingService();
    var sorted = sut.SortFiles(items, "Size", false).ToList();

    sorted.First().FileSize.Should().BeGreaterThan(sorted.Last().FileSize);
  }

  [Fact]
  public void UpdateItems_Highlights_New_Items()
  {
    using var tmp = new TempDir();
    var       f1  = new FileListItem(new KmzFile(new FileInfo(tmp.CreateFile("a.kmz").FullName)));
    var       f2  = new FileListItem(new KmzFile(new FileInfo(tmp.CreateFile("b.kmz").FullName)));

    var sorting = new FileSortingService();
    var vm      = new FileListViewModel("KMZ", sorting);

    vm.UpdateItems(new[] { f1 });
    vm.Items.Single().IsHighlighted.Should().BeTrue();

    // Second update: f1 remains, f2 is new -> only f2 highlighted
    vm.UpdateItems(new[] { f1, f2 });
    vm.Items.First(i => i.FilePath == f1.FilePath).IsHighlighted.Should().BeFalse();
    vm.Items.First(i => i.FilePath == f2.FilePath).IsHighlighted.Should().BeTrue();
  }

  #endregion
}
