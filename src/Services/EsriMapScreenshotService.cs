namespace DJI_Mission_Installer.Services;

using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public class EsriMapScreenshotService : IMapScreenshotService
{
  #region Constants & Statics

  internal const string DefaultBaseUrl = "https://services.arcgisonline.com/arcgis/rest/services/World_Imagery/MapServer/export";

  // Transient HTTP statuses worth retrying quickly.
  private static readonly HashSet<HttpStatusCode> TransientStatuses =
  [
    HttpStatusCode.RequestTimeout,     // 408
    (HttpStatusCode)429,               // 429 Too Many Requests
    HttpStatusCode.BadGateway,         // 502
    HttpStatusCode.ServiceUnavailable, // 503
    HttpStatusCode.GatewayTimeout      // 504
  ];

  #endregion

  #region Properties & Fields - Non-Public

  private readonly HttpClient                        _httpClient;
  private readonly ILogger<EsriMapScreenshotService> _logger;
  private readonly EsriMapOptions                    _options;

  #endregion

  #region Constructors

  // Backward-compatible ctor: parameterless still works.
  public EsriMapScreenshotService(HttpClient?                        httpClient = null,
                                  ILogger<EsriMapScreenshotService>? logger     = null,
                                  EsriMapOptions?                    options    = null)
  {
    _logger  = logger ?? NullLogger<EsriMapScreenshotService>.Instance;
    _options = options ?? EsriMapOptions.Default;

    _httpClient = httpClient ?? new HttpClient
    {
      // Short, bounded timeout: we layer fast retries on top.
      Timeout = _options.HttpTimeout
    };

    _httpClient.DefaultRequestHeaders.Accept.Clear();
    _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("image/jpeg"));
  }

  #endregion

  #region Methods Impl

  public async Task<string> SaveMapScreenshotAsync(
    double            latitude,
    double            longitude,
    int               zoomLevel,
    string            outputPath,
    int               width  = 640,
    int               height = 640,
    CancellationToken ct     = default)
  {
    ValidateParameters(latitude, longitude, zoomLevel, width, height);

    if (string.IsNullOrWhiteSpace(outputPath))
      throw new ArgumentException("Output path cannot be empty", nameof(outputPath));

    try
    {
      ct.ThrowIfCancellationRequested();

      // Convert lat/long to Web Mercator
      var (x, y) = LatLongToWebMercator(latitude, longitude);

      // Calculate extent for requested zoom and dimensions
      var extent = CalculateExtent(x, y, zoomLevel, width, height);

      var url = BuildMapUrl(extent, width, height);

      _logger.LogInformation("ESRI export request: width={Width}, height={Height}, zoom={Zoom}, url={Url}",
                             width, height, zoomLevel, url);

      // Bounded, jittered retries for transient responses.
      var attempt   = 0;
      var maxTries  = _options.MaxRetries;

      while (true)
      {
        ct.ThrowIfCancellationRequested();
        attempt++;

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        using var res = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);

        if (res.IsSuccessStatusCode)
        {
          var imageBytes = await res.Content.ReadAsByteArrayAsync(ct);

          // Ensure directory and persist
          var directory = Path.GetDirectoryName(outputPath);
          if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

          await File.WriteAllBytesAsync(outputPath, imageBytes, ct);

          _logger.LogInformation("ESRI export succeeded in {Attempts} attempt(s). Saved to {Path}.", attempt, outputPath);
          return outputPath;
        }

        // Non-success: decide if we retry or fail fast
        var status = res.StatusCode;
        var body   = await res.Content.ReadAsStringAsync(ct);

        _logger.LogWarning("ESRI export failed (attempt {Attempt}/{Max}). Status={Status}, Body={BodySnippet}",
                           attempt, maxTries, status, body.Length > 256 ? body[..256] : body);

        if (attempt >= maxTries || !TransientStatuses.Contains(status))
          throw new HttpRequestException($"ESRI Map request failed with status {(int)status} {status}. Response: {body}");

        // Jittered backoff: 150ms base * 2^(attempt-1) + 0..100ms jitter
        var delay = ComputeBackoff(attempt);
        await Task.Delay(delay, ct);
      }
    }
    catch (OperationCanceledException)
    {
      _logger.LogWarning("ESRI export cancelled.");
      throw;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to get map image.");
      throw new Exception($"Failed to get map image: {ex.Message}", ex);
    }
  }

  #endregion

  #region Methods

  private static TimeSpan ComputeBackoff(int attempt)
  {
    var baseMs = 150 * Math.Pow(2, attempt - 1);
    var jitter = Random.Shared.Next(0, 100);
    var ms     = Math.Min(baseMs + jitter, 1500); // Clamp to keep UI responsive
    return TimeSpan.FromMilliseconds(ms);
  }

  private void ValidateParameters(double latitude, double longitude, int zoomLevel, int width, int height)
  {
    if (latitude < -90 || latitude > 90)
      throw new ArgumentOutOfRangeException(nameof(latitude), "Latitude must be between -90 and 90");

    if (longitude < -180 || longitude > 180)
      throw new ArgumentOutOfRangeException(nameof(longitude), "Longitude must be between -180 and 180");

    if (zoomLevel < 0 || zoomLevel > 23)
      throw new ArgumentOutOfRangeException(nameof(zoomLevel), "Zoom level must be between 0 and 23");

    if (width <= 0)
      throw new ArgumentOutOfRangeException(nameof(width), "Dimensions must be positive numbers");
    if (height <= 0)
      throw new ArgumentOutOfRangeException(nameof(height), "Dimensions must be positive numbers");

    if (width > 4096)
      throw new ArgumentOutOfRangeException(nameof(width), "Maximum dimension is 4096 pixels");
    if (height > 4096)
      throw new ArgumentOutOfRangeException(nameof(height), "Maximum dimension is 4096 pixels");
  }

  private string BuildMapUrl(Extent extent, int width, int height)
  {
    var baseUrl = string.IsNullOrWhiteSpace(_options.BaseUrl) ? DefaultBaseUrl : _options.BaseUrl;

    var parameters = new Dictionary<string, string>
    {
      { "bbox", $"{extent.XMin},{extent.YMin},{extent.XMax},{extent.YMax}" },
      { "bboxSR", "102100" }, // Web Mercator WKID
      { "size", $"{width},{height}" },
      { "imageSR", "102100" },
      { "format", "jpg" },
      { "f", "image" }
    };

    var queryString = string.Join("&", parameters.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
    return $"{baseUrl}?{queryString}";
  }

  private (double x, double y) LatLongToWebMercator(double lat, double lon)
  {
    const double earthRadius = 6378137.0; // Earth's radius in meters

    var x = lon * Math.PI / 180 * earthRadius;
    var y = Math.Log(Math.Tan((90 + lat) * Math.PI / 360)) * earthRadius;

    return (x, y);
  }

  private Extent CalculateExtent(double centerX, double centerY, int zoomLevel, int width, int height)
  {
    // Calculate the ground resolution at the given zoom (Web Mercator)
    var resolution = 156543.03392804097 * Math.Pow(2, -zoomLevel); // meters per pixel

    // Calculate the width and height in meters
    var widthInMeters  = width * resolution;
    var heightInMeters = height * resolution;

    return new Extent
    {
      XMin = centerX - widthInMeters / 2,
      XMax = centerX + widthInMeters / 2,
      YMin = centerY - heightInMeters / 2,
      YMax = centerY + heightInMeters / 2
    };
  }

  #endregion

  private class Extent
  {
    #region Properties & Fields - Public

    public double XMin { get; init; }
    public double XMax { get; init; }
    public double YMin { get; init; }
    public double YMax { get; init; }

    #endregion
  }
}

/// <summary>Options for the ESRI export client.</summary>
public sealed class EsriMapOptions
{
  #region Constants & Statics

  public static EsriMapOptions Default { get; } = new EsriMapOptions();

  #endregion

  #region Properties & Fields - Public

  public string   BaseUrl     { get; init; } = EsriMapScreenshotService.DefaultBaseUrl;
  public TimeSpan HttpTimeout { get; init; } = TimeSpan.FromSeconds(8);
  public int      MaxRetries  { get; init; } = 3;

  #endregion
}
