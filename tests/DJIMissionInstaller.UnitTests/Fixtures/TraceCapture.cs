namespace DJIMissionInstaller.UnitTests.Fixtures;

/// <summary>
///   Captures Debug/Trace output into the current test's output and keeps a copy for
///   ad-hoc assertions when diagnosing failures.
/// </summary>
public sealed class TraceCapture : IDisposable
{
  #region Properties & Fields - Non-Public

  private readonly ITestOutputHelper      _output;
  private readonly BufferingTraceListener _listener;

  #endregion

  #region Constructors

  public TraceCapture(ITestOutputHelper output)
  {
    _output   = output;
    _listener = new BufferingTraceListener(_output);
    //Debug.Listeners.Add(_listener);
    Trace.Listeners.Add(_listener);
  }

  public void Dispose()
  {
    //Debug.Listeners.Remove(_listener);
    Trace.Listeners.Remove(_listener);
    _listener.Dispose();
  }

  #endregion

  #region Properties & Fields - Public

  public IReadOnlyList<string> Lines => _listener.Lines;

  #endregion

  private sealed class BufferingTraceListener : TraceListener
  {
    #region Properties & Fields - Non-Public

    private readonly ITestOutputHelper _output;
    private readonly List<string>      _lines = new();

    #endregion

    #region Constructors

    public BufferingTraceListener(ITestOutputHelper output) => _output = output;

    #endregion

    #region Properties & Fields - Public

    public IReadOnlyList<string> Lines => _lines;

    #endregion

    #region Methods Impl

    public override void Write(string? message)
    {
      /* ignore partials */
    }

    public override void WriteLine(string? message)
    {
      message ??= string.Empty;
      _lines.Add(message);
      try
      {
        _output.WriteLine(message);
      }
      catch
      {
        /* test might have already finished */
      }
    }

    #endregion
  }
}
