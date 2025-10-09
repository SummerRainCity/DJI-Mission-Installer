namespace DJIMissionInstaller.UnitTests.Fixtures;

using System.Reflection;

/// <summary>Minimal helper to set private fields for testability where DI is missing.</summary>
public static class ReflectionHelper
{
  #region Methods

  public static void SetPrivateField(object instance, string fieldName, object? value)
  {
    var type = instance.GetType();
    var fi = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
      ?? throw new MissingFieldException(type.FullName, fieldName);

    fi.SetValue(instance, value);
  }

  #endregion
}
