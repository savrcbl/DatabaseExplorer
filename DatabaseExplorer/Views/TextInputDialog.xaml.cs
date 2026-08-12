using System.Windows;
using System.Windows.Input;

namespace DatabaseExplorer.Views;

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
