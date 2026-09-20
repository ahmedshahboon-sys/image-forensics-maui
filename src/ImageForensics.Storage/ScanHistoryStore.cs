using System.Text.Json;
using ImageForensics.Core.Abstractions;
using ImageForensics.Core.Models;

namespace ImageForensics.Storage;

public sealed class ScanHistoryStore : IScanHistoryStore
{
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public ScanHistoryStore(string path)
        => _path = Path.GetFullPath(path);

    public async Task AddAsync(
        ScanHistoryEntry entry,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var items = (await ReadUnsafeAsync(cancellationToken))
                .Prepend(entry)
                .Take(200)
                .ToArray();

            await WriteUnsafeAsync(items, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<ScanHistoryEntry>> GetRecentAsync(
        int maxEntries = 50,
        CancellationToken cancellationToken = default)
    {
        maxEntries = Math.Clamp(maxEntries, 1, 200);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return (await ReadUnsafeAsync(cancellationToken))
                .OrderByDescending(x => x.ScannedAtUtc)
                .Take(maxEntries)
                .ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ClearAsync(
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(_path))
                File.Delete(_path);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IReadOnlyList<ScanHistoryEntry>> ReadUnsafeAsync(CancellationToken ct)
    {
        if (!File.Exists(_path))
            return Array.Empty<ScanHistoryEntry>();

        try
        {
            await using var stream = new FileStream(
                _path, FileMode.Open, FileAccess.Read, FileShare.Read,
                32 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            return await JsonSerializer.DeserializeAsync<ScanHistoryEntry[]>(
                       stream, cancellationToken: ct)
                   ?? Array.Empty<ScanHistoryEntry>();
        }
        catch (JsonException)
        {
            return Array.Empty<ScanHistoryEntry>();
        }
    }

    private async Task WriteUnsafeAsync(
        IReadOnlyList<ScanHistoryEntry> entries,
        CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(_path)
            ?? throw new InvalidOperationException("History path has no directory.");

        Directory.CreateDirectory(directory);
        var temp = Path.Combine(
            directory,
            $".{Path.GetFileName(_path)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var stream = new FileStream(
                             temp, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                             32 * 1024,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(
                    stream, entries, cancellationToken: ct);
                await stream.FlushAsync(ct);
            }

            File.Move(temp, _path, true);
        }
        finally
        {
            try
            {
                if (File.Exists(temp))
                    File.Delete(temp);
            }
            catch
            {
            }
        }
    }
}
