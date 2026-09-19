using ImageForensics.Core.Abstractions;
using ImageForensics.Core.Models;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

namespace ImageForensics.Metadata;

public sealed class MetadataInspector : IMetadataInspector
{
    private static readonly Dictionary<string, string> Meanings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Make"] = "Camera/device manufacturer",
        ["Model"] = "Camera/device model",
        ["Lens Model"] = "Lens model",
        ["Software"] = "Software recorded as processing or writing metadata",
        ["Artist"] = "Author/artist field",
        ["Copyright"] = "Copyright field",
        ["Date/Time Original"] = "Original capture timestamp",
        ["Date/Time Digitized"] = "Digitization timestamp",
        ["GPS Latitude"] = "Recorded latitude",
        ["GPS Longitude"] = "Recorded longitude",
        ["GPS Altitude"] = "Recorded altitude",
        ["Exposure Time"] = "Camera exposure time",
        ["F-Number"] = "Aperture value",
        ["ISO Speed Ratings"] = "Sensor ISO setting",
        ["Focal Length"] = "Lens focal length",
        ["Flash"] = "Flash state",
        ["White Balance Mode"] = "Recorded white-balance mode"
    };

    public Task<MetadataInspectionResult> InspectAsync(string filePath, CancellationToken cancellationToken = default)
        => Task.Run(() => InspectCore(filePath, cancellationToken), cancellationToken);

    private static MetadataInspectionResult InspectCore(string filePath, CancellationToken ct)
    {
        var fields = new List<MetadataField>();
        var errors = new List<string>();
        var directories = ImageMetadataReader.ReadMetadata(filePath);

        foreach (var directory in directories)
        {
            ct.ThrowIfCancellationRequested();

            foreach (var error in directory.Errors)
                errors.Add($"{directory.Name}: {error}");

            foreach (var tag in directory.Tags)
            {
                ct.ThrowIfCancellationRequested();
                string? raw = null;
                try { raw = directory.GetObject(tag.Type)?.ToString(); } catch { }

                fields.Add(new MetadataField(
                    directory.Name,
                    tag.Name,
                    raw,
                    tag.Description,
                    Meanings.TryGetValue(tag.Name, out var meaning) ? meaning : "Metadata field reported by the image container",
                    ForensicConfidence.Confirmed,
                    directory.Name));
            }
        }

        GpsInfo? gpsInfo = null;
        var gps = directories.OfType<GpsDirectory>().FirstOrDefault();
        var location = gps?.GetGeoLocation();
        if (location is { } value &&
            !double.IsNaN(value.Latitude) && !double.IsInfinity(value.Latitude) &&
            !double.IsNaN(value.Longitude) && !double.IsInfinity(value.Longitude))
        {
            gpsInfo = new GpsInfo(value.Latitude, value.Longitude);
        }

        return new MetadataInspectionResult(fields, gpsInfo, errors);
    }
}
