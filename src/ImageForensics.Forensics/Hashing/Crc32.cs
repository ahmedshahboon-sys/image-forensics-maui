namespace ImageForensics.Forensics.Hashing;

public static class Crc32
{
    public const uint InitialState = 0xFFFFFFFF;
    private static readonly uint[] Table = BuildTable();

    public static uint Compute(ReadOnlySpan<byte> data)
        => FinalizeHash(Update(InitialState, data));

    public static uint Update(uint state, ReadOnlySpan<byte> data)
    {
        var crc = state;
        foreach (var b in data)
            crc = (crc >> 8) ^ Table[(crc ^ b) & 0xFF];
        return crc;
    }

    public static uint FinalizeHash(uint state) => ~state;

    public static async Task<uint> ComputeAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var crc = InitialState;
        var buffer = new byte[128 * 1024];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;
            crc = Update(crc, buffer.AsSpan(0, read));
        }
        return FinalizeHash(crc);
    }

    private static uint[] BuildTable()
    {
        const uint polynomial = 0xEDB88320;
        var table = new uint[256];
        for (uint i = 0; i < table.Length; i++)
        {
            var c = i;
            for (var bit = 0; bit < 8; bit++)
                c = (c & 1) != 0 ? polynomial ^ (c >> 1) : c >> 1;
            table[i] = c;
        }
        return table;
    }
}
