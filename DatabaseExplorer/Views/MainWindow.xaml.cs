using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using DatabaseExplorer.Core.Interfaces;
using DatabaseExplorer.Helpers;
using DatabaseExplorer.ViewModels;

namespace DatabaseExplorer.Views;

/// <summary>
/// Code-behind for the main window. Deliberately thin: most handlers here exist only
/// because plain WPF does not expose <see cref="TreeView.SelectedItem"/> as a two-way
/// bindable property, because a single toolbar button opens a small format-choice menu,
/// and because matching the native title bar to the current theme requires a Win32 call
/// that has no WPF/XAML equivalent. All real application logic lives in
/// <see cref="MainViewModel"/>.
/// </summary>
public partial class MainWindow : Window
{
    private readonly IThemeService _themeService;
    private bool _cleanupStarted;
    private bool _cleanupComplete;

    public MainWindow(MainViewModel viewModel, IThemeService themeService)
    {
        InitializeComponent();
        DataContext = viewModel;
        _themeService = themeService;

        // WPF's native window chrome does not automatically follow the OS light/dark
        // theme the way its own content can via resource dictionaries — that requires
        // an explicit DWM call once the window's handle exists, and again whenever the
        // theme changes afterwards.
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

    /// <summary>
    /// Ensures any open database connection is closed and disposed before the window is
    /// actually allowed to close. <see cref="Window.Closing"/>'s handler can't simply be
    /// awaited (WPF doesn't wait for an async void handler to finish), so the first
    /// attempt is cancelled while cleanup runs in the background, and the window closes
    /// itself for real once that cleanup completes.
    /// </summary>
    private async void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_cleanupComplete)
        {
            // Cleanup already finished — this is the real close triggered below; let it through.
            return;
        }

        e.Cancel = true;

        if (_cleanupStarted)
        {
            // A previous close attempt already kicked off cleanup; nothing more to do
            // here but wait for it to finish and call Close() again.
            return;
        }

        _cleanupStarted = true;

        if (DataContext is MainViewModel viewModel)
        {
            try
            {
                await viewModel.PrepareForShutdownAsync().ConfigureAwait(true);
            }
            finally
            {
                await viewModel.DisposeAsync().ConfigureAwait(true);
            }
        }

        _themeService.ThemeChanged -= OnThemeChanged;
        _cleanupComplete = true;
        Close();
    }
}