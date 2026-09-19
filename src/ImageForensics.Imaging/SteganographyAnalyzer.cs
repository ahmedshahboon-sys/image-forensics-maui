using ImageForensics.Core.Abstractions;
using ImageForensics.Core.Models;
using SkiaSharp;

namespace ImageForensics.Imaging;

public sealed class SteganographyAnalyzer : ISteganographyAnalyzer
{
    private const long MaxDecodedPixels = 40_000_000;
    private const int MaxSampledPixels = 1_500_000;

    public Task<SteganographyResult> AnalyzeAsync(
        string filePath,
        CancellationToken cancellationToken = default)
        => Task.Run(
            () => AnalyzeCore(
                filePath,
                cancellationToken),
            cancellationToken);

    private static SteganographyResult AnalyzeCore(
        string filePath,
        CancellationToken ct)
    {
        using var codec = SKCodec.Create(filePath)
            ?? throw new InvalidDataException(
                "Unsupported or corrupt image.");

        var decodedPixels =
            (long)codec.Info.Width *
            codec.Info.Height;

        if (decodedPixels <= 0 ||
            decodedPixels > MaxDecodedPixels)
            throw new InvalidDataException(
                $"Steganography pixel analysis skipped: decoded image exceeds {MaxDecodedPixels:N0} pixels.");

        using var bitmap = SKBitmap.Decode(filePath)
            ?? throw new InvalidDataException(
                "Unable to decode image for steganography analysis.");

        var step = Math.Max(
            1,
            (int)Math.Ceiling(
                Math.Sqrt(
                    decodedPixels /
                    (double)MaxSampledPixels)));

        long sampled = 0;
        long redOne = 0;
        long greenOne = 0;
        long blueOne = 0;
        long transitions = 0;
        long transitionComparisons = 0;

        var planeOnes = new long[8];
        long planeSamples = 0;

        int? previousRed = null;
        int? previousGreen = null;
        int? previousBlue = null;

        for (var y = 0;
             y < bitmap.Height;
             y += step)
        {
            ct.ThrowIfCancellationRequested();

            for (var x = 0;
                 x < bitmap.Width;
                 x += step)
            {
                var pixel = bitmap.GetPixel(x, y);

                var r = pixel.Red;
                var g = pixel.Green;
                var b = pixel.Blue;

                var r0 = r & 1;
                var g0 = g & 1;
                var b0 = b & 1;

                redOne += r0;
                greenOne += g0;
                blueOne += b0;
                sampled++;

                if (previousRed is not null)
                {
                    transitions += previousRed.Value != r0 ? 1 : 0;
                    transitions += previousGreen!.Value != g0 ? 1 : 0;
                    transitions += previousBlue!.Value != b0 ? 1 : 0;
                    transitionComparisons += 3;
                }

                previousRed = r0;
                previousGreen = g0;
                previousBlue = b0;

                for (var plane = 0;
                     plane < 8;
                     plane++)
                {
                    planeOnes[plane] +=
                        (r >> plane) & 1;
                    planeOnes[plane] +=
                        (g >> plane) & 1;
                    planeOnes[plane] +=
                        (b >> plane) & 1;
                }

                planeSamples += 3;
            }
        }

        if (sampled == 0)
            throw new InvalidDataException(
                "No image pixels were sampled.");

        var redRatio =
            (double)redOne / sampled;
        var greenRatio =
            (double)greenOne / sampled;
        var blueRatio =
            (double)blueOne / sampled;

        var transitionRate =
            transitionComparisons == 0
                ? 0
                : (double)transitions /
                  transitionComparisons;

        var entropies = new double[8];

        for (var plane = 0;
             plane < entropies.Length;
             plane++)
        {
            var p =
                planeSamples == 0
                    ? 0
                    : (double)planeOnes[plane] /
                      planeSamples;

            entropies[plane] =
                BinaryEntropy(p);
        }

        var indicators = new List<EvidenceItem>();

        var maxDeviation = new[]
        {
            Math.Abs(redRatio - 0.5),
            Math.Abs(greenRatio - 0.5),
            Math.Abs(blueRatio - 0.5)
        }.Max();

        if (entropies[0] >= 0.9995 &&
            maxDeviation <= 0.0125 &&
            transitionRate is >= 0.47 and <= 0.53)
        {
            indicators.Add(new EvidenceItem(
                "stego.lsb_near_random",
                "LSB statistics are close to a balanced random-like pattern",
                $"R={redRatio:F4}; G={greenRatio:F4}; B={blueRatio:F4}; entropy={entropies[0]:F5}; transition={transitionRate:F4}.",
                ForensicConfidence.Possible,
                "All RGB least-significant bits are close to 50/50, binary entropy is high, and adjacent sampled LSB transitions are near 0.5.",
                "Natural sensor noise, dithering, JPEG decode artifacts and normal image processing can also produce near-random LSBs. This is not proof of embedded data."));
        }

        var channelSpread =
            new[]
            {
                redRatio,
                greenRatio,
                blueRatio
            }.Max() -
            new[]
            {
                redRatio,
                greenRatio,
                blueRatio
            }.Min();

        if (channelSpread > 0.12)
        {
            indicators.Add(new EvidenceItem(
                "stego.lsb_channel_imbalance",
                "LSB distribution differs strongly between RGB channels",
                $"R={redRatio:F4}; G={greenRatio:F4}; B={blueRatio:F4}; spread={channelSpread:F4}.",
                ForensicConfidence.Unknown,
                "The least-significant-bit one-ratio differs substantially between channels.",
                "Color processing, synthetic graphics, palette conversions and channel-specific noise can legitimately create this imbalance."));
        }

        if (entropies[0] > 0.998 &&
            entropies[1] < entropies[0] - 0.08)
        {
            indicators.Add(new EvidenceItem(
                "stego.low_plane_entropy_contrast",
                "Bit-plane 0 is much more random-like than bit-plane 1",
                $"Plane0 entropy={entropies[0]:F5}; Plane1 entropy={entropies[1]:F5}.",
                ForensicConfidence.Possible,
                "The least-significant bit plane has substantially higher binary entropy than the next plane.",
                "Quantization, noise and image-processing pipelines can create the same entropy contrast; manual bit-plane inspection is required."));
        }

        return new SteganographyResult(
            redRatio,
            greenRatio,
            blueRatio,
            transitionRate,
            entropies,
            sampled,
            indicators);
    }

    private static double BinaryEntropy(double p)
    {
        if (p <= 0 || p >= 1)
            return 0;

        return
            -p * Math.Log2(p) -
            (1 - p) *
            Math.Log2(1 - p);
    }
}
