namespace DatabaseExplorer.Core.Interfaces;

public enum AppTheme
{
    Light,
    Dark
}

public interface IThemeService
{
    AppTheme CurrentTheme { get; }

    /// <summary>Loads the last-saved theme (defaulting to Light the first time the app runs) and applies it.</summary>
    void Initialize();

    /// <summary>Applies the given theme and persists it as the user's choice.</summary>
    void ApplyTheme(AppTheme theme);

    /// <summary>Switches between Light and Dark and persists the result.</summary>
    void ToggleTheme();

    event EventHandler<AppTheme>? ThemeChanged;
}
