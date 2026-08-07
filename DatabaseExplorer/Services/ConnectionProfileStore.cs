using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DatabaseExplorer.Core.Interfaces;
using DatabaseExplorer.Core.Models;

namespace DatabaseExplorer.Services;

/// <summary>
/// <see cref="IConnectionProfileStore"/> implementation that persists profiles as JSON
/// under <c>%AppData%\DatabaseExplorer\connections.json</c>. Connection strings are
/// encrypted at rest using Windows DPAPI (<see cref="ProtectedData"/>,
/// <see cref="DataProtectionScope.CurrentUser"/>), so the file is unreadable outside the
/// current Windows user account on this machine — meaningfully better than plaintext,
/// though this is convenience-level protection for a local desktop tool, not a
/// replacement for a real secrets manager in a team/production setting.
/// </summary>
public sealed class ConnectionProfileStore : IConnectionProfileStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    public ConnectionProfileStore()
    {
        var appDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DatabaseExplorer");

        Directory.CreateDirectory(appDataFolder);
        _filePath = Path.Combine(appDataFolder, "connections.json");
    }

    public async Task<IReadOnlyList<SavedConnectionProfile>> LoadAllAsync(CancellationToken cancellationToken = default)
    {
        var records = await ReadRecordsAsync(cancellationToken).ConfigureAwait(false);
        var profiles = new List<SavedConnectionProfile>(records.Count);

        foreach (var record in records)
        {
            var connectionString = TryDecrypt(record.ProtectedConnectionString);
            if (connectionString is null)
            {
                continue;
            }

            profiles.Add(new SavedConnectionProfile(record.Name, record.ProviderType, connectionString));
        }

        return profiles;
    }

    public async Task SaveAsync(SavedConnectionProfile profile, CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var records = await ReadRecordsAsync(cancellationToken).ConfigureAwait(false);
            records.RemoveAll(r => string.Equals(r.Name, profile.Name, StringComparison.OrdinalIgnoreCase));

            records.Add(new StoredRecord
            {
                Name = profile.Name,
                ProviderType = profile.ProviderType,
                ProtectedConnectionString = Encrypt(profile.ConnectionString)
            });

            await WriteRecordsAsync(records, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task DeleteAsync(string name, CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var records = await ReadRecordsAsync(cancellationToken).ConfigureAwait(false);
            var removed = records.RemoveAll(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));

            if (removed > 0)
            {
                await WriteRecordsAsync(records, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task<List<StoredRecord>> ReadRecordsAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            var records = await JsonSerializer.DeserializeAsync<List<StoredRecord>>(
                stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
            return records ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private async Task WriteRecordsAsync(List<StoredRecord> records, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, records, SerializerOptions, cancellationToken).ConfigureAwait(false);
    }

    private static string Encrypt(string plaintext)
    {
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var protectedBytes = ProtectedData.Protect(plaintextBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    private static string? TryDecrypt(string protectedBase64)
    {
        try
        {
            var protectedBytes = Convert.FromBase64String(protectedBase64);
            var plaintextBytes = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plaintextBytes);
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>The on-disk JSON shape. Kept private/internal to this class deliberately
    /// — callers only ever see the decrypted <see cref="SavedConnectionProfile"/>.</summary>
    private sealed class StoredRecord
    {
        public required string Name { get; init; }
        public required DatabaseProviderType ProviderType { get; init; }
        public required string ProtectedConnectionString { get; init; }
    }
}
