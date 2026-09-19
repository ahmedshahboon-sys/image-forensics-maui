using ImageForensics.Core.Abstractions;
using ImageForensics.Core.Models;

namespace ImageForensics.Forensics.Comparison;

public sealed class ImageComparisonService : IImageComparisonService
{
    private readonly IFileIdentityInspector _identity;
    private readonly IImageTechnicalInspector _technical;
    private readonly IPerceptualHashService _hashes;
    private readonly IMetadataInspector _metadata;

    public ImageComparisonService(
        IFileIdentityInspector identity,
        IImageTechnicalInspector technical,
        IPerceptualHashService hashes,
        IMetadataInspector metadata)
    {
        _identity = identity;
        _technical = technical;
        _hashes = hashes;
        _metadata = metadata;
    }

    public async Task<ImageComparisonResult> CompareAsync(
        string leftPath,
        string rightPath,
        CancellationToken cancellationToken = default)
    {
        var leftIdentity = await _identity.InspectAsync(leftPath, cancellationToken: cancellationToken);
        var rightIdentity = await _identity.InspectAsync(rightPath, cancellationToken: cancellationToken);

        var leftTech = await _technical.InspectAsync(leftPath, cancellationToken);
        var rightTech = await _technical.InspectAsync(rightPath, cancellationToken);

        var leftHash = await _hashes.ComputeAsync(leftPath, cancellationToken);
        var rightHash = await _hashes.ComputeAsync(rightPath, cancellationToken);

        var leftMeta = await _metadata.InspectAsync(leftPath, cancellationToken);
        var rightMeta = await _metadata.InspectAsync(rightPath, cancellationToken);

        return new ImageComparisonResult(
            leftIdentity.Sha256 == rightIdentity.Sha256,
            leftIdentity.Sha256,
            rightIdentity.Sha256,
            leftTech.Width,
            leftTech.Height,
            rightTech.Width,
            rightTech.Height,
            PerceptualHashResult.Similarity64(leftHash.AHash, rightHash.AHash),
            PerceptualHashResult.Similarity64(leftHash.DHash, rightHash.DHash),
            PerceptualHashResult.Similarity64(leftHash.PHash, rightHash.PHash),
            DiffMetadata(leftMeta, rightMeta));
    }

    private static IReadOnlyList<MetadataDifference> DiffMetadata(
        MetadataInspectionResult left,
        MetadataInspectionResult right)
    {
        static string Key(MetadataField f) => $"{f.Directory}\u001f{f.Tag}";
        var l = left.Fields.GroupBy(Key).ToDictionary(g => g.Key, g => g.First());
        var r = right.Fields.GroupBy(Key).ToDictionary(g => g.Key, g => g.First());

        var keys = new SortedSet<string>(l.Keys, StringComparer.Ordinal);
        keys.UnionWith(r.Keys);
        var diffs = new List<MetadataDifference>();

        foreach (var key in keys)
        {
            l.TryGetValue(key, out var lf);
            r.TryGetValue(key, out var rf);
            var lv = lf?.ParsedValue ?? lf?.RawValue;
            var rv = rf?.ParsedValue ?? rf?.RawValue;
            if (string.Equals(lv, rv, StringComparison.Ordinal)) continue;

            var split = key.Split('\u001f');
            diffs.Add(new MetadataDifference(
                split[0],
                split.Length > 1 ? split[1] : string.Empty,
                lv,
                rv));
        }

        return diffs;
    }
}
