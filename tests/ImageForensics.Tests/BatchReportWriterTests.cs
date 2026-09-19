using ImageForensics.Reporting;
using Xunit;

namespace ImageForensics.Tests;

public sealed class BatchReportWriterTests
{
    [Fact]
    public void BatchExportsContainDuplicateAndNearDuplicateColumns()
    {
        var row =
            new BatchReportRow(
                "a.jpg",
                "abc",
                "JPEG",
                123,
                10,
                20,
                0.5,
                true,
                2,
                3,
                1,
                12,
                "a",
                "d",
                "p",
                "first.jpg",
                "near.jpg",
                null);

        var writer =
            new ReportWriter();

        var csv =
            writer.BatchToCsv(
                new[] { row });

        var json =
            writer.BatchToJson(
                new[] { row });

        Assert.Contains(
            "DuplicateOf",
            csv);

        Assert.Contains(
            "NearDuplicateOf",
            csv);

        Assert.Contains(
            "first.jpg",
            csv);

        Assert.Contains(
            "\"NearDuplicateOf\"",
            json);
    }
}
