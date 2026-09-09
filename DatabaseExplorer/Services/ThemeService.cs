using System.IO;
using System.Text.Json;
using System.Windows;
using DatabaseExplorer.Core.Interfaces;

namespace DatabaseExplorer.Services;

/// <summary>
/// Applies and persists the app's Light/Dark theme.
///
/// Deliberately does NOT follow the Windows system theme: the app is a data tool that people
/// often run alongside a terminal or SQL client in a specific theme of their own choosing, so
/// the last theme the user explicitly picked here is respected until they change it again —
/// it never flips underneath them because they changed their Windows-wide setting.
/// </summary>
public sealed class ThemeService : IThemeService
{
    private readonly string _settingsFilePath;
    private bool _initialized;

    public AppTheme CurrentTheme { get; private set; } = AppTheme.Light;

    public event EventHandler<AppTheme>? ThemeChanged;

    public ThemeService()
    {
        var appDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DatabaseExplorer");

        Directory.CreateDirectory(appDataFolder);
        _settingsFilePath = Path.Combine(appDataFolder, "ui-settings.json");
    }

    public void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        ApplyTheme(LoadSavedTheme());
    }

    public void ToggleTheme() =>
        ApplyTheme(CurrentTheme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark);

    public void ApplyTheme(AppTheme theme)
    {
        var themeUri = theme == AppTheme.Dark
            ? new Uri("pack://application:,,,/Themes/Colors.Dark.xaml", UriKind.Absolute)
            : new Uri("pack://application:,,,/Themes/Colors.Light.xaml", UriKind.Absolute);

        var newDictionary = new ResourceDictionary { Source = themeUri };

        Application.Current.Dispatcher.Invoke(() =>
        {
            var merged = Application.Current.Resources.MergedDictionaries;

            var existingThemeDictionary = merged.FirstOrDefault(d =>
                d.Source is not null && d.Source.OriginalString.Contains("Colors.", StringComparison.Ordinal));

            if (existingThemeDictionary is not null)
            {
                var index = merged.IndexOf(existingThemeDictionary);
                merged[index] = newDictionary;
            }
            else
            {
                merged.Insert(0, newDictionary);
            }
        });

        CurrentTheme = theme;
        SaveTheme(theme);
        ThemeChanged?.Invoke(this, theme);
    }

    private AppTheme LoadSavedTheme()
    {
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                return AppTheme.Light;
            }

            var json = File.ReadAllText(_settingsFilePath);
            var settings = JsonSerializer.Deserialize<UiSettings>(json);
            return settings?.Theme ?? AppTheme.Light;
        }
        catch
        {
            // Missing, unreadable, or corrupt settings file — fall back to the default rather
            // than blocking startup.
            return AppTheme.Light;
        }
    }

    private void SaveTheme(AppTheme theme)
    {
        try
        {
            var json = JsonSerializer.Serialize(new UiSettings { Theme = theme });
            File.WriteAllText(_settingsFilePath, json);
        }
        catch
        {
            // Best-effort: if we can't persist the preference, the app still works for this
            // session, it just won't remember the choice next launch.
        }
    }

    private sealed class UiSettings
    {
        public AppTheme Theme { get; set; } = AppTheme.Light;
    }
}
