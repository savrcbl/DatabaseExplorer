using System.Globalization;
using System.Windows.Data;

namespace DatabaseExplorer.Helpers.Converters;

/// <summary>
/// Converts null/non-null (and, for strings, empty/non-empty) to a <see cref="bool"/>.
/// Unlike <see cref="NullToVisibilityConverter"/>, this is for binding to boolean
/// properties such as <c>IsEnabled</c> rather than <c>Visibility</c>.
/// </summary>
public sealed class NullToBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hasValue = value is not null && value is not string { Length: 0 };
        if (string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase))
        {
            hasValue = !hasValue;
        }

        return hasValue;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
