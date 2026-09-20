namespace ImageForensics.Core.Utilities;

public static class BatchSequence
{
    public static IEnumerable<(T Item, int Index)> Enumerate<T>(
        IReadOnlyList<T> items,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);

        for (var index = 0; index < items.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return (items[index], index);
        }
    }
}
