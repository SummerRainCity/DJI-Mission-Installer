namespace DJI_Mission_Installer.Models;

/// <summary>Result of a mission transfer operation.</summary>
public sealed class MissionTransferOutcome(string localPreviewPath, bool success)
{
  #region Properties & Fields - Public

  public string LocalPreviewPath { get; } = localPreviewPath;

  public bool Success { get; } = success;

  #endregion
}
