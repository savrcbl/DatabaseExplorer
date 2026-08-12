using System.Windows;
using DatabaseExplorer.Core.Interfaces;
using Microsoft.Win32;

namespace DatabaseExplorer.Services;

public sealed class ThemeService : IThemeService, IDisposable
{
    private const string PersonalizeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsUseLightThemeValue = "AppsUseLightTheme";

    private bool _initialized;
    private bool _disposed;

    public AppTheme CurrentTheme { get; private set; } = AppTheme.Light;

    public event EventHandler<AppTheme>? ThemeChanged;

    public void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        ApplyTheme(DetectWindowsTheme());
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

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
        ThemeChanged?.Invoke(this, theme);
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category != UserPreferenceCategory.General)
        {
            return;
        }

        var detected = DetectWindowsTheme();
        if (detected != CurrentTheme)
        {
            ApplyTheme(detected);
        }
    }

    private static AppTheme DetectWindowsTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath);
            var value = key?.GetValue(AppsUseLightThemeValue);
            if (value is int intValue)
            {
                return intValue == 0 ? AppTheme.Dark : AppTheme.Light;
            }
        }
        catch
        {
        }

        return AppTheme.Light;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }
}
