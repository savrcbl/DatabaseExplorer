using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseExplorer.Core.Exceptions;
using DatabaseExplorer.Core.Interfaces;
using DatabaseExplorer.Core.Models;
using DatabaseExplorer.Helpers;

namespace DatabaseExplorer.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IAsyncDisposable
{
    private readonly IDatabaseProviderFactory _providerFactory;
    private readonly IExportService _exportService;
    private readonly IDialogService _dialogService;
    private readonly IConnectionProfileStore _profileStore;

    private IDatabaseConnection? _connection;
    private CancellationTokenSource? _selectionLoadCts;

    public MainViewModel(
        IDatabaseProviderFactory providerFactory,
        IExportService exportService,
        IDialogService dialogService,
        IConnectionProfileStore profileStore)
    {
        _providerFactory = providerFactory;
        _exportService = exportService;
        _dialogService = dialogService;
        _profileStore = profileStore;

        foreach (var provider in providerFactory.GetAllProviders())
        {
            AvailableProviders.Add(new ProviderOption(provider.ProviderType, provider.DisplayName, provider.ConnectionStringPlaceholder));
        }

        _selectedProviderType = AvailableProviders.Count > 0 ? AvailableProviders[0].Type : DatabaseProviderType.SqlServer;
        _selectedRowLimit = RowLimitOptions[1];

        _ = LoadSavedConnectionsAsync();
    }

    // ----- Navigation panel state -----------------------------------------------------

    public ObservableCollection<ProviderOption> AvailableProviders { get; } = new ObservableCollection<ProviderOption>();

    [ObservableProperty]
    private DatabaseProviderType _selectedProviderType;

    [ObservableProperty]
    private string _connectionString = string.Empty;

    public string ConnectionStringPlaceholder =>
        _providerFactory.GetProvider(SelectedProviderType).ConnectionStringPlaceholder;

    public ObservableCollection<TreeNodeViewModel> TreeNodes { get; } = new ObservableCollection<TreeNodeViewModel>();

    [ObservableProperty]
    private TreeNodeViewModel? _selectedNode;

    // ----- Saved connections ------------------------------------------------------------

    public ObservableCollection<SavedConnectionProfile> SavedConnections { get; } = new ObservableCollection<SavedConnectionProfile>();

    [ObservableProperty]
    private SavedConnectionProfile? _selectedSavedConnection;

    // ----- Query editor ------------------------------------------------------------------

    [ObservableProperty]
    private bool _isQueryEditorVisible;

    [ObservableProperty]
    private string _queryText = string.Empty;

    // ----- Row limit / paging ------------------------------------------------------------

    public ObservableCollection<RowLimitOption> RowLimitOptions { get; } = new ObservableCollection<RowLimitOption>
    {
        new RowLimitOption("100 rows", 100),
        new RowLimitOption("1,000 rows", 1000),
        new RowLimitOption("5,000 rows", 5000),
        new RowLimitOption("10,000 rows", 10000),
        new RowLimitOption("No limit", null)
    };

    [ObservableProperty]
    private RowLimitOption _selectedRowLimit;

    [ObservableProperty]
    private int _loadedRowCount;

    // ----- Main content state ----------------------------------------------------------

    [ObservableProperty]
    private DataView? _currentDataView;

    [ObservableProperty]
    private string _filterText = string.Empty;

    // ----- Status / busy state ----------------------------------------------------------

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = "Not connected. Select a provider and enter a connection string to begin.";

    [ObservableProperty]
    private AppConnectionState _connectionState = AppConnectionState.Disconnected;

    [ObservableProperty]
    private string? _connectedProviderDisplayName;

    [ObservableProperty]
    private string? _connectedServerName;

    [ObservableProperty]
    private string? _connectedDatabaseName;

    [ObservableProperty]
    private string? _currentObjectName;

    [ObservableProperty]
    private long _totalRowCount;

    [ObservableProperty]
    private string _scanStatusText = "Not connected.";

    public bool IsConnected => ConnectionState is AppConnectionState.Connected
        or AppConnectionState.Scanning
        or AppConnectionState.Ready;

    public string RowsSummaryText => IsRowCountCapped
        ? $"{LoadedRowCount:N0} of {TotalRowCount:N0} row(s)"
        : $"{LoadedRowCount:N0} row(s)";

