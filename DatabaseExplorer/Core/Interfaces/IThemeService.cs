namespace DatabaseExplorer.Core.Interfaces;

public enum AppTheme
{
    Light,
    Dark
}

public interface IThemeService
{
    AppTheme CurrentTheme { get; }

    void Initialize();

    void ApplyTheme(AppTheme theme);

    event EventHandler<AppTheme>? ThemeChanged;
}
