namespace DJIMissionInstaller.UnitTests.Services;

public class EsriMapScreenshotServiceTests
{
  #region Methods

  [Fact]
  public async Task SaveMapScreenshotAsync_Invalid_Params_Are_Rejected()
  {
    var sut = new EsriMapScreenshotService();

    await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                                                            sut.SaveMapScreenshotAsync(100, 0, 10, "x.jpg", 640, 640, CancellationToken.None));

    await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                                                            sut.SaveMapScreenshotAsync(0, 190, 10, "x.jpg", 640, 640, CancellationToken.None));

    await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                                                            sut.SaveMapScreenshotAsync(0, 0, 99, "x.jpg", 640, 640, CancellationToken.None));

    await Assert.ThrowsAsync<ArgumentException>(() =>
                                                  sut.SaveMapScreenshotAsync(0, 0, 10, "", 640, 640, CancellationToken.None));
  }

  #endregion
}
