using ImageForensics.Core.Abstractions;
using ImageForensics.Core.Models;

namespace ImageForensics.Forensics.Comparison;

public sealed class ImageComparisonService : IImageComparisonService
{
    private readonly IFileIdentityInspector _identity;
    private readonly IImageTechnicalInspector _technical;
    private readonly IPerceptualHashService _hashes;
    private readonly IMetadataInspector _metadata;
    private readonly IImageHeuristicsService _heuristics;
    private readonly IImagePairPixelAnalyzer _pixels;

    public ImageComparisonService(
        IFileIdentityInspector identity,
        IImageTechnicalInspector technical,
        IPerceptualHashService hashes,
        IMetadataInspector metadata,
        IImageHeuristicsService heuristics,
        IImagePairPixelAnalyzer pixels)
    {
        _identity = identity;
        _technical = technical;
        _hashes = hashes;
        _metadata = metadata;
        _heuristics = heuristics;
        _pixels = pixels;
    }

    public async Task<ImageComparisonResult> CompareAsync(
        string leftPath,
        string rightPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(leftPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(rightPath);

        var leftIdentity =
            await _identity.InspectAsync(
                leftPath,
                cancellationToken:
                    cancellationToken);

        var rightIdentity =
            await _identity.InspectAsync(
                rightPath,
                cancellationToken:
                    cancellationToken);

        var leftTech =
            await _technical.InspectAsync(
                leftPath,
                cancellationToken);

        var rightTech =
            await _technical.InspectAsync(
                rightPath,
                cancellationToken);

        var leftHash =
            await _hashes.ComputeAsync(
                leftPath,
                cancellationToken);

        var rightHash =
            await _hashes.ComputeAsync(
                rightPath,
                cancellationToken);

        var leftMeta =
            await _metadata.InspectAsync(
                leftPath,
                cancellationToken);

        var rightMeta =
            await _metadata.InspectAsync(
                rightPath,
                cancellationToken);

        var pixelMetrics =
            await _pixels.CompareAsync(
                leftPath,
                rightPath,
                cancellationToken);

        var leftHeuristics =
            await TryHeuristicsAsync(
                leftPath,
                cancellationToken);

        var rightHeuristics =
            await TryHeuristicsAsync(
                rightPath,
                cancellationToken);

        var metadataDifferences =
            DiffMetadata(
                leftMeta,
                rightMeta);

        var iccDifferences =
            metadataDifferences
                .Where(d =>
                    d.Directory.Contains(
                        "ICC",
                        StringComparison.OrdinalIgnoreCase))
                .ToArray();

        var aSimilarity =
            PerceptualHashResult.Similarity64(
                leftHash.AHash,
                rightHash.AHash);

        var dSimilarity =
            PerceptualHashResult.Similarity64(
                leftHash.DHash,
                rightHash.DHash);

        var pSimilarity =
            PerceptualHashResult.Similarity64(
                leftHash.PHash,
                rightHash.PHash);

        var exact =
            string.Equals(
                leftIdentity.Sha256,
                rightIdentity.Sha256,
                StringComparison.OrdinalIgnoreCase);

        var leftAspect =
            leftTech.AspectRatio;

        var rightAspect =
            rightTech.AspectRatio;

        var aspectDelta =
            Math.Abs(
                leftAspect -
                rightAspect);

        var sameAspect =
            aspectDelta <=
            Math.Max(
                0.005,
                Math.Min(
                    leftAspect,
                    rightAspect) *
                0.005);

        var dimensionsEqual =
            leftTech.Width ==
                rightTech.Width &&
            leftTech.Height ==
                rightTech.Height;

        var scaleX =
            leftTech.Width == 0
                ? 0
                : (double)rightTech.Width /
                  leftTech.Width;

        var scaleY =
            leftTech.Height == 0
                ? 0
                : (double)rightTech.Height /
                  leftTech.Height;

        var uniformResize =
            !dimensionsEqual &&
            sameAspect &&
            Math.Abs(scaleX - scaleY) <= 0.02 &&
            pSimilarity >= 0.84 &&
            pixelMetrics.NormalizedRgbSimilarity >= 0.82;

        var cropCandidate =
            !sameAspect &&
            pixelMetrics.CenterCropSimilarity >= 0.84 &&
            Math.Max(
                pSimilarity,
                dSimilarity) >= 0.72;

        var dimensionRelation =
            dimensionsEqual
                ? "Same dimensions"
                : uniformResize
                    ? "Uniform resize candidate"
                    : cropCandidate
                        ? "Center-crop / aspect-change candidate"
                        : sameAspect
                            ? "Same aspect ratio with different dimensions"
                            : "Different aspect ratio";

        var indicators =
            BuildIndicators(
                exact,
                uniformResize,
                cropCandidate,
                leftHeuristics,
                rightHeuristics,
                metadataDifferences,
                iccDifferences,
                pixelMetrics,
                pSimilarity);

        return new ImageComparisonResult(
            exact,
            leftIdentity.Sha256,
            rightIdentity.Sha256,
            leftTech.Width,
            leftTech.Height,
            rightTech.Width,
            rightTech.Height,
            leftAspect,
            rightAspect,
            scaleX,
            scaleY,
            dimensionRelation,
            uniformResize,
            cropCandidate,
            aSimilarity,
            dSimilarity,
            pSimilarity,
            pixelMetrics,
            leftHeuristics?.EstimatedJpegQuality,
            rightHeuristics?.EstimatedJpegQuality,
            leftHeuristics?.ChromaSubsampling,
            rightHeuristics?.ChromaSubsampling,
            metadataDifferences,
            iccDifferences,
            indicators);
    }

    private async Task<ImageHeuristicsResult?> TryHeuristicsAsync(
        string path,
        CancellationToken ct)
    {
        try
        {
            return await _heuristics.AnalyzeAsync(
                path,
                ct);
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    private static IReadOnlyList<EvidenceItem> BuildIndicators(
        bool exact,
        bool uniformResize,
        bool cropCandidate,
        ImageHeuristicsResult? leftHeuristics,
        ImageHeuristicsResult? rightHeuristics,
        IReadOnlyList<MetadataDifference> metadataDifferences,
        IReadOnlyList<MetadataDifference> iccDifferences,
        PixelComparisonMetrics pixelMetrics,
        double pSimilarity)
    {
        var items =
            new List<EvidenceItem>();

        if (exact)
        {
            items.Add(
                new EvidenceItem(
                    "compare.exact_hash",
                    "Files are byte-for-byte identical",
                    "The SHA-256 digests are identical.",
                    ForensicConfidence.Confirmed,
                    "Cryptographic SHA-256 equality of the complete input bytes.",
                    "This establishes equality of these two files only; it does not establish authenticity or provenance."));
        }

        if (uniformResize)
        {
            items.Add(
                new EvidenceItem(
                    "compare.uniform_resize_candidate",
                    "Uniform resize candidate",
                    $"Dimensions changed with near-uniform scale; normalized RGB similarity={pixelMetrics.NormalizedRgbSimilarity:F4}; pHash similarity={pSimilarity:F4}.",
                    ForensicConfidence.Possible,
                    "Aspect ratio and X/Y scale are consistent, while perceptual and normalized-pixel similarities remain high.",
                    "Different images can share strong visual similarity, and resampling/cropping pipelines can alter these metrics. This does not prove one file was derived from the other."));
        }

        if (cropCandidate)
        {
            items.Add(
                new EvidenceItem(
                    "compare.center_crop_candidate",
                    "Center-crop / aspect-change candidate",
                    $"Aspect ratios differ, while center-crop similarity={pixelMetrics.CenterCropSimilarity:F4}.",
                    ForensicConfidence.Possible,
                    "A center-cropped normalization aligns more closely than the raw aspect relationship.",
                    "This heuristic only tests centered crop-like relationships and cannot establish edit history or direction of derivation."));
        }

        if (leftHeuristics is not null &&
            rightHeuristics is not null)
        {
            var qualityDelta =
                leftHeuristics.EstimatedJpegQuality is not null &&
                rightHeuristics.EstimatedJpegQuality is not null
                    ? Math.Abs(
                        leftHeuristics.EstimatedJpegQuality.Value -
                        rightHeuristics.EstimatedJpegQuality.Value)
                    : 0;

            var chromaChanged =
                !string.Equals(
                    leftHeuristics.ChromaSubsampling,
                    rightHeuristics.ChromaSubsampling,
                    StringComparison.OrdinalIgnoreCase);

            if (qualityDelta >= 8 ||
                chromaChanged)
            {
                items.Add(
                    new EvidenceItem(
                        "compare.compression_change",
                        "Compression characteristics differ",
                        $"Estimated JPEG quality delta={qualityDelta:F1}; chroma left={leftHeuristics.ChromaSubsampling ?? "n/a"}; right={rightHeuristics.ChromaSubsampling ?? "n/a"}.",
                        ForensicConfidence.Possible,
                        "Quantization-derived quality estimates and/or chroma subsampling differ between the files.",
                        "JPEG quality estimation is approximate. Different encoders can produce different tables without implying manipulation."));
            }
        }

        if (iccDifferences.Count > 0)
        {
            items.Add(
                new EvidenceItem(
                    "compare.icc_difference",
                    "ICC/color-profile metadata differs",
                    $"{iccDifferences.Count} ICC-related field difference(s) were observed.",
                    ForensicConfidence.Confirmed,
                    "ICC-related metadata values differ between these two files.",
                    "A color-profile difference can result from export or conversion and is not proof of content manipulation."));
        }

        if (metadataDifferences.Count > 0)
        {
            items.Add(
                new EvidenceItem(
                    "compare.metadata_difference",
                    "Metadata differs",
                    $"{metadataDifferences.Count} metadata field difference(s) were observed.",
                    ForensicConfidence.Confirmed,
                    "The compared metadata values are not identical.",
                    "Metadata can be added, removed or rewritten by ordinary applications and transport services."));
        }

        return items;
    }

    private static IReadOnlyList<MetadataDifference> DiffMetadata(
        MetadataInspectionResult left,
        MetadataInspectionResult right)
    {
        static string Key(
            MetadataField f)
            => $"{f.Directory}\u001f{f.Tag}";

        var l =
            left.Fields
                .GroupBy(Key)
                .ToDictionary(
                    g => g.Key,
                    g => g.First());

        var r =
            right.Fields
                .GroupBy(Key)
                .ToDictionary(
                    g => g.Key,
                    g => g.First());

        var keys =
            new SortedSet<string>(
                l.Keys,
                StringComparer.Ordinal);

        keys.UnionWith(
            r.Keys);

        var diffs =
            new List<MetadataDifference>();

        foreach (var key in keys)
        {
            l.TryGetValue(
                key,
                out var lf);

            r.TryGetValue(
                key,
                out var rf);

            var lv =
                lf?.ParsedValue ??
                lf?.RawValue;

            var rv =
                rf?.ParsedValue ??
                rf?.RawValue;

            if (string.Equals(
                    lv,
                    rv,
                    StringComparison.Ordinal))
                continue;

            var split =
                key.Split(
                    '\u001f');

            diffs.Add(
                new MetadataDifference(
                    split[0],
                    split.Length > 1
                        ? split[1]
                        : string.Empty,
                    lv,
                    rv));
        }

        return diffs;
    }
}
