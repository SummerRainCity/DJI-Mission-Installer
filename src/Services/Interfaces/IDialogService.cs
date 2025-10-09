namespace DJI_Mission_Installer.Services.Interfaces
{
  public interface IDialogService
  {
    Task ShowErrorAsync(string title, string message);
    Task ShowInfoAsync(string  title, string message);
  }
}
