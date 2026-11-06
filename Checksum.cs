namespace NFSU2CustomCarSaves;

/// <summary>NFSU2 MSB-first CRC-32 (poly 0x04C11DB7), gelijk aan SPEED2 / hackch.</summary>
public static class Checksum
{
    private const uint Poly = 0x04C11DB7;
    private static readonly uint[] Table = BuildTable();

    private static uint[] BuildTable()
    {
        var t = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i << 24;
            for (int k = 0; k < 8; k++)
                c = (c & 0x80000000) != 0 ? (c << 1) ^ Poly : c << 1;
            t[i] = c;
        }
        return t;
    }

    public static uint Nfsu2Crc(byte[] data, int start, int end)
    {
        if (end - start < 4) return 0;
        uint first = (uint)((data[start] << 24) | (data[start + 1] << 16) | (data[start + 2] << 8) | data[start + 3]);
        uint r = ~first;
        for (int i = start + 4; i < end; i++)
            r = ((r << 8) | data[i]) ^ Table[r >> 24];
        return ~r;
    }

    public static void RecalculateCareerChecksums(byte[] career)
    {
        if (career.Length < Layout.HeaderSize) throw new InvalidDataException("Career file too small.");
        uint lenA = BitConverter.ToUInt32(career, 0x08);
        uint lenB = BitConverter.ToUInt32(career, 0x0C);
        int a0 = Layout.HeaderSize;
        int a1 = a0 + (int)lenA;
        int b1 = a1 + (int)lenB;
        if (b1 != career.Length)
            throw new InvalidDataException("Block lengths do not match file size.");

        BitConverter.GetBytes(Nfsu2Crc(career, a0, a1)).CopyTo(career, 0x10);
        BitConverter.GetBytes(Nfsu2Crc(career, a1, b1)).CopyTo(career, 0x14);
        BitConverter.GetBytes(Nfsu2Crc(career, 0, Layout.HeaderSize - 4)).CopyTo(career, 0x18);
    }

    /// <summary>True when header CRC fields @ 0x10 / 0x14 / 0x18 match the payload.</summary>
    public static bool CareerChecksumsMatch(byte[] career)
    {
        if (career.Length < Layout.HeaderSize) return false;
        uint lenA = BitConverter.ToUInt32(career, 0x08);
        uint lenB = BitConverter.ToUInt32(career, 0x0C);
        int a0 = Layout.HeaderSize;
        int a1 = a0 + (int)lenA;
        int b1 = a1 + (int)lenB;
        if (b1 != career.Length) return false;
        uint cA = BitConverter.ToUInt32(career, 0x10);
        uint cB = BitConverter.ToUInt32(career, 0x14);
        uint cH = BitConverter.ToUInt32(career, 0x18);
        return cA == Nfsu2Crc(career, a0, a1)
            && cB == Nfsu2Crc(career, a1, b1)
            && cH == Nfsu2Crc(career, 0, Layout.HeaderSize - 4);
    }
}
