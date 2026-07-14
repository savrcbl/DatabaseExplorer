using System.Windows;
using System.Windows.Controls;

namespace DatabaseExplorer.Controls;

/// <summary>
/// A small reusable overlay that dims its host area and shows an indeterminate progress
/// indicator with a status message while <see cref="IsBusy"/> is true. Used to give
/// long-running operations (scanning, loading, exporting) a clear, non-blocking visual
/// state without freezing the rest of the UI.
/// </summary>
public partial class BusyOverlay : UserControl
{
    public static readonly DependencyProperty IsBusyProperty = DependencyProperty.Register(
        nameof(IsBusy), typeof(bool), typeof(BusyOverlay), new PropertyMetadata(false));

    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(
        nameof(Message), typeof(string), typeof(BusyOverlay), new PropertyMetadata(string.Empty));

    public BusyOverlay()
    {
        InitializeComponent();
    }

    /// <summary>Whether the overlay is currently shown.</summary>
    public bool IsBusy
    {
        get => (bool)GetValue(IsBusyProperty);
        set => SetValue(IsBusyProperty, value);
    }

    /// <summary>The status text shown beneath the spinner.</summary>
    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }
}
