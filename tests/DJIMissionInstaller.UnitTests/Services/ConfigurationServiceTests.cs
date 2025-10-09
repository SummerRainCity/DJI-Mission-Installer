namespace DJIMissionInstaller.UnitTests.Services;

using Fixtures;

public class ConfigurationServiceTests
{
  #region Methods

  [Fact]
  public void KmzSourceFolder_Has_Default_And_Is_NonEmpty()
  {
    // Arrange
    var sut = new ConfigurationService();

    // Act
    var folder = sut.KmzSourceFolder;

    // Assert
    folder.Should().NotBeNullOrWhiteSpace("the service must expose a usable default folder");
    // It should be an absolute path if present.
    Path.IsPathRooted(folder).Should().BeTrue("defaults are materialized as absolute paths");
  }


  [Fact]
  public void KmzSourceFolder_Persisted_When_Overridden()
  {
    // Arrange: create a temp folder and point config to it.
    using var tmp = new TempDir("KMZ_OVERRIDES");
    var sut = new ConfigurationService
    {
      // Act
      KmzSourceFolder = tmp.Path
    };

    sut.Save();

    // New instance should observe the persisted value (single source of truth).
    var roundTrip = new ConfigurationService();

    // Assert
    roundTrip.KmzSourceFolder.Should().Be(tmp.Path);
  }


  [Fact]
  public void FileSystemService_Sees_The_Same_Configured_Folder()
  {
    // Arrange
    using var tmp = new TempDir("KMZ_SEE");
    var cfg = new ConfigurationService
    {
      KmzSourceFolder = tmp.Path
    };

    cfg.Save();

    // Create a KMZ so we can verify the service reads from the configured location.
    var kmz = tmp.CreateFile("missions/test.kmz");

    // Act
    var fs    = new FileSystemService(cfg.KmzSourceFolder);
    var files = fs.GetKmzFiles();

    // Assert
    files.Should().ContainSingle(f => f.FullName == kmz.FullName);
  }

  [Fact]
  public void UseAdbByDefault_Is_True_By_Default()
  {
    var cfg = new ConfigurationService();

    cfg.UseAdbByDefault.Should().BeTrue("ADB is the default when no prior user preference exists");
  }


  [Fact]
  public void UseAdbByDefault_Persists_After_Save()
  {
    // Arrange
    var cfg1 = new ConfigurationService
    {
      UseAdbByDefault = false
    };
    cfg1.Save();

    // Act
    var cfg2 = new ConfigurationService();

    // Assert
    cfg2.UseAdbByDefault.Should().BeFalse("preference should persist across sessions via appSettings");
  }

  #endregion
}
