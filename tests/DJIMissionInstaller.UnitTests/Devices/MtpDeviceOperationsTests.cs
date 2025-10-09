namespace DJIMissionInstaller.UnitTests.Devices;

/// <summary>
///   Tests for <see cref="MtpDeviceOperations" /> that are hermetic and do not require a
///   physical MTP device. They validate: - Guard clauses via <c>EnsureConnected</c> throwing the
///   expected exception before calling into the MediaDevices API - Connect() behavior when the
///   requested device is not present - Path normalization semantics (a no-op for MTP)
/// </summary>
public class MtpDeviceOperationsTests
{
  #region Methods

  [Fact]
  public void Connect_With_Unknown_DeviceId_Throws()
  {
    // Arrange: choose an ID that is virtually guaranteed to not match any real device.
    var deviceInfo = new MtpDeviceInfo("nonexistent-device-id-for-tests", "/storage/emulated/0", "Demo");
    var sut        = new MtpDeviceOperations();

    // Act
    Action act = () => sut.Connect(deviceInfo);

    // Assert
    act.Should().Throw<InvalidOperationException>()
       .WithMessage("*not found*");
  }


  [Fact]
  public void Operations_When_Not_Connected_Throw_DeviceIsNotConnected()
  {
    // Arrange
    var sut = new MtpDeviceOperations();

    // Each of these calls should be short-circuited by EnsureConnected().
    Action[] ops =
    [
      () => sut.FileExists("/x/y/z"),
      () => sut.DeleteFile("/x/y/z"),
      () => sut.UploadFile(new MemoryStream(new byte[] { 1, 2, 3 }), "/x/y/z"),
      () => sut.DownloadFile("/x/y/z", Path.Combine(Path.GetTempPath(), "dummy")),
      () => sut.DirectoryExists("/x/y"),
      () => { _ = sut.GetDirectories("/x", "*", SearchOption.TopDirectoryOnly); },
      () => { _ = sut.GetFileInfo("/x/y/z"); }
    ];

    foreach (var op in ops)
      op.Should().Throw<InvalidOperationException>()
        .WithMessage("*Device is not connected*");
  }


  [Fact]
  public void Disconnect_Is_Idempotent()
  {
    // Arrange
    var sut = new MtpDeviceOperations();

    // Act + Assert: Should not throw even if called multiple times without a prior Connect().
    sut.Invoking(s => s.Disconnect()).Should().NotThrow();
    sut.Invoking(s => s.Disconnect()).Should().NotThrow();
  }


  [Fact]
  public void NormalizePath_Is_NoOp_For_Mtp()
  {
    // Arrange
    var sut  = new MtpDeviceOperations();
    var path = @"Android\data\dji.go.v5\files\waypoint\123\123.kmz";

    // Act
    var normalized = sut.NormalizePath(path);

    // Assert
    normalized.Should().Be(path);
  }

  #endregion
}
