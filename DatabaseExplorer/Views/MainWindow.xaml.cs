using System.Threading.Tasks;
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
/// because matching the native title bar to the current theme requires a Win32 call
/// that has no WPF/XAML equivalent, and because releasing the database connection on
/// close needs to happen synchronously (see <see cref="OnClosing"/>). All real
/// application logic lives in <see cref="MainViewModel"/>.
/// </summary>
public partial class MainWindow : Window
{
    private readonly IThemeService _themeService;
    private bool _closeHandled;

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
    /// Closes and releases the active database connection before the window is allowed
    /// to close. WPF will not wait for an <c>async void</c> Closing handler to finish,
    /// and — importantly — will throw if you cancel the close and later call
    /// <see cref="Window.Close"/> again from that handler's continuation; the framework
    /// treats the whole close attempt as still "in progress" until this handler's call
    /// chain fully returns. So instead of cancelling and re-closing, cleanup runs
    /// synchronously, in a single pass, via a brief blocking wait: the wait happens on a
    /// background thread (via <see cref="Task.Run(Func{Task})"/>) and
    /// <see cref="MainViewModel.DisposeAsync"/> uses <c>ConfigureAwait(false)</c>
    /// throughout, so nothing in the chain needs the (blocked) UI thread to make
    /// progress — there's no deadlock risk, just a short pause before the window closes.
    /// A guard flag makes this idempotent in case Closing is ever raised more than once
    /// for the same close (e.g. a rapid double-click on the title bar's close button).
    /// </summary>
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
            // A database that won't disconnect cleanly should never prevent the user
            // from closing the app.
        }
    }
}