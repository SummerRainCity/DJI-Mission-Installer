namespace DJIMissionInstaller.UnitTests.Extensions;

public class WaypointFileInfoExTests
{
  #region Methods

  [Fact]
  public void Device_Paths_Combine_Correctly()
  {
    var device = new MtpDeviceInfo("dev1", "/storage/emulated/0", "Demo");
    var wfi    = new WaypointFileInfo(Guid.NewGuid().ToString("D"), "x", "y", 123, DateTime.UtcNow);

    var kmz   = wfi.GetDeviceKmzPath(device);
    var image = wfi.GetDeviceImagePath(device);

    kmz.Should().Contain(Const.WaypointFolder);
    kmz.Should().EndWith($"{wfi.Id}.kmz");

    image.Should().Contain(Const.WaypointPreviewFolder);
    image.Should().EndWith($"{wfi.Id}.jpg");
  }

  [Fact]
  public void Local_Image_Uri_Uses_TempPath()
  {
    var wfi = new WaypointFileInfo(Guid.NewGuid().ToString("D"), "x", "y", 0, DateTime.MinValue);
    var uri = wfi.GetLocalImageUri();

    uri.Should().NotBeNull();
    uri.AbsolutePath.Should().EndWith($"{wfi.Id}.jpg");
    uri.AbsolutePath.Should().Contain(Const.TempFolderName);
  }

  #endregion
}
