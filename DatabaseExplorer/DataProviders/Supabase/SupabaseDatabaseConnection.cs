using DatabaseExplorer.Core.Exceptions;
using DatabaseExplorer.Core.Interfaces;
using DatabaseExplorer.Core.Models;
using System.Data;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace DatabaseExplorer.DataProviders.Supabase;

public sealed class SupabaseDatabaseConnection : IDatabaseConnection
{
    private readonly SupabaseConnectionInfo _info;
    private HttpClient? _httpClient;
    private ConnectionState _state = ConnectionState.Closed;
    private bool _disposed;

    internal JsonNode? OpenApiSpec { get; private set; }

    internal HttpClient HttpClient =>
        _httpClient ?? throw new InvalidOperationException("Not connected.");

    public SupabaseDatabaseConnection(string connectionString)
    {
        if (!SupabaseConnectionInfo.TryParse(connectionString, out var info, out var error))
        {
            throw new DatabaseConnectionException(
                error ?? "Invalid Supabase connection string.",
                DatabaseConnectionFailureReason.InvalidConnectionString);
        }

        _info = info!;
        QueryService = new SupabaseQueryService(this);
    }

    public DatabaseProviderType ProviderType => DatabaseProviderType.Supabase;

    public ConnectionState State => _state;

    public string? DatabaseName => _state == ConnectionState.Open ? "public" : null;

    public string? ServerName => _state == ConnectionState.Open ? new Uri(_info.Url).Host : null;

    public IDatabaseQueryService QueryService { get; }

    public async Task OpenAsync(CancellationToken cancellationToken = default)
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri(_info.Url + "/"),
            Timeout = TimeSpan.FromSeconds(20)
        };
        client.DefaultRequestHeaders.Add("apikey", _info.ApiKey);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _info.ApiKey);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            using var response = await client.GetAsync("rest/v1/", cancellationToken).ConfigureAwait(false);

            if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            {
                client.Dispose();
                throw new DatabaseConnectionException(
                    "The API key was rejected. Check your Supabase publishable/secret key.",
                    DatabaseConnectionFailureReason.AuthenticationFailed);
            }

            if (!response.IsSuccessStatusCode)
            {
                client.Dispose();
                throw new DatabaseConnectionException(
                    $"Supabase responded with HTTP {(int)response.StatusCode} {response.ReasonPhrase}.",
                    DatabaseConnectionFailureReason.Unknown);
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            OpenApiSpec = JsonNode.Parse(body);

            _httpClient = client;
            _state = ConnectionState.Open;
        }
        catch (OperationCanceledException)
        {
            client.Dispose();
            throw;
        }
        catch (DatabaseConnectionException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            client.Dispose();
            throw new DatabaseConnectionException(
                $"Could not reach '{_info.Url}': {ex.Message}",
                DatabaseConnectionFailureReason.ServerNotFound,
                ex);
        }
        catch (Exception ex)
        {
            client.Dispose();
            throw new DatabaseConnectionException(
                $"Could not connect to Supabase: {ex.Message}",
                DatabaseConnectionFailureReason.Unknown,
                ex);
        }
    }

    public Task CloseAsync()
    {
        _httpClient?.Dispose();
        _httpClient = null;
        OpenApiSpec = null;
        _state = ConnectionState.Closed;
        return Task.CompletedTask;
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            client.DefaultRequestHeaders.Add("apikey", _info.ApiKey);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _info.ApiKey);

            using var response = await client.GetAsync($"{_info.Url}/rest/v1/", cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await CloseAsync().ConfigureAwait(false);
    }
}