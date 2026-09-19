using ImageForensics.Core.Models;
using ImageForensics.Forensics.VisibleContent;
using Xunit;

namespace ImageForensics.Tests;

public sealed class VisibleTextEntityExtractorTests
{
    [Fact]
    public void ExtractsUrlEmailAndPhoneFromOcrText()
    {
        var entities = new VisibleTextEntityExtractor().Extract(
            "Visit https://example.com/path, mail Test.User@example.com or call +218 92 123 4567.");

        Assert.Contains(entities, x => x.Kind == "URL" && x.Value.StartsWith("https://example.com/path", StringComparison.Ordinal));
        Assert.Contains(entities, x => x.Kind == "Email" && x.Value.Equals("Test.User@example.com", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(entities, x => x.Kind == "Phone" && x.Value.Contains("218", StringComparison.Ordinal));
    }

    [Fact]
    public void ClassifiesQrHttpUrlWithoutOpeningIt()
    {
        var entities = new VisibleTextEntityExtractor().Extract(
            null,
            new[] { new BarcodeHit("QR_CODE", "https://openai.com/") });

        var qr = Assert.Single(entities, x => x.Kind == "QR URL");
        Assert.Equal("Barcode:QR_CODE", qr.Source);
        Assert.Equal("https://openai.com/", qr.Value);
    }

    [Fact]
    public void DoesNotPromoteWifiQrPayloadToUrl()
    {
        var entities = new VisibleTextEntityExtractor().Extract(
            null,
            new[] { new BarcodeHit("QR_CODE", "WIFI:T:WPA;S:test;P:secret;;") });

        Assert.DoesNotContain(entities, x => x.Kind == "QR URL");
    }

    [Fact]
    public void RejectsShortNumericFragmentsAsPhones()
    {
        var entities = new VisibleTextEntityExtractor().Extract("ISO 100, f/2.8, 2026");
        Assert.DoesNotContain(entities, x => x.Kind == "Phone");
    }
}
