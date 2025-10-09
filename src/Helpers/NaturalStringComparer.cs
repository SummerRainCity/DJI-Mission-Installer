namespace DJI_Mission_Installer.Helpers;

public class NaturalStringComparer : IComparer<string>
{
  #region Constants & Statics

  public static NaturalStringComparer Ordinal           { get; } = new NaturalStringComparer(StringComparison.Ordinal);
  public static NaturalStringComparer OrdinalIgnoreCase { get; } = new NaturalStringComparer(StringComparison.OrdinalIgnoreCase);
  public static NaturalStringComparer CurrentCulture    { get; } = new NaturalStringComparer(StringComparison.CurrentCulture);
  public static NaturalStringComparer CurrentCultureIgnoreCase { get; } =
    new NaturalStringComparer(StringComparison.CurrentCultureIgnoreCase);
  public static NaturalStringComparer InvariantCulture { get; } = new NaturalStringComparer(StringComparison.InvariantCulture);
  public static NaturalStringComparer InvariantCultureIgnoreCase { get; } =
    new NaturalStringComparer(StringComparison.InvariantCultureIgnoreCase);

  #endregion

  #region Properties & Fields - Non-Public

  private readonly StringComparison _comparison;

  #endregion

  #region Constructors

  public NaturalStringComparer(StringComparison comparison = StringComparison.OrdinalIgnoreCase)
  {
    _comparison = comparison;
  }

  #endregion

  #region Methods Impl

  public int Compare(string? x, string? y)
  {
    // Let string.Compare handle the case where x or y is null
    if (x is null || y is null)
      return string.Compare(x, y, _comparison);

    var xSegments = GetSegments(x);
    var ySegments = GetSegments(y);

    // Drive the enumerators explicitly so we can tell who finished first.
    var hasX = xSegments.MoveNext();
    var hasY = ySegments.MoveNext();

    while (hasX && hasY)
    {
      int cmp;

      // If both are numeric, compare numerically without overflow and ignoring leading zeros.
      if (xSegments.CurrentIsNumber && ySegments.CurrentIsNumber)
      {
        cmp = CompareNumericSpans(xSegments.Current, ySegments.Current);
        if (cmp != 0)
          return cmp;
      }
      // If x is a number and y is not, x is "less than" y
      else if (xSegments.CurrentIsNumber)
      {
        return -1;
      }
      // If y is a number and x is not, x is "greater than" y
      else if (ySegments.CurrentIsNumber)
      {
        return 1;
      }
      else
      {
        // Compare non-numeric segments with the configured StringComparison
        cmp = string.Compare(xSegments.Current.ToString(), ySegments.Current.ToString(), _comparison);
        if (cmp != 0)
          return cmp;
      }

      hasX = xSegments.MoveNext();
      hasY = ySegments.MoveNext();
    }

    // If both sequences ended at the same time, they are equal ("file02" == "file2").
    if (hasX == hasY)
      return 0;

    // Otherwise, the one with remaining segments is greater.
    return hasX ? 1 : -1;
  }

  #endregion

  #region Methods

  private static StringSegmentEnumerator GetSegments(string s) => new StringSegmentEnumerator(s);

  // Compares two digit-only spans numerically without allocation or overflow, ignoring leading zeros.
  private static int CompareNumericSpans(ReadOnlySpan<char> a, ReadOnlySpan<char> b)
  {
    int i = 0;
    while (i < a.Length && a[i] == '0') i++;

    int j = 0;
    while (j < b.Length && b[j] == '0') j++;

    int lenA = a.Length - i;
    int lenB = b.Length - j;

    // Treat all-zero as a single '0'
    if (lenA == 0)
    {
      i    = a.Length - 1;
      lenA = 1;
    }

    if (lenB == 0)
    {
      j    = b.Length - 1;
      lenB = 1;
    }

    if (lenA != lenB)
      return lenA.CompareTo(lenB);

    // Same significant length: ordinal compare of the significant parts.
    return a.Slice(i, lenA).CompareTo(b.Slice(j, lenB), StringComparison.Ordinal);
  }

  #endregion

  private struct StringSegmentEnumerator
  {
    private readonly string _s;
    private          int    _start;
    private          int    _length;

    public StringSegmentEnumerator(string s)
    {
      _s              = s;
      _start          = -1;
      _length         = 0;
      CurrentIsNumber = false;
    }

    public ReadOnlySpan<char> Current => _s.AsSpan(_start, _length);

    public bool CurrentIsNumber { get; private set; }

    public bool MoveNext()
    {
      var currentPosition = _start >= 0
        ? _start + _length
        : 0;

      if (currentPosition >= _s.Length)
        return false;

      int  start            = currentPosition;
      bool isFirstCharDigit = Char.IsDigit(_s[currentPosition]);

      while (++currentPosition < _s.Length && Char.IsDigit(_s[currentPosition]) == isFirstCharDigit) { }

      _start          = start;
      _length         = currentPosition - start;
      CurrentIsNumber = isFirstCharDigit;

      return true;
    }
  }
}
