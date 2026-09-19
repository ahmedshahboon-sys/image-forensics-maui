namespace ImageForensics.Core.Models;

public sealed record PerceptualHashResult(string AHash, string DHash, string PHash)
{
    public static double Similarity64(string leftHex, string rightHex)
    {
        if (!ulong.TryParse(leftHex, System.Globalization.NumberStyles.HexNumber, null, out var a) ||
            !ulong.TryParse(rightHex, System.Globalization.NumberStyles.HexNumber, null, out var b))
            return 0;

        var x = a ^ b;
        var distance = System.Numerics.BitOperations.PopCount(x);
        return 1.0 - (distance / 64.0);
    }
}
