using System.Globalization;
using System.Windows.Data;

namespace DatabaseExplorer.Helpers.Converters;

/// <summary>Converts a <see cref="bool"/> to its logical inverse.</summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b ? !b : true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b ? !b : false;
}
