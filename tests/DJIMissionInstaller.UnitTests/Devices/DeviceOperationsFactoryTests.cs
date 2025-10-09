namespace DJIMissionInstaller.UnitTests.Devices;

public class DeviceOperationsFactoryTests
{
  #region Methods

  [Fact]
  public void Factory_Returns_Correct_Implementation()
  {
    DeviceOperationsFactory.Create(DeviceConnectionType.Mtp)
                           .Should().BeOfType<MtpDeviceOperations>();

    DeviceOperationsFactory.Create(DeviceConnectionType.Adb)
                           .Should().BeOfType<AdbDeviceOperations>();
  }

  [Fact]
  public void NormalizePath_Replaces_Backslashes_For_Adb()
  {
    var adb = new AdbDeviceOperations();
    // We won't initialize or connect; NormalizePath is independent.
    adb.NormalizePath(@"a\b\c").Should().Be("a/b/c");
  }

  #endregion
}
