namespace DJIMissionInstaller.UnitTests.Devices;

using System.Reflection;

/// <summary>
///   Tests for <see cref="AdbDeviceOperations"/> that do not require a real ADB server or device.
///   We intentionally validate environment-independent behavior such as:
///   - Guard clauses (Initialize/Connect preconditions) throwing the correct exceptions
///   - Path normalization semantics (backslashes -> forward slashes)
///   - Filename wildcard matching logic used by GetDirectories (via reflection to the private method)
///   These tests protect critical invariants while keeping CI stable and hermetic.
/// </summary>
public class AdbDeviceOperationsTests
{
  #region Methods

  [Fact]
  public void GetDevices_Before_Initialize_Throws()
  {
    // Arrange
    var sut = new AdbDeviceOperations();

    // Act
    var act = () => sut.GetDevices();

    // Assert
    act.Should().Throw<InvalidOperationException>()
       .WithMessage("*ADB client not initialized*");
  }


  [Fact]
  public void Connect_Before_Initialize_Throws()
  {
    // Arrange
    var sut        = new AdbDeviceOperations();
    var deviceInfo = new MtpDeviceInfo("dev1", "/storage/emulated/0", "Demo"); // any IDeviceInfo instance

    // Act
    var act = () => sut.Connect(deviceInfo);

    // Assert
    act.Should().Throw<InvalidOperationException>()
       .WithMessage("*ADB client not initialized*");
  }


  [Fact]
  public void Operations_When_Not_Connected_Throw_NoDeviceConnected()
  {
    // Arrange
    var sut = new AdbDeviceOperations();

    // Each operation should validate connection state before hitting the ADB client.
    Action[] ops =
    [
      () => sut.FileExists("/x/y/z"),
      () => sut.DeleteFile("/x/y/z"),
      () => sut.UploadFile(new MemoryStream([1, 2, 3]), "/x/y/z"),
      () => sut.DownloadFile("/x/y/z", Path.Combine(Path.GetTempPath(), "dummy")),
      () => sut.DirectoryExists("/x/y"),
      () => { _ = sut.GetDirectories("/x", "*", SearchOption.TopDirectoryOnly); }
    ];

    foreach (var op in ops)
    {
      op.Should().Throw<InvalidOperationException>()
        .WithMessage("*No device connected*");
    }
  }


  [Fact]
  public void NormalizePath_Replaces_Backslashes_With_ForwardSlashes()
  {
    // Arrange
    var sut = new AdbDeviceOperations();

    // Act
    var normalized = sut.NormalizePath(@"Android\data\dji.go.v5\files\waypoint\123\123.kmz");

    // Assert
    normalized.Should().Be("Android/data/dji.go.v5/files/waypoint/123/123.kmz");
  }


  [Fact]
  public void MatchesPattern_Wildcards_Behave_As_Expected()
  {
    // Arrange
    var sut      = new AdbDeviceOperations();
    var mi       = typeof(AdbDeviceOperations).GetMethod("MatchesPattern", BindingFlags.Instance | BindingFlags.NonPublic)
                  ?? throw new MissingMethodException(nameof(AdbDeviceOperations), "MatchesPattern");

    // Act + Assert
    Call("file.txt", "*.txt").Should().BeTrue();
    Call("image.JPG", "*.jpg").Should().BeTrue("matching is case-insensitive");
    Call("alpha01", "alpha??").Should().BeTrue();
    Call("alpha1",  "alpha??").Should().BeFalse();
    Call("mission-2025.kmz", "mission-*.kmz").Should().BeTrue();
    Call("mission-2025.kmz", "mission-????.kmz").Should().BeTrue();
    Call("other.kmz", "mission-*.kmz").Should().BeFalse();
    return;

    bool Call(string name, string pattern) => (bool)mi.Invoke(sut, [name, pattern])!;
  }

  #endregion
}
