using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace DatabaseExplorer.Core.Interfaces;

public enum ExportFormat
{
    Csv,
    Excel
}

public interface IExportService
{
    Task ExportAsync(
        DataView data,
        string filePath,
        ExportFormat format,
        string sheetName,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}
