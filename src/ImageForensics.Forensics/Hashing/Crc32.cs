namespace ImageForensics.Forensics.Hashing;

public static class Crc32
{
    private static readonly uint[] Table = BuildTable();

    public static uint Compute(ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var b in data)
            crc = (crc >> 8) ^ Table[(crc ^ b) & 0xFF];
        return ~crc;
    }

    public static async Task<uint> ComputeAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        uint crc = 0xFFFFFFFF;
        var buffer = new byte[128 * 1024];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;
            for (var i = 0; i < read; i++)
                crc = (crc >> 8) ^ Table[(crc ^ buffer[i]) & 0xFF];
        }
        return ~crc;
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
