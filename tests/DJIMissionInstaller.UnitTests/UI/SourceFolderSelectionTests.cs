namespace DJIMissionInstaller.UnitTests.UI;

public class SourceFolderSelectionTests
{
  #region Methods

  [WpfFact]
  public async Task BrowseKmzFolder_Updates_Config_Changes_Watcher_And_Reloads_List()
  {
    // Arrange
    EnsureApp();

    var desiredFolder = Path.Combine(Path.GetTempPath(), $"KMZ_{Guid.NewGuid():N}");
    Directory.CreateDirectory(desiredFolder);
    var sample = Path.Combine(desiredFolder, "demo.kmz");
    await File.WriteAllBytesAsync(sample, new byte[] { 0x42 });

    var deviceOps = new Mock<IDeviceOperations>();
    deviceOps.SetupGet(d => d.IsConnected).Returns(false);
    deviceOps.Setup(d => d.InitializeAsync()).Returns(Task.CompletedTask);

    var fs = new Mock<IFileSystemService>();
    fs.SetupGet(f => f.CurrentFolder).Returns(desiredFolder);
    fs.Setup(f => f.WatchKmzFolder()); // no-op
    fs.Setup(f => f.ChangeKmzFolder(It.IsAny<string>())).Callback<string>(_ =>
    {
      /* watcher retargeted */
    });
    fs.Setup(f => f.GetKmzFiles()).Returns(() => Directory.GetFiles(desiredFolder, "*.kmz", SearchOption.AllDirectories)
                                                          .Select(p => new FileInfo(p)).ToList());

    var dlg  = new Mock<IDialogService>();
    var sort = new FileSortingService();
    var cfg  = new Mock<IConfigurationService>();
    var pick = new Mock<IFolderPickerService>();
    var switcher = new Mock<IDeviceOperationsSwitcher>();

    cfg.SetupProperty(c => c.KmzSourceFolder, desiredFolder);
    cfg.Setup(c => c.Save());

    pick.Setup(p => p.PickFolder(It.IsAny<string>())).Returns(desiredFolder);

    var vm = new MainViewModel(
      deviceOps.Object, fs.Object, dlg.Object, sort,
      new EsriMapScreenshotService(), new ImageService(), new KmzReader(), new MissionTransferService(),
      cfg.Object, pick.Object, switcher.Object);

    // Act
    vm.BrowseKmzFolderCommand.Execute(null);

    // Assert
    cfg.VerifySet(c => c.KmzSourceFolder = desiredFolder, Times.AtLeastOnce);
    cfg.Verify(c => c.Save(), Times.AtLeastOnce);
    fs.Verify(f => f.ChangeKmzFolder(It.Is<string>(s => s == desiredFolder)), Times.AtLeastOnce);

    // After reload, the VM should list the *.kmz we created
    vm.KmzFiles.Items.Should().NotBeEmpty();
    vm.KmzFiles.Items.Any(i => i.DisplayName == "demo.kmz").Should().BeTrue();
  }

  [WpfFact]
  public void Empty_State_When_No_Kmz_Files()
  {
    EnsureApp();

    var deviceOps = new Mock<IDeviceOperations>();
    deviceOps.SetupGet(d => d.IsConnected).Returns(false);
    deviceOps.Setup(d => d.InitializeAsync()).Returns(Task.CompletedTask);

    var fs = new Mock<IFileSystemService>();
    fs.SetupGet(f => f.CurrentFolder).Returns(Path.Combine(Path.GetTempPath(), $"KMZ_EMPTY_{Guid.NewGuid():N}"));
    fs.Setup(f => f.WatchKmzFolder());
    fs.Setup(f => f.GetKmzFiles()).Returns(new List<FileInfo>());

    var dlg      = new Mock<IDialogService>();
    var sort     = new FileSortingService();
    var cfg      = new Mock<IConfigurationService>();
    var pick     = new Mock<IFolderPickerService>();
    var switcher = new Mock<IDeviceOperationsSwitcher>();

    var vm = new MainViewModel(
      deviceOps.Object, fs.Object, dlg.Object, sort,
      new EsriMapScreenshotService(), new ImageService(), new KmzReader(), new MissionTransferService(),
      cfg.Object, pick.Object, switcher.Object);

    // VM should handle empty folder gracefully (no exceptions, no items).
    vm.KmzFiles.Items.Should().BeEmpty();
  }

  private static void EnsureApp()
  {
    if (System.Windows.Application.Current == null)
      _ = new System.Windows.Application();
  }

  #endregion
}
