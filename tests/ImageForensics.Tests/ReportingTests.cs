using ImageForensics.Core.Models;
using ImageForensics.Reporting;
using Xunit;

namespace ImageForensics.Tests;

public sealed class ReportingTests
{
    [Fact]
    public void BatchCsvEscapesNames()
    {
        var csv =
            new ReportWriter()
                .BatchToCsv(
                    new[]
                    {
                        new BatchReportRow(
                            "a,\"b.jpg",
                            "abc",
                            "JPEG",
                            12,
                            10,
                            20,
                            0.5,
                            true,
                            2,
                            3,
                            0,
                            0,
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            null,
                            null,
                            null)
                    });

        Assert.Contains(
            "\"a,\"\"b.jpg\"",
            csv);
    }
}
