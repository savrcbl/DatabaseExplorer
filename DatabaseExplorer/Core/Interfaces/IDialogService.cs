namespace DatabaseExplorer.Core.Interfaces;

/// <summary>
/// Abstracts user-facing dialogs so view models never reference WPF UI types directly,
/// keeping them unit-testable.
/// </summary>
public interface IDialogService
{
    /// <summary>Shows an error message to the user.</summary>
    void ShowError(string title, string message);

    /// <summary>Shows an informational message to the user.</summary>
    void ShowInfo(string title, string message);

    /// <summary>Shows a warning message to the user.</summary>
    void ShowWarning(string title, string message);

    /// <summary>Asks the user to confirm an action. Returns true if confirmed.</summary>
    bool ShowConfirmation(string title, string message);

    /// <summary>
    /// Shows a native "Save As" dialog. Returns the chosen path, or null if cancelled.
    /// </summary>
    /// <param name="filter">A Win32-style filter string, e.g. "CSV file (*.csv)|*.csv".</param>
    /// <param name="defaultFileName">The suggested file name.</param>
    string? ShowSaveFileDialog(string filter, string defaultFileName);

    /// <summary>
    /// Prompts the user for a short line of text (e.g. a name to save a connection
    /// under). Returns the entered text, or null if the user cancelled.
    /// </summary>
    /// <param name="title">The dialog's title bar text.</param>
    /// <param name="message">A short prompt shown above the input field.</param>
    /// <param name="defaultValue">The initial value of the input field.</param>
    string? ShowTextInput(string title, string message, string defaultValue = "");
}
