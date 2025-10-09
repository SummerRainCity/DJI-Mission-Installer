namespace DJIMissionInstaller.UnitTests.Devices;

using System.Reflection;

public class AdbQuotingTests
{
  #region Methods

  [Fact]
  public void Q_Properly_Escapes_Single_Quotes_For_Shell()
  {
    var mi = typeof(AdbDeviceOperations).GetMethod("Q",
                                                   BindingFlags.Static | BindingFlags.NonPublic)
      ?? throw new MissingMethodException(nameof(AdbDeviceOperations), "Q");

    var input  = "it's ok";
    var quoted = (string)mi.Invoke(null, [input])!;

    // Expected: 'it'"'"'s ok'
    quoted.Should().Be("'it'\"'\"'s ok'");
  }

  #endregion
}
