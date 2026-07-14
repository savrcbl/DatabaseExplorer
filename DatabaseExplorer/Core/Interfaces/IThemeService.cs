namespace DatabaseExplorer.Core.Interfaces;

/// <summary>The visual theme applied to the application.</summary>
public enum AppTheme
{
    Light,
    Dark
}

/// <summary>
/// Detects the current Windows light/dark app theme, applies the matching WPF resource
/// dictionary, and reacts automatically to OS theme changes while running.
/// </summary>
public interface IThemeService
{
    /// <summary>The theme currently applied to the application.</summary>
    AppTheme CurrentTheme { get; }

    /// <summary>
    /// Detects the current Windows theme, applies it, and starts listening for OS theme
    /// change notifications. Call once during application startup.
    /// </summary>
    void Initialize();

    /// <summary>Applies the given theme's resource dictionary immediately.</summary>
    void ApplyTheme(AppTheme theme);

    /// <summary>Raised whenever the effective theme changes (including OS-driven changes).</summary>
    event EventHandler<AppTheme>? ThemeChanged;
}
