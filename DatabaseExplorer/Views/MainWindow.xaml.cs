using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using DatabaseExplorer.Core.Interfaces;
using DatabaseExplorer.Helpers;
using DatabaseExplorer.ViewModels;

namespace DatabaseExplorer.Views;

public partial class MainWindow : Window
{
    private readonly IThemeService _themeService;
    private bool _closeHandled;

    public MainWindow(MainViewModel viewModel, IThemeService themeService)
    {
        InitializeComponent();
        DataContext = viewModel;
        _themeService = themeService;

        SourceInitialized += OnSourceInitialized;
        _themeService.ThemeChanged += OnThemeChanged;

        Closing += OnClosing;
    }

    private void OnSourceInitialized(object? sender, EventArgs e) =>
        ApplyTitleBarTheme(_themeService.CurrentTheme);

    private void OnThemeChanged(object? sender, AppTheme theme) =>
        ApplyTitleBarTheme(theme);

    private void ApplyTitleBarTheme(AppTheme theme)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        NativeThemeMethods.SetImmersiveDarkMode(hwnd, theme == AppTheme.Dark);
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

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_closeHandled)
        {
            return;
        }

        _closeHandled = true;
        _themeService.ThemeChanged -= OnThemeChanged;

        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        viewModel.IsBusy = true;
        viewModel.StatusMessage = "Disconnecting before exit...";

        try
        {
            Task.Run(() => viewModel.DisposeAsync().AsTask()).GetAwaiter().GetResult();
        }
        catch
        {
        }
    }
}
