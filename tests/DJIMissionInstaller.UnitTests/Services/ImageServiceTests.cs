namespace DJIMissionInstaller.UnitTests.Services;

using Fixtures;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

public class ImageServiceTests
{
  #region Methods

  [StaFact] // Requires STA for some font APIs on certain environments.
  public async Task ProcessImage_Creates_Jpeg_With_Overlay_When_Input_Exists()
  {
    using var tmp    = new TempDir("IMG");
    var       input  = tmp.Combine("in.jpg");
    var       output = tmp.Combine("out.jpg");

    using (var img = new Image<Rgba32>(800, 480))
      await img.SaveAsJpegAsync(input);

    using var svc  = new ImageService();
    var       path = await svc.ProcessImageAsync(input, output, "Test KMZ", DateTime.UtcNow);

    File.Exists(path).Should().BeTrue();
    // quick JPEG signature check
    var sig = (await File.ReadAllBytesAsync(path)).Take(2).ToArray();

    sig[0].Should().Be(0xFF);
    sig[1].Should().Be(0xD8);
  }

  [StaFact]
  public async Task CreateDefaultImage_Works_When_Input_Missing()
  {
    using var tmp    = new TempDir("IMG");
    var       output = tmp.Combine("default.jpg");

    using var svc  = new ImageService();
    var       path = await svc.CreateDefaultImageAsync(output, "ID-123", DateTime.UtcNow);

    File.Exists(path).Should().BeTrue();
  }

  #endregion
}
