using ImageForensics.Core.Models;
using ImageForensics.Reporting;
using Xunit;

namespace ImageForensics.Tests;

public sealed class ReportingTests
{
    [Fact]
    public void BatchCsvEscapesNames()
    {
        var csv=new ReportWriter().BatchToCsv(new[]{new BatchReportRow("a,\"b.jpg","abc","JPEG",12,10,20,true,2,3)});
        Assert.Contains("\"a,\"\"b.jpg\"",csv);
    }
}
