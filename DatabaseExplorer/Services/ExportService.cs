using System.Data;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using DatabaseExplorer.Core.Interfaces;

namespace DatabaseExplorer.Services;

/// <summary>
/// <see cref="IExportService"/> implementation supporting CSV (hand-written, RFC 4180
/// compliant) and Excel (via ClosedXML) export of the currently displayed grid data.
/// </summary>
public sealed class ExportService : IExportService
{
    public async Task ExportAsync(
        DataView data,
        string filePath,
        ExportFormat format,
        string sheetName,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        switch (format)
        {
            case ExportFormat.Csv:
                await ExportToCsvAsync(data, filePath, progress, cancellationToken).ConfigureAwait(false);
                break;

            case ExportFormat.Excel:
                await ExportToExcelAsync(data, filePath, sheetName, progress, cancellationToken).ConfigureAwait(false);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported export format.");
        }
    }

    private static async Task ExportToCsvAsync(
        DataView data,
        string filePath,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            using var writer = new StreamWriter(filePath, append: false, Encoding.UTF8);

            var columnCount = data.Table!.Columns.Count;
            var headers = data.Table.Columns.Cast<DataColumn>().Select(c => EscapeCsvField(c.ColumnName));
            writer.WriteLine(string.Join(',', headers));

            var totalRows = data.Count;
            for (var rowIndex = 0; rowIndex < totalRows; rowIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var row = data[rowIndex];
                var fields = new string[columnCount];
                for (var col = 0; col < columnCount; col++)
                {
                    fields[col] = EscapeCsvField(FormatValue(row[col]));
                }

                writer.WriteLine(string.Join(',', fields));

                if (rowIndex % 500 == 0)
                {
                    progress?.Report(totalRows == 0 ? 1.0 : (double)rowIndex / totalRows);
                }
            }

            progress?.Report(1.0);
        }, cancellationToken).ConfigureAwait(false);
    }

    private static async Task ExportToExcelAsync(
        DataView data,
        string filePath,
        string sheetName,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            using var workbook = new XLWorkbook();
            var safeSheetName = string.IsNullOrWhiteSpace(sheetName) ? "Data" : SanitizeSheetName(sheetName);
            var worksheet = workbook.Worksheets.Add(safeSheetName);

            var columnCount = data.Table!.Columns.Count;
            for (var col = 0; col < columnCount; col++)
            {
                worksheet.Cell(1, col + 1).Value = data.Table.Columns[col].ColumnName;
                worksheet.Cell(1, col + 1).Style.Font.Bold = true;
            }

            var totalRows = data.Count;
            for (var rowIndex = 0; rowIndex < totalRows; rowIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var row = data[rowIndex];
                for (var col = 0; col < columnCount; col++)
                {
                    var cell = worksheet.Cell(rowIndex + 2, col + 1);
                    SetCellValue(cell, row[col]);
                }

                if (rowIndex % 500 == 0)
                {
                    progress?.Report(totalRows == 0 ? 1.0 : (double)rowIndex / totalRows);
                }
            }

            worksheet.SheetView.FreezeRows(1);
            worksheet.Columns().AdjustToContents(1, Math.Min(totalRows + 1, 2000));

            workbook.SaveAs(filePath);
            progress?.Report(1.0);
        }, cancellationToken).ConfigureAwait(false);
    }

    private static void SetCellValue(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null or DBNull:
                cell.Value = string.Empty;
                break;
            case bool b:
                cell.Value = b;
                break;
            case DateTime dt:
                cell.Value = dt;
                break;
            case sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal:
                cell.Value = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                break;
            default:
                cell.Value = FormatValue(value);
                break;
        }
    }

    private static string FormatValue(object? value) => value switch
    {
        null or DBNull => string.Empty,
        DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture),
        byte[] bytes => Convert.ToBase64String(bytes),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };

    /// <summary>Escapes a single CSV field per RFC 4180.</summary>
    private static string EscapeCsvField(string field)
    {
        if (field.IndexOfAny([',', '"', '\n', '\r']) < 0)
        {
            return field;
        }

        return $"\"{field.Replace("\"", "\"\"")}\"";
    }

    private static string SanitizeSheetName(string name)
    {
        var invalidChars = new[] { '\\', '/', '*', '?', ':', '[', ']' };
        var sanitized = new string(name.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
        return sanitized.Length > 31 ? sanitized[..31] : sanitized;
    }
}
