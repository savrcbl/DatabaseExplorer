using System.Data;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace DatabaseExplorer.Helpers;

/// <summary>
/// Builds tab-separated clipboard payloads from a bound <see cref="DataGrid"/>'s current
/// selection, for the toolbar's Copy Cell / Copy Row / Copy Table actions. The grid is
/// bound to a <see cref="DataView"/>, so cell values are read directly from the
/// underlying <see cref="DataRowView"/> rather than from realized visual cells, which
/// keeps this correct even for virtualized, off-screen rows.
/// </summary>
public static class ClipboardHelper
{
    /// <summary>Copies the single currently-focused cell's value to the clipboard.</summary>
    public static bool CopyCurrentCell(DataGrid grid)
    {
        var cellInfo = grid.CurrentCell;
        if (!cellInfo.IsValid || cellInfo.Item is not DataRowView rowView || cellInfo.Column is null)
        {
            return false;
        }

        var columnName = GetColumnBindingPath(cellInfo.Column) ?? cellInfo.Column.Header?.ToString();
        if (columnName is null || !rowView.Row.Table.Columns.Contains(columnName))
        {
            return false;
        }

        var text = FormatValue(rowView[columnName]);
        SetClipboardText(text);
        return true;
    }

    /// <summary>Copies every cell of the currently-selected row(s), tab-separated, one row per line.</summary>
    public static bool CopyRows(DataGrid grid)
    {
        if (grid.SelectedItems.Count == 0)
        {
            return false;
        }

        var columns = grid.Columns.OrderBy(c => c.DisplayIndex).ToList();
        var sb = new StringBuilder();

        foreach (var item in grid.SelectedItems)
        {
            if (item is not DataRowView rowView)
            {
                continue;
            }

            AppendRow(sb, rowView, columns);
        }

        if (sb.Length == 0)
        {
            return false;
        }

        SetClipboardText(sb.ToString());
        return true;
    }

    /// <summary>Copies the entire visible table (headers + all rows currently in view), tab-separated.</summary>
    public static bool CopyTable(DataGrid grid)
    {
        if (grid.ItemsSource is not DataView view || view.Count == 0)
        {
            return false;
        }

        var columns = grid.Columns.OrderBy(c => c.DisplayIndex).ToList();
        var sb = new StringBuilder();

        sb.AppendLine(string.Join('\t', columns.Select(c => c.Header?.ToString() ?? string.Empty)));

        foreach (DataRowView rowView in view)
        {
            AppendRow(sb, rowView, columns);
        }

        SetClipboardText(sb.ToString());
        return true;
    }

    private static void AppendRow(StringBuilder sb, DataRowView rowView, IReadOnlyList<DataGridColumn> columns)
    {
        var values = new string[columns.Count];
        for (var i = 0; i < columns.Count; i++)
        {
            var columnName = GetColumnBindingPath(columns[i]) ?? columns[i].Header?.ToString();
            values[i] = columnName is not null && rowView.Row.Table.Columns.Contains(columnName)
                ? FormatValue(rowView[columnName])
                : string.Empty;
        }

        sb.AppendLine(string.Join('\t', values));
    }

    private static string? GetColumnBindingPath(DataGridColumn column) =>
        column is DataGridBoundColumn { Binding: System.Windows.Data.Binding binding } ? binding.Path.Path : null;

    private static string FormatValue(object? value) => value switch
    {
        null or DBNull => string.Empty,
        byte[] bytes => Convert.ToBase64String(bytes),
        _ => value.ToString() ?? string.Empty
    };

    /// <summary>
    /// Setting clipboard text can intermittently throw <see cref="System.Runtime.InteropServices.COMException"/>
    /// if another process transiently holds the clipboard; retry briefly before giving up.
    /// </summary>
    private static void SetClipboardText(string text)
    {
        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                Clipboard.SetText(text);
                return;
            }
            catch (System.Runtime.InteropServices.COMException) when (attempt < maxAttempts)
            {
                System.Threading.Thread.Sleep(50);
            }
        }
    }
}
