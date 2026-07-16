using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using DatabaseExplorer.Core.Interfaces;
using DatabaseExplorer.Database;
using DatabaseExplorer.DataProviders.PostgreSql;
using DatabaseExplorer.DataProviders.SqlServer;
using DatabaseExplorer.Services;
using DatabaseExplorer.ViewModels;
using DatabaseExplorer.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DatabaseExplorer;

/// <summary>
/// Application entry point. Wires up the dependency injection container, applies the
/// initial (and ongoing) light/dark theme, and translates any exception that escapes the
/// normal error-handling paths into a friendly message instead of a crash.
/// </summary>
public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(ConfigureServices)
            .Build();

        await _host.StartAsync().ConfigureAwait(true);

        // Detect and apply the current Windows light/dark theme before the window is
        // shown, and keep listening so the app follows the OS theme afterwards.
        var themeService = _host.Services.GetRequiredService<IThemeService>();
        themeService.Initialize();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        // Database engine providers. Supporting a new engine only requires adding one
        // more line here (plus the provider's own IDatabaseProvider/IDatabaseConnection/
        // IDatabaseQueryService implementations) — nothing else in the app changes.
        services.AddSingleton<IDatabaseProvider, SqlServerDatabaseProvider>();
        services.AddSingleton<IDatabaseProvider, PostgreSqlDatabaseProvider>();
        services.AddSingleton<IDatabaseProviderFactory, DatabaseProviderFactory>();

        // Cross-cutting application services.
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IExportService, ExportService>();
        services.AddSingleton<IConnectionProfileStore, ConnectionProfileStore>();

        // View models and views.
        services.AddTransient<MainViewModel>();
        services.AddTransient<MainWindow>();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync().ConfigureAwait(true);
            _host.Dispose();
        }

        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ShowFatalError(e.Exception);
        e.Handled = true;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            ShowFatalError(ex);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        // Background task failures are already surfaced to the user via IDialogService at
        // their origin; here we only need to prevent the process from being torn down.
        e.SetObserved();
    }

    private static void ShowFatalError(Exception ex)
    {
        MessageBox.Show(
            $"An unexpected error occurred and the operation could not complete:\n\n{ex.Message}",
            "Database Explorer — Unexpected Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
