namespace NFSU2CustomCarSaves;

/// <summary>
/// Main-menu Customize → <b>My Cars</b> tab (MemphisRider layout).
/// 20 × 1072-byte TunedCar blocks in the career profile @ decimal 1196.
/// Separate from Career garage slots 1–10 (profile + GARAGE.DAT).
/// Occupation uses TunedCar[+8] (not Bought at end of SAVE_REC).
/// </summary>
public static class MyCarsLayout
{
    public const int BaseOffset = 1196;
    public const int BlockSize = 1072; // == Layout.TunedCarSize
    public const int Count = 20;
    /// <summary>MemphisRider enable flag inside the TunedCar block.</summary>
    public const int EnableOff = 8;
    public const int CarHashOff = Layout.CarHashInTuned; // 24
    /// <summary>Editor base within a block (= TunedCar + 4), same as career SlotEditor relative to TunedCar.</summary>
    public const int EditorFromBlock = 4;
    public const int DynoRel = Layout.DynoOff + EditorFromBlock; // 0x358
}

public sealed class MyCarInfo
{
    public int Index { get; init; }
    public bool IsOccupied { get; init; }
    public uint CarHash { get; init; }
    public string XName { get; init; } = "(empty)";
    public string DisplayName => CarNames.ListLabel(CarHash, IsOccupied, locked: false);
}

public static class MyCarsService
{
    public static int BlockOffset(int index)
    {
        if ((uint)index >= MyCarsLayout.Count) throw new ArgumentOutOfRangeException(nameof(index));
        return MyCarsLayout.BaseOffset + index * MyCarsLayout.BlockSize;
    }

    /// <summary>Slot-editor absolute offset (career-equivalent SLOT_BASE).</summary>
    public static int EditorOffset(int index) => BlockOffset(index) + MyCarsLayout.EditorFromBlock;

    public static bool BufferHasMyCars(byte[] working) =>
        working.Length >= MyCarsLayout.BaseOffset + MyCarsLayout.Count * MyCarsLayout.BlockSize;

    public static MyCarInfo Read(byte[] working, int index)
    {
        Ensure(working);
        int off = BlockOffset(index);
        bool on = working[off + MyCarsLayout.EnableOff] != 0;
        uint hash = BitConverter.ToUInt32(working, off + MyCarsLayout.CarHashOff);
        if (hash == 0) on = false;
        string x = on ? CarNames.ResolveXName(hash) : "(empty)";
        return new MyCarInfo
        {
            Index = index,
            IsOccupied = on,
            CarHash = on ? hash : 0,
            XName = x,
        };
    }

    public static List<MyCarInfo> ReadAll(byte[] working)
    {
        var list = new List<MyCarInfo>(MyCarsLayout.Count);
        if (!BufferHasMyCars(working)) return list;
        for (int i = 0; i < MyCarsLayout.Count; i++)
            list.Add(Read(working, i));
        return list;
    }

    public static void ReadTuned(byte[] working, int index, out byte[] tuned)
    {
        Ensure(working);
        tuned = new byte[Layout.TunedCarSize];
        Buffer.BlockCopy(working, BlockOffset(index), tuned, 0, Layout.TunedCarSize);
    }

    public static void WriteTuned(byte[] working, int index, byte[] tuned)
    {
        if (tuned.Length != Layout.TunedCarSize)
            throw new ArgumentException($"TunedCar must be {Layout.TunedCarSize} bytes.");
        Ensure(working);
        var t = (byte[])tuned.Clone();
        // My Cars occupation = enable @ +8 (Bought at 0x42D stays unused / 0)
        t[MyCarsLayout.EnableOff] = 1;
        if (t.Length > 12 && t[12] == 0) t[12] = 4;
        Buffer.BlockCopy(t, 0, working, BlockOffset(index), Layout.TunedCarSize);
    }

    public static void Clear(byte[] working, int index)
    {
        Ensure(working);
        Array.Clear(working, BlockOffset(index), MyCarsLayout.BlockSize);
    }

    public static CarSlotPackage ExportPackage(byte[] working, int index, string profileName)
    {
        var info = Read(working, index);
        if (!info.IsOccupied) throw new InvalidOperationException("My Cars slot is empty.");
        ReadTuned(working, index, out var tuned);
        return new CarSlotPackage
        {
            CarName = info.DisplayName,
            XName = info.XName,
            CarHash = info.CarHash,
            SourceProfile = profileName + " / My Cars",
            TunedHex = Convert.ToHexString(tuned),
            InventoryHex = Convert.ToHexString(new byte[Layout.InventorySize]),
        };
    }

    public static void ImportPackage(byte[] working, int index, CarSlotPackage pkg)
    {
        var (tuned, _) = CarSlotIo.Decode(pkg);
        WriteTuned(working, index, tuned);
    }

    /// <summary>
    /// Stock donor into a My Cars block (same relative patches as career SLOT_BASE).
    /// Sets enable=1; skips SAVE_REC lock/bought / garage counters.
    /// </summary>
    public static void PlaceDonor(byte[] working, int index, CarDonor donor)
    {
        Ensure(working);
        int editor = EditorOffset(index);

        int visualOff = editor + 0x18;
        int visualBytes = 170 * 4;
        if (visualOff + visualBytes <= working.Length)
            Array.Clear(working, visualOff, visualBytes);

        foreach (var (rel, data) in donor.SlotBlocks)
        {
            int at = editor + rel;
            if (at < 0 || at + data.Length > working.Length)
                throw new InvalidOperationException($"Car data does not fit in My Cars slot {index + 1}.");
            // Keep writes inside this TunedCar block
            int block = BlockOffset(index);
            if (at < block || at + data.Length > block + MyCarsLayout.BlockSize)
                throw new InvalidOperationException($"Car data overflows My Cars slot {index + 1}.");
            Buffer.BlockCopy(data, 0, working, at, data.Length);
        }

        int perfAt = editor + Layout.PerfOff;
        if (perfAt + Layout.PerfCount <= BlockOffset(index) + MyCarsLayout.BlockSize)
            Array.Clear(working, perfAt, Layout.PerfCount);

        int uniqAt = editor + Layout.UniquePerfOff;
        int uniqBytes = Layout.UniquePerfCount * 4;
        if (uniqAt + uniqBytes <= BlockOffset(index) + MyCarsLayout.BlockSize)
            Array.Clear(working, uniqAt, uniqBytes);

        var stockDyno = new byte[Layout.DynoTotalSize];
        Array.Fill(stockDyno, (byte)100);
        DynoService.WriteRaw(working, index, stockDyno, menuMyCars: true);

        working[BlockOffset(index) + MyCarsLayout.EnableOff] = 1;
    }

    public static DynoPackage ExportDyno(byte[] working, int index, string? saveName = null)
    {
        var info = Read(working, index);
        if (!info.IsOccupied)
            throw new InvalidOperationException("My Cars slot is empty — no dyno to copy.");
        byte[] raw = DynoService.ReadRaw(working, index, menuMyCars: true);
        return new DynoPackage
        {
            SourceCar = info.DisplayName,
            SourceSave = saveName ?? "",
            SourceSlot = index + 1,
            SourceHash = info.CarHash.ToString("X8"),
            Note = $"Dyno from My Cars · {info.DisplayName} · menu slot {index + 1}",
            DynoHex = Convert.ToHexString(raw),
        };
    }

    private static void Ensure(byte[] working)
    {
        if (!BufferHasMyCars(working))
            throw new InvalidOperationException("Save buffer too small for My Cars (menu Customize) blocks.");
    }
}