    public bool IsRowCountCapped => LoadedRowCount > 0 && LoadedRowCount < TotalRowCount;

    // ----- Property change hooks (keep computed state & command availability in sync) --

    partial void OnSelectedProviderTypeChanged(DatabaseProviderType value) =>
        OnPropertyChanged(nameof(ConnectionStringPlaceholder));

    partial void OnConnectionStringChanged(string value)
    {
        ConnectCommand.NotifyCanExecuteChanged();
        SaveConnectionCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value) => RefreshCommandStates();

    partial void OnConnectionStateChanged(AppConnectionState value) => RefreshCommandStates();

    partial void OnCurrentDataViewChanged(DataView? value) => RefreshCommandStates();

    partial void OnSelectedNodeChanged(TreeNodeViewModel? value)
    {
        RefreshCommandStates();
        _ = LoadSelectedNodeDataAsync();
    }

    partial void OnFilterTextChanged(string value) => ApplyFilter();

    partial void OnQueryTextChanged(string value) => RunQueryCommand.NotifyCanExecuteChanged();

    partial void OnLoadedRowCountChanged(int value)
    {
        OnPropertyChanged(nameof(RowsSummaryText));
        OnPropertyChanged(nameof(IsRowCountCapped));
        LoadAllRowsCommand.NotifyCanExecuteChanged();
    }

    partial void OnTotalRowCountChanged(long value)
    {
        OnPropertyChanged(nameof(RowsSummaryText));
        OnPropertyChanged(nameof(IsRowCountCapped));
        LoadAllRowsCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedSavedConnectionChanged(SavedConnectionProfile? value)
    {
        DeleteSavedConnectionCommand.NotifyCanExecuteChanged();

        if (value is null)
        {
            return;
        }

        SelectedProviderType = value.ProviderType;
        ConnectionString = value.ConnectionString;
    }

    private void RefreshCommandStates()
    {
        OnPropertyChanged(nameof(IsConnected));
        ConnectCommand.NotifyCanExecuteChanged();
        DisconnectCommand.NotifyCanExecuteChanged();
        StartScanCommand.NotifyCanExecuteChanged();
        ReloadCommand.NotifyCanExecuteChanged();
        RefreshCurrentObjectCommand.NotifyCanExecuteChanged();
        SmartRefreshCommand.NotifyCanExecuteChanged();
        RunQueryCommand.NotifyCanExecuteChanged();
        LoadAllRowsCommand.NotifyCanExecuteChanged();
        ExportCommand.NotifyCanExecuteChanged();
        CopyCellCommand.NotifyCanExecuteChanged();
        CopyRowCommand.NotifyCanExecuteChanged();
        CopyTableCommand.NotifyCanExecuteChanged();
        ClearCommand.NotifyCanExecuteChanged();
    }

    // ----- Connect / Disconnect ----------------------------------------------------------

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync(CancellationToken token) => await ConnectInternalAsync(token).ConfigureAwait(true);

    private bool CanConnect() => !IsBusy && !IsConnected && !string.IsNullOrWhiteSpace(ConnectionString);

    private async Task<bool> ConnectInternalAsync(CancellationToken token)
    {
        var provider = _providerFactory.GetProvider(SelectedProviderType);

        var validationError = provider.ValidateConnectionString(ConnectionString);
        if (validationError is not null)
        {
            _dialogService.ShowWarning("Invalid Connection String", validationError);
            return false;
        }

        IsBusy = true;
        ConnectionState = AppConnectionState.Connecting;
        StatusMessage = $"Connecting to {provider.DisplayName}...";

        var connection = provider.CreateConnection(ConnectionString);

        try
        {
            await connection.OpenAsync(token).ConfigureAwait(true);

            _connection = connection;
            ConnectedProviderDisplayName = provider.DisplayName;
            ConnectedServerName = connection.ServerName;
            ConnectedDatabaseName = connection.DatabaseName;
            ConnectionState = AppConnectionState.Connected;
            ScanStatusText = "Connected. Not yet scanned.";
            StatusMessage = $"Connected to '{connection.DatabaseName}' on '{connection.ServerName}'.";
            return true;
        }
        catch (OperationCanceledException)
        {
            await connection.DisposeAsync().ConfigureAwait(true);
            ConnectionState = AppConnectionState.Disconnected;
            StatusMessage = "Connection attempt cancelled.";
            return false;
        }
        catch (DatabaseConnectionException ex)
        {
            await connection.DisposeAsync().ConfigureAwait(true);
            ConnectionState = AppConnectionState.Disconnected;
            StatusMessage = ex.Message;
            _dialogService.ShowError("Connection Failed", ex.Message);
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanDisconnect))]
    private async Task DisconnectAsync()
    {
        IsBusy = true;
        try
        {
            if (_connection is not null)
            {
                await _connection.CloseAsync().ConfigureAwait(true);
                await _connection.DisposeAsync().ConfigureAwait(true);
                _connection = null;
            }
        }
        finally
        {
            TreeNodes.Clear();
            SelectedNode = null;
            CurrentDataView = null;
            CurrentObjectName = null;
            TotalRowCount = 0;
            LoadedRowCount = 0;
            FilterText = string.Empty;
            ConnectedServerName = null;
            ConnectedDatabaseName = null;
            ConnectedProviderDisplayName = null;
            ConnectionState = AppConnectionState.Disconnected;
            ScanStatusText = "Not connected.";
            StatusMessage = "Disconnected.";
            IsBusy = false;
        }
    }

