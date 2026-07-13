namespace DatabaseExplorer.Core.Interfaces;

public interface IDialogService
{
    void ShowError(string title, string message);

    void ShowInfo(string title, string message);

    void ShowWarning(string title, string message);

    bool ShowConfirmation(string title, string message);

    string? ShowSaveFileDialog(string filter, string defaultFileName);
}
