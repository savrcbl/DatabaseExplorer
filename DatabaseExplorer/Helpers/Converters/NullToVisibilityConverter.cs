using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DatabaseExplorer.Helpers.Converters;

/// <summary>
/// Converts null/non-null (and, for strings, empty/non-empty) to <see cref="Visibility"/>.
/// Pass "Invert" as the converter parameter to show only when the value IS null/empty
/// (useful for "nothing loaded yet" placeholders).
/// </summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hasValue = value is not null && value is not string { Length: 0 };
        if (string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase))
        {
            hasValue = !hasValue;
        }

        return hasValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
