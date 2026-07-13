using System.Windows;
using DatabaseExplorer.Core.Interfaces;
using Microsoft.Win32;

namespace DatabaseExplorer.Services;

/// <summary>
/// <see cref="IDialogService"/> implementation backed by standard WPF/Win32 dialogs.
/// </summary>
public sealed class DialogService : IDialogService
{
    public void ShowError(string title, string message) =>
        Application.Current.Dispatcher.Invoke(() =>
            MessageBox.Show(GetOwner(), message, title, MessageBoxButton.OK, MessageBoxImage.Error));

    public void ShowInfo(string title, string message) =>
        Application.Current.Dispatcher.Invoke(() =>
            MessageBox.Show(GetOwner(), message, title, MessageBoxButton.OK, MessageBoxImage.Information));

    public void ShowWarning(string title, string message) =>
        Application.Current.Dispatcher.Invoke(() =>
            MessageBox.Show(GetOwner(), message, title, MessageBoxButton.OK, MessageBoxImage.Warning));

    public bool ShowConfirmation(string title, string message) =>
        Application.Current.Dispatcher.Invoke(() =>
            MessageBox.Show(GetOwner(), message, title, MessageBoxButton.YesNo, MessageBoxImage.Question)
                == MessageBoxResult.Yes);

    public string? ShowSaveFileDialog(string filter, string defaultFileName)
    {
        return Application.Current.Dispatcher.Invoke(() =>
        {
            var dialog = new SaveFileDialog
            {
                Filter = filter,
                FileName = defaultFileName,
                AddExtension = true,
                OverwritePrompt = true
            };

            return dialog.ShowDialog(GetOwner()) == true ? dialog.FileName : null;
        });
    }

    private static Window? GetOwner() => Application.Current?.MainWindow;
}
