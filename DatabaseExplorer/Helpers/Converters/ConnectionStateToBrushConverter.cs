using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using DatabaseExplorer.Core.Models;

namespace DatabaseExplorer.Helpers.Converters;

public sealed class ConnectionStateToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var resourceKey = value switch
        {
            AppConnectionState.Ready => "StatusSuccessBrush",
            AppConnectionState.Connected or AppConnectionState.Connecting or AppConnectionState.Scanning => "StatusBusyBrush",
            AppConnectionState.Error => "StatusErrorBrush",
            _ => "StatusIdleBrush"
        };

        return Application.Current.TryFindResource(resourceKey) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
