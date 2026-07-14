using System.Windows;
using System.Windows.Controls;
using DatabaseExplorer.ViewModels;

namespace DatabaseExplorer.Views;

/// <summary>
/// Code-behind for the main window. Deliberately thin: the two handlers here exist only
/// because plain WPF does not expose <see cref="TreeView.SelectedItem"/> as a two-way
/// bindable property and because a single toolbar button opens a small format-choice
/// menu rather than navigating anywhere. All real logic lives in <see cref="MainViewModel"/>.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Closing += OnClosing;
    }

    private void ObjectTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.SelectedNode = e.NewValue as TreeNodeViewModel;
        }
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: not null } button)
        {
            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.IsOpen = true;
        }
    }

    private async void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            await viewModel.DisposeAsync().ConfigureAwait(true);
        }
    }
}
