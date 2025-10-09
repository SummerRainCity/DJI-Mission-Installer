namespace DJIMissionInstaller.UnitTests.Helpers;

public class NaturalStringComparerTests
{
  #region Methods

  [Fact]
  public void Numbers_Are_Compared_By_Value()
  {
    var sut = new NaturalStringComparer();

    sut.Compare("file2", "file10").Should().BeNegative();
    sut.Compare("file10", "file2").Should().BePositive();
    sut.Compare("file02", "file2").Should().Be(0);
  }

  [Fact]
  public void Nulls_Are_Delegated_To_string_Compare()
  {
    var sut = new NaturalStringComparer();
    sut.Compare(null, "x").Should().BeNegative();
    sut.Compare("x", null).Should().BePositive();
    sut.Compare(null, null).Should().Be(0);
  }

  [Fact]
  public void NonNumeric_Segments_Use_StringComparison()
  {
    var sut = NaturalStringComparer.OrdinalIgnoreCase;
    sut.Compare("Alpha", "alpha").Should().Be(0);
  }

  #endregion
}
