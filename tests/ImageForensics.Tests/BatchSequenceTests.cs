using ImageForensics.Core.Utilities;
using Xunit;

namespace ImageForensics.Tests;

public sealed class BatchSequenceTests
{
    [Fact]
    public void BatchEnumerationHonorsCancellationBetweenItems()
    {
        var items =
            Enumerable.Range(1, 10)
                .ToArray();

        using var cts =
            new CancellationTokenSource();

        var seen =
            new List<int>();

        Assert.Throws<OperationCanceledException>(
            () =>
            {
                foreach (var (item, index) in
                         BatchSequence.Enumerate(
                             items,
                             cts.Token))
                {
                    seen.Add(item);

                    if (index == 1)
                        cts.Cancel();
                }
            });

        Assert.Equal(
            new[] { 1, 2 },
            seen);
    }
}
