using System.Windows;
using System.Windows.Input;

namespace DatabaseExplorer.Views;

/// <summary>
/// A small modal dialog collecting a single line of text, used for prompts such as
/// naming a saved connection. Set <see cref="Window.Owner"/> before calling
/// <see cref="Window.ShowDialog"/> so it centers correctly and inherits the app's
/// current theme.
/// </summary>
public partial class TextInputDialog : Window
{
    public TextInputDialog(string title, string message, string defaultValue)
    {
        InitializeComponent();

        Title = title;
        MessageText.Text = message;
        InputTextBox.Text = defaultValue;
        InputTextBox.Loaded += (_, _) =>
        {
            InputTextBox.Focus();
            InputTextBox.SelectAll();
        };
    }

    /// <summary>The text entered when the dialog was accepted; null if cancelled.</summary>
    public string? ResultText { get; private set; }

    private void OkButton_Click(object sender, RoutedEventArgs e) => Accept();

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        ResultText = null;
        DialogResult = false;
    }

    private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Accept();
        }
    }

    private void Accept()
    {
        var text = InputTextBox.Text.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        ResultText = text;
        DialogResult = true;
    }
}
