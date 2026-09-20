using System.Globalization;
using System.Text.RegularExpressions;
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
        ["Lens Serial Number"] = "Lens serial number when recorded",
        ["Body Serial Number"] = "Camera/device body serial number when recorded",
        ["Serial Number"] = "Device or component serial number when recorded",
        ["Software"] = "Software recorded as processing or writing metadata",
        ["Artist"] = "Author/artist field",
        ["Copyright"] = "Copyright field",
        ["Date/Time Original"] = "Original capture timestamp",
        ["Date/Time Digitized"] = "Digitization timestamp",
        ["Date/Time"] = "Metadata modification timestamp",
        ["Offset Time"] = "Timezone offset associated with a metadata timestamp",
        ["Offset Time Original"] = "Timezone offset for original capture time",
        ["Offset Time Digitized"] = "Timezone offset for digitization time",
        ["GPS Latitude"] = "Recorded latitude",
        ["GPS Longitude"] = "Recorded longitude",
        ["GPS Altitude"] = "Recorded altitude",
        ["GPS Speed"] = "Recorded movement speed",
        ["GPS Img Direction"] = "Recorded camera/image direction",
        ["GPS Track"] = "Recorded movement direction",
        ["GPS Time-Stamp"] = "Recorded GPS time",
        ["GPS Date Stamp"] = "Recorded GPS date",
        ["Exposure Time"] = "Camera exposure time",
        ["F-Number"] = "Aperture value",
        ["Aperture Value"] = "Aperture value",
        ["ISO Speed Ratings"] = "Sensor ISO setting",
        ["Photographic Sensitivity"] = "Sensor ISO/sensitivity setting",
        ["Focal Length"] = "Lens focal length",
        ["Flash"] = "Flash state",
        ["White Balance Mode"] = "Recorded white-balance mode",
        ["White Balance"] = "Recorded white-balance value",
        ["Metering Mode"] = "Camera metering mode",
        ["Orientation"] = "Stored display/camera orientation",
        ["Digital Zoom Ratio"] = "Recorded digital zoom ratio",
        ["Camera Temperature"] = "Camera/device temperature if the maker recorded it",
        ["User Comment"] = "User or device comment field",
        ["Image Description"] = "Image description/caption",
        ["XP Title"] = "Windows XP title metadata",
        ["XP Comment"] = "Windows XP comment metadata",
        ["XP Keywords"] = "Windows XP keywords metadata",
        ["XP Subject"] = "Windows XP subject metadata",
        ["XP Author"] = "Windows XP author metadata"
    };

    public Task<MetadataInspectionResult> InspectAsync(string filePath, CancellationToken cancellationToken = default)
        => Task.Run(() => InspectCore(filePath, cancellationToken), cancellationToken);

    private static MetadataInspectionResult InspectCore(string filePath, CancellationToken ct)
    {
        var fields = new List<MetadataField>();
        var errors = new List<string>();
        IReadOnlyList<MetadataExtractor.Directory> directories;

        try
        {
            directories = ImageMetadataReader.ReadMetadata(filePath);
        }
        catch (Exception ex) when (ex is ImageProcessingException or IOException)
        {
            return new MetadataInspectionResult(
                Array.Empty<MetadataField>(),
                null,
                new[] { $"{ex.GetType().Name}: {ex.Message}" });
        }

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
                    Meanings.TryGetValue(tag.Name, out var meaning)
                        ? meaning
                        : "Metadata field reported by the image container",
                    ForensicConfidence.Confirmed,
                    directory.Name));
            }
        }

        GpsInfo? gpsInfo = null;
        var gps = directories.OfType<GpsDirectory>().FirstOrDefault();
        var location = gps?.GetGeoLocation();

        if (location is { } value &&
            IsFiniteCoordinate(value.Latitude, -90, 90) &&
            IsFiniteCoordinate(value.Longitude, -180, 180))
        {
            var altitude = FindNumeric(fields, "GPS Altitude");
            var speed = FindNumeric(fields, "GPS Speed");
            var direction = FindNumeric(fields, "GPS Img Direction", "GPS Track", "GPS Dest Bearing");
            var gpsDate = FindText(fields, "GPS Date Stamp");
            var gpsTime = FindText(fields, "GPS Time-Stamp");
            var timestamp = string.Join(" ", new[] { gpsDate, gpsTime }.Where(x => !string.IsNullOrWhiteSpace(x)));

            gpsInfo = new GpsInfo(
                value.Latitude,
                value.Longitude,
                altitude,
                speed,
                direction,
                string.IsNullOrWhiteSpace(timestamp) ? null : timestamp);
        }

        return new MetadataInspectionResult(fields, gpsInfo, errors);
    }

    private static bool IsFiniteCoordinate(double value, double min, double max)
        => !double.IsNaN(value) && !double.IsInfinity(value) && value >= min && value <= max;

    private static string? FindText(IReadOnlyList<MetadataField> fields, params string[] tags)
        => fields.FirstOrDefault(f => tags.Any(t => string.Equals(f.Tag, t, StringComparison.OrdinalIgnoreCase)))?.ParsedValue
           ?? fields.FirstOrDefault(f => tags.Any(t => string.Equals(f.Tag, t, StringComparison.OrdinalIgnoreCase)))?.RawValue;

    private static double? FindNumeric(IReadOnlyList<MetadataField> fields, params string[] tags)
    {
        var text = FindText(fields, tags);
        if (string.IsNullOrWhiteSpace(text)) return null;

        var match = Regex.Match(text, @"[-+]?\d+(?:[.,]\d+)?");
        if (!match.Success) return null;

        var normalized = match.Value.Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }
}