    private bool CanDisconnect() => !IsBusy && IsConnected;

    // ----- Scan / Reload -------------------------------------------------------------------

    [RelayCommand(CanExecute = nameof(CanStartScan))]
    private async Task StartScanAsync(CancellationToken token)
    {
        if (!IsConnected)
        {
            var connected = await ConnectInternalAsync(token).ConfigureAwait(true);
            if (!connected)
            {
                return;
            }
        }

        await ScanDatabaseAsync(token).ConfigureAwait(true);
    }

    private bool CanStartScan() => !IsBusy && !string.IsNullOrWhiteSpace(ConnectionString);

    [RelayCommand(CanExecute = nameof(CanReload))]
    private async Task ReloadAsync(CancellationToken token)
    {
        if (!IsConnected || _connection is null)
        {
            return;
        }

        var path = GetNodePath(SelectedNode);

        await ScanDatabaseAsync(token).ConfigureAwait(true);

        var restored = path.Count > 0 ? FindNodeByPath(TreeNodes, path) : null;
        if (restored is not null)
        {
            ExpandAncestors(restored);
            restored.IsSelected = true;
            SelectedNode = restored;
        }
    }

    private bool CanReload() => !IsBusy && IsConnected;

    private async Task ScanDatabaseAsync(CancellationToken token)
    {
        if (_connection is null)
        {
            return;
        }

        IsBusy = true;
        ConnectionState = AppConnectionState.Scanning;
        ScanStatusText = "Scanning...";
        StatusMessage = "Scanning database objects...";

        try
        {
            var schemas = await _connection.QueryService.GetSchemasAsync(token).ConfigureAwait(true);
            var rootNode = new TreeNodeViewModel(_connection.DatabaseName ?? "Database", DatabaseObjectType.Database)
            {
                IsExpanded = true
            };

            foreach (var schema in schemas)
            {
                token.ThrowIfCancellationRequested();
                var schemaNode = new TreeNodeViewModel(schema.Name, DatabaseObjectType.Schema, rootNode);

                var tables = await _connection.QueryService.GetTablesAsync(schema.Name, token).ConfigureAwait(true);
                if (tables.Count > 0)
                {
                    var tablesFolder = new TreeNodeViewModel("Tables", DatabaseObjectType.TablesFolder, schemaNode);
                    foreach (var table in tables)
                    {
                        tablesFolder.Children.Add(new TreeNodeViewModel(table.Name, DatabaseObjectType.Table, tablesFolder, table));
                    }

                    schemaNode.Children.Add(tablesFolder);
                }

                var views = await _connection.QueryService.GetViewsAsync(schema.Name, token).ConfigureAwait(true);
                if (views.Count > 0)
                {
                    var viewsFolder = new TreeNodeViewModel("Views", DatabaseObjectType.ViewsFolder, schemaNode);
                    foreach (var view in views)
                    {
                        viewsFolder.Children.Add(new TreeNodeViewModel(view.Name, DatabaseObjectType.View, viewsFolder, view));
                    }

                    schemaNode.Children.Add(viewsFolder);
                }

                var procedures = await _connection.QueryService.GetProceduresAsync(schema.Name, token).ConfigureAwait(true);
                if (procedures.Count > 0)
                {
                    var proceduresFolder = new TreeNodeViewModel("Procedures", DatabaseObjectType.ProceduresFolder, schemaNode);
                    foreach (var procedure in procedures)
                    {
                        proceduresFolder.Children.Add(new TreeNodeViewModel(procedure.Name, DatabaseObjectType.Procedure, proceduresFolder, procedure));
                    }

                    schemaNode.Children.Add(proceduresFolder);
                }

                if (schemaNode.Children.Count > 0)
                {
                    rootNode.Children.Add(schemaNode);
                }
            }

            TreeNodes.Clear();
            TreeNodes.Add(rootNode);

            ConnectionState = AppConnectionState.Ready;
            ScanStatusText = $"Scan complete: {rootNode.Children.Count:N0} schema(s).";
            StatusMessage = "Scan complete. Select a table or view to browse its data.";
        }
        catch (OperationCanceledException)
        {
            ConnectionState = AppConnectionState.Connected;
            StatusMessage = "Scan cancelled.";
        }
        catch (DatabaseQueryException ex)
        {
            ConnectionState = AppConnectionState.Connected;
            StatusMessage = ex.Message;
            _dialogService.ShowError("Scan Failed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ----- Selection-driven data / metadata loading ---------------------------------------

    private async Task LoadSelectedNodeDataAsync()
    {
        _selectionLoadCts?.Cancel();
        _selectionLoadCts?.Dispose();
        _selectionLoadCts = new CancellationTokenSource();
        var token = _selectionLoadCts.Token;

        var node = SelectedNode;
        if (node is null || _connection is null)
        {
            return;
        }

        try
        {
            switch (node.NodeType)
            {
                case DatabaseObjectType.Table:
                case DatabaseObjectType.View:
                    await LoadObjectDataAsync(node, token).ConfigureAwait(true);
                    break;

                case DatabaseObjectType.Procedure:
                    await LoadProcedureMetadataAsync(node, token).ConfigureAwait(true);
                    break;

                default:
                    ClearGridForNavigation(node);
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            // A newer selection superseded this load; nothing to do.
        }
        catch (DatabaseQueryException ex)
        {
            StatusMessage = ex.Message;
            _dialogService.ShowError("Query Failed", ex.Message);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Unexpected error: {ex.Message}";
            _dialogService.ShowError("Unexpected Error", ex.Message);
        }
    }

    private async Task LoadObjectDataAsync(TreeNodeViewModel node, CancellationToken token, bool ignoreRowLimit = false)
    {
        if (_connection is null || node.SchemaName is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var effectiveLimit = ignoreRowLimit ? null : SelectedRowLimit.Value;

            long actualTotal;
            try
            {
                actualTotal = await _connection.QueryService
                    .GetRowCountAsync(node.SchemaName, node.Name, token)
                    .ConfigureAwait(true);
            }
            catch (DatabaseQueryException)
            {
                actualTotal = -1;
            }

            var progress = new Progress<string>(message => StatusMessage = message);
            var result = await _connection.QueryService
                .GetObjectDataAsync(node.SchemaName, node.Name, effectiveLimit, progress, token)
                .ConfigureAwait(true);

            var kind = node.NodeType == DatabaseObjectType.View ? "view" : "table";
            CurrentDataView = result.Data.DefaultView;
            CurrentObjectName = $"{result.SourceName} ({kind})";
            LoadedRowCount = result.RowCount;
            TotalRowCount = actualTotal >= 0 ? actualTotal : result.RowCount;
            FilterText = string.Empty;

            StatusMessage = IsRowCountCapped
                ? $"Loaded {result.RowCount:N0} of {TotalRowCount:N0} row(s) from {result.SourceName} in {result.Elapsed.TotalMilliseconds:N0} ms — click \"Load All\" to fetch the rest."
                : $"Loaded {result.RowCount:N0} row(s), {result.ColumnCount} column(s) from {result.SourceName} in {result.Elapsed.TotalMilliseconds:N0} ms.";
        }
        finally
        {
            IsBusy = false;
        }
    }
    private async Task LoadProcedureMetadataAsync(TreeNodeViewModel node, CancellationToken token)
    {
        if (_connection is null || node.SchemaName is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            StatusMessage = $"Loading metadata for {node.SchemaName}.{node.Name}...";

            var table = await Task.Run(() =>
            {
                var dt = new DataTable(node.Name);
                dt.Columns.Add("Property", typeof(string));
                dt.Columns.Add("Value", typeof(string));
                dt.Rows.Add("Schema", node.SchemaName);
                dt.Rows.Add("Name", node.Name);
                dt.Rows.Add("Object type", "Stored Procedure");
                dt.Rows.Add("Provider", _connection.ProviderType.ToString());
                return dt;
            }, token).ConfigureAwait(true);

            CurrentDataView = table.DefaultView;
            CurrentObjectName = $"{node.SchemaName}.{node.Name} (procedure)";
            LoadedRowCount = table.Rows.Count;
            TotalRowCount = table.Rows.Count;
            FilterText = string.Empty;
            StatusMessage = $"Showing metadata for procedure {node.SchemaName}.{node.Name}.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ClearGridForNavigation(TreeNodeViewModel node)
    {
        CurrentDataView = null;
        CurrentObjectName = null;
        TotalRowCount = 0;
        LoadedRowCount = 0;
        FilterText = string.Empty;

        StatusMessage = node.NodeType switch
        {
            DatabaseObjectType.Database => "Select a schema to browse its contents.",
            DatabaseObjectType.Schema => $"Schema '{node.Name}' selected — expand it to see tables, views, and procedures.",
            DatabaseObjectType.TablesFolder => $"{node.Children.Count:N0} table(s) in schema '{node.Parent?.Name}'.",
            DatabaseObjectType.ViewsFolder => $"{node.Children.Count:N0} view(s) in schema '{node.Parent?.Name}'.",
            DatabaseObjectType.ProceduresFolder => $"{node.Children.Count:N0} procedure(s) in schema '{node.Parent?.Name}'.",
            _ => "Select a table or view to browse its data."
        };
    }

    [RelayCommand(CanExecute = nameof(CanRefreshCurrentObject))]
    private async Task RefreshCurrentObjectAsync(CancellationToken token)
    {
        var node = SelectedNode;
        if (node is null)
        {
            return;
        }

        switch (node.NodeType)
        {
            case DatabaseObjectType.Table:
            case DatabaseObjectType.View:
                await LoadObjectDataAsync(node, token).ConfigureAwait(true);
                break;
            case DatabaseObjectType.Procedure:
                await LoadProcedureMetadataAsync(node, token).ConfigureAwait(true);
                break;
        }
    }

    private bool CanRefreshCurrentObject() => !IsBusy && IsConnected && SelectedNode is not null;

    [RelayCommand(CanExecute = nameof(CanLoadAllRows))]
    private async Task LoadAllRowsAsync(CancellationToken token)
    {
        if (SelectedNode is { IsDataObject: true } node)
        {
            await LoadObjectDataAsync(node, token, ignoreRowLimit: true).ConfigureAwait(true);
        }
    }

    private bool CanLoadAllRows() => !IsBusy && IsRowCountCapped;

    // ----- Query editor --------------------------------------------------------------------

    [RelayCommand]
    private void ToggleQueryEditor() => IsQueryEditorVisible = !IsQueryEditorVisible;

    [RelayCommand(CanExecute = nameof(CanRunQuery))]
    private async Task RunQueryAsync(CancellationToken token)
    {
        if (_connection is null || string.IsNullOrWhiteSpace(QueryText))
        {
            return;
        }

        IsBusy = true;
        try
        {
            var progress = new Progress<string>(message => StatusMessage = message);
            var result = await _connection.QueryService
                .ExecuteQueryAsync(QueryText, progress, token)
                .ConfigureAwait(true);

            SelectedNode = null;

            CurrentDataView = result.Data.DefaultView;
            CurrentObjectName = result.SourceName;
            LoadedRowCount = result.RowCount;
            TotalRowCount = result.RowCount;
            FilterText = string.Empty;
            StatusMessage = $"Query completed — {result.RowCount:N0} row(s), {result.ColumnCount} column(s) in {result.Elapsed.TotalMilliseconds:N0} ms.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Query cancelled.";
        }
        catch (DatabaseQueryException ex)
        {
            StatusMessage = ex.Message;
            _dialogService.ShowError("Query Failed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanRunQuery() => !IsBusy && IsConnected && !string.IsNullOrWhiteSpace(QueryText);

    [RelayCommand(CanExecute = nameof(CanSmartRefresh))]
    private async Task SmartRefreshAsync(CancellationToken token)
    {
        if (IsQueryEditorVisible && !string.IsNullOrWhiteSpace(QueryText))
        {
            await RunQueryAsync(token).ConfigureAwait(true);
        }
        else
        {
            await RefreshCurrentObjectAsync(token).ConfigureAwait(true);
        }
    }

    private bool CanSmartRefresh() => !IsBusy && IsConnected;

    // ----- Saved connections -----------------------------------------------------------------

    private async Task LoadSavedConnectionsAsync()
    {
        try
        {
            var profiles = await _profileStore.LoadAllAsync().ConfigureAwait(true);

            SavedConnections.Clear();
            foreach (var profile in profiles.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
            {
                SavedConnections.Add(profile);
            }
        }
        catch
        {
        }
    }

    [RelayCommand(CanExecute = nameof(CanSaveConnection))]
    private async Task SaveConnectionAsync()
    {
        var provider = _providerFactory.GetProvider(SelectedProviderType);
        var name = _dialogService.ShowTextInput(
            "Save Connection",
            $"Enter a name for this {provider.DisplayName} connection:",
            SelectedSavedConnection?.Name ?? string.Empty);

        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var profile = new SavedConnectionProfile(name, SelectedProviderType, ConnectionString);

        try
        {
            await _profileStore.SaveAsync(profile).ConfigureAwait(true);

            var existingIndex = -1;
            for (var i = 0; i < SavedConnections.Count; i++)
            {
                if (string.Equals(SavedConnections[i].Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    existingIndex = i;
                    break;
                }
            }

            if (existingIndex >= 0)
            {
                SavedConnections[existingIndex] = profile;
            }
            else
            {
                SavedConnections.Add(profile);
            }

            SelectedSavedConnection = profile;
            StatusMessage = $"Connection saved as \"{name}\".";
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("Save Failed", $"Could not save the connection: {ex.Message}");
        }
    }

    private bool CanSaveConnection() => !string.IsNullOrWhiteSpace(ConnectionString);

    [RelayCommand(CanExecute = nameof(CanDeleteSavedConnection))]
    private async Task DeleteSavedConnectionAsync()
    {
        var profile = SelectedSavedConnection;
        if (profile is null)
        {
            return;
        }

        if (!_dialogService.ShowConfirmation("Delete Connection", $"Delete the saved connection \"{profile.Name}\"?"))
        {
            return;
        }

        try
        {
            await _profileStore.DeleteAsync(profile.Name).ConfigureAwait(true);
            SavedConnections.Remove(profile);
            SelectedSavedConnection = null;
            StatusMessage = $"Deleted saved connection \"{profile.Name}\".";
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("Delete Failed", $"Could not delete the connection: {ex.Message}");
        }
    }

    private bool CanDeleteSavedConnection() => SelectedSavedConnection is not null;

    // ----- Filtering -----------------------------------------------------------------------

    private void ApplyFilter()
    {
        if (CurrentDataView is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(FilterText))
        {
            CurrentDataView.RowFilter = string.Empty;
            return;
        }

        try
        {
            var columns = CurrentDataView.Table!.Columns.Cast<DataColumn>().ToList();
            var escaped = FilterText.Replace("'", "''");
            var clauses = columns.Select(c =>
                $"CONVERT([{c.ColumnName.Replace("]", "]]")}], 'System.String') LIKE '%{escaped}%'");

            CurrentDataView.RowFilter = string.Join(" OR ", clauses);
        }
        catch (Exception)
        {
        }
    }

    // ----- Export --------------------------------------------------------------------------

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportAsync(string formatArgument, CancellationToken token)
    {
        if (CurrentDataView is null)
        {
            return;
        }

        var format = string.Equals(formatArgument, "excel", StringComparison.OrdinalIgnoreCase)
            ? ExportFormat.Excel
            : ExportFormat.Csv;

        var extension = format == ExportFormat.Excel ? "xlsx" : "csv";
        var filter = format == ExportFormat.Excel
            ? "Excel Workbook (*.xlsx)|*.xlsx"
            : "CSV File (*.csv)|*.csv";

        var baseName = (CurrentObjectName ?? "export").Split(' ')[0].Replace('.', '_');
        var path = _dialogService.ShowSaveFileDialog(filter, $"{baseName}.{extension}");
        if (path is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var progress = new Progress<double>(p => StatusMessage = $"Exporting... {p:P0}");
            await _exportService
                .ExportAsync(CurrentDataView, path, format, baseName, progress, token)
                .ConfigureAwait(true);

            StatusMessage = $"Exported to {path}.";
            _dialogService.ShowInfo("Export Complete", $"Data was exported successfully to:\n{path}");
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Export cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Export failed.";
            _dialogService.ShowError("Export Failed", $"Could not export data: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanExport() => !IsBusy && CurrentDataView is not null;


    [RelayCommand(CanExecute = nameof(CanUseGrid))]
    private void CopyCell(DataGrid? grid)
    {
        if (grid is null)
        {
            return;
        }

        StatusMessage = ClipboardHelper.CopyCurrentCell(grid) ? "Cell copied to clipboard." : "No cell selected.";
    }

    [RelayCommand(CanExecute = nameof(CanUseGrid))]
    private void CopyRow(DataGrid? grid)
    {
        if (grid is null)
        {
            return;
        }

        StatusMessage = ClipboardHelper.CopyRows(grid) ? "Row(s) copied to clipboard." : "No row selected.";
    }

    [RelayCommand(CanExecute = nameof(CanUseGrid))]
    private void CopyTable(DataGrid? grid)
    {
        if (grid is null)
        {
            return;
        }

        StatusMessage = ClipboardHelper.CopyTable(grid) ? "Table copied to clipboard." : "No data to copy.";
    }

    private bool CanUseGrid(DataGrid? grid) => CurrentDataView is not null;

    // ----- Clear ---------------------------------------------------------------------------

    [RelayCommand(CanExecute = nameof(CanClear))]
    private void Clear()
    {
        CurrentDataView = null;
        CurrentObjectName = null;
        TotalRowCount = 0;
        LoadedRowCount = 0;
        FilterText = string.Empty;
        StatusMessage = "Grid cleared.";
    }

    private bool CanClear() => CurrentDataView is not null;

    // ----- Tree path helpers (used by Reload to preserve selection) ------------------------

    private static List<(DatabaseObjectType Type, string Name)> GetNodePath(TreeNodeViewModel? node)
    {
        var path = new List<(DatabaseObjectType, string)>();
        while (node is not null)
        {
            path.Insert(0, (node.NodeType, node.Name));
            node = node.Parent;
        }

        return path;
    }

    private static TreeNodeViewModel? FindNodeByPath(
        IEnumerable<TreeNodeViewModel> roots,
        IReadOnlyList<(DatabaseObjectType Type, string Name)> path)
    {
        if (path.Count == 0)
        {
            return null;
        }

        var current = roots.FirstOrDefault(n => n.NodeType == path[0].Type && n.Name == path[0].Name);
        for (var i = 1; i < path.Count && current is not null; i++)
        {
            var step = path[i];
            current = current.Children.FirstOrDefault(c => c.NodeType == step.Type && c.Name == step.Name);
        }

        return current;
    }

    private static void ExpandAncestors(TreeNodeViewModel node)
    {
        var current = node.Parent;
        while (current is not null)
        {
            current.IsExpanded = true;
            current = current.Parent;
        }
    }

    // ----- Shutdown ------------------------------------------------------------------------

    public async ValueTask DisposeAsync()
    {
        if (_selectionLoadCts is not null)
        {
            _selectionLoadCts.Cancel();
            _selectionLoadCts.Dispose();
            _selectionLoadCts = null;
        }

        if (_connection is null)
        {
            return;
        }

        try
        {
            await _connection.CloseAsync().ConfigureAwait(false);
        }
        catch
        {
            // Best-effort close; we dispose regardless below.
        }
        finally
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
            _connection = null;
        }
    }
}
