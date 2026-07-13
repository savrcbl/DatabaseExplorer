using System.Data;

namespace DatabaseExplorer.Core.Interfaces;

/// <summary>The file format to export grid data to.</summary>
public enum ExportFormat
{
    Csv,
    Excel
}

/// <summary>
/// Exports the currently displayed data to disk in a user-chosen format.
/// </summary>
public interface IExportService
{
    /// <summary>
    /// Writes <paramref name="data"/> (respecting its current sort/filter view) to
    /// <paramref name="filePath"/> in the given <paramref name="format"/>.
    /// </summary>
    /// <param name="data">The view to export, including only currently-visible rows/order.</param>
    /// <param name="filePath">The destination file path.</param>
    /// <param name="format">The target file format.</param>
    /// <param name="sheetName">Worksheet name to use when exporting to Excel.</param>
    /// <param name="progress">Optional 0.0–1.0 progress reporter.</param>
    /// <param name="cancellationToken">Allows the export to be cancelled.</param>
    Task ExportAsync(
        DataView data,
        string filePath,
        ExportFormat format,
        string sheetName,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}
