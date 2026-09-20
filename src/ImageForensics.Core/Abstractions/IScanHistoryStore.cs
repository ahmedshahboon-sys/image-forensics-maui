using ImageForensics.Core.Models;

namespace ImageForensics.Core.Abstractions;

public interface IScanHistoryStore
{
    Task AddAsync(
        ScanHistoryEntry entry,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ScanHistoryEntry>> GetRecentAsync(
        int maxEntries = 50,
        CancellationToken cancellationToken = default);

    Task ClearAsync(
        CancellationToken cancellationToken = default);
}
