namespace NFSU2CustomCarSaves;

public enum GarageState { Loaded, Missing, Stale, Invalid }

public sealed class SlotInfo
{
    public int Index { get; init; }          // 0..9
    public bool IsOccupied { get; init; }
    public bool IsLocked { get; init; }
    public string XName { get; init; } = "(empty)";
    public string DisplayName { get; init; } = "(empty)";
    public uint CarHash { get; init; }
}

/// <summary>nfsu2-web GARAGE.DAT (N2GA) + 10-slot werkbuffer.</summary>
public static class GarageDatService
{
    public static ulong Fingerprint(byte[] career)
    {
        if (career.Length < Layout.ProfilePayloadOff + Layout.ProfileCarsPartSize)
            throw new InvalidDataException("Career file too small for fingerprint.");
        ulong hash = 0xcbf29ce484222325UL;
        int end = Layout.ProfilePayloadOff + Layout.ProfileCarsPartSize;
        for (int i = Layout.ProfilePayloadOff; i < end; i++)
        {
            hash ^= career[i];
            hash *= 0x100000001b3UL;
        }
        return hash;
    }

    public static ulong StoredFingerprint(byte[] garage) =>
        BitConverter.ToUInt64(garage, 8);

    public static void SetFingerprint(byte[] garage, byte[] career)
    {
        ulong fp = Fingerprint(career);
        BitConverter.GetBytes(fp).CopyTo(garage, 8);
    }

    public static bool LooksLikeCareer(byte[] data)
    {
        if (data.Length != Layout.ProfileSize) return false;
        for (int i = 0; i < 4; i++)
            if (data[i] != Layout.CareerMagic[i]) return false;
        return true;
    }

    public static bool LooksLikeGarage(byte[] data)
    {
        if (data.Length != Layout.GarageSize) return false;
        for (int i = 0; i < 4; i++)
            if (data[i] != Layout.GarageMagic[i]) return false;
        return BitConverter.ToUInt32(data, 4) == 1;
    }

    public static (GarageState State, string Note) Check(byte[]? garage, byte[] career)
    {
        if (!LooksLikeCareer(career))
            return (GarageState.Invalid, "Not a valid 20CM career save (54966 bytes).");
        if (garage == null)
            return (GarageState.Missing, "No GARAGE.DAT — slots 1–5 only.");
        if (!LooksLikeGarage(garage))
            return (GarageState.Invalid, "GARAGE.DAT is not nfsu2-web N2GA v1.");
        if (StoredFingerprint(garage) != Fingerprint(career))
            return (GarageState.Stale, "Fingerprint mismatch — browser will not load slots 6–10.");
        return (GarageState.Loaded, "GARAGE.DAT OK — slots 1–10 available.");
    }

    /// <summary>Career + optionele garage → werkbuffer (54966 of extended).</summary>
    public static byte[] Merge(byte[] career, byte[]? garage, GarageState state)
    {
        if (!LooksLikeCareer(career)) throw new InvalidDataException("Invalid career save.");
        if (state != GarageState.Loaded || garage == null)
            return (byte[])career.Clone();

        // Extended buffer: career + 5 raw SAVE_REC blocks (GARAGE body @ 0x20)
        var work = new byte[Layout.ExtraSlotRecordBase + Layout.CareerSlotCount * Layout.SlotStride];
        Buffer.BlockCopy(career, 0, work, 0, Layout.ProfileSize);
        Buffer.BlockCopy(garage, Layout.GarageHeaderSize, work, Layout.ExtraSlotRecordBase,
            Layout.CareerSlotCount * Layout.SlotStride);
        return work;
    }

    public static byte[] BuildGarage(byte[] originalGarage, byte[] working, byte[] careerAfterCrc)
    {
        if (!LooksLikeGarage(originalGarage))
            throw new InvalidDataException("Original GARAGE.DAT invalid.");
        if (working.Length < Layout.ExtraSlotRecordBase + Layout.CareerSlotCount * Layout.SlotStride)
            throw new InvalidDataException("Working buffer has no slots 6–10.");

        var outG = (byte[])originalGarage.Clone();
        // Behoud header-rest 0x10..0x1F (drive/qr/stand), update body + fingerprint
        Buffer.BlockCopy(working, Layout.ExtraSlotRecordBase, outG, Layout.GarageHeaderSize,
            Layout.CareerSlotCount * Layout.SlotStride);
        SetFingerprint(outG, careerAfterCrc);
        return outG;
    }

    public static byte[] CreateEmptyGarage(byte[] career)
    {
        var g = new byte[Layout.GarageSize];
        Layout.GarageMagic.CopyTo(g, 0);
        BitConverter.GetBytes(1u).CopyTo(g, 4);
        SetFingerprint(g, career);
        // Lege SAVE_REC slots zoals Joey: unlocked (lock=0), geen auto — browser toont ze dan na paste
        for (int i = 0; i < Layout.CareerSlotCount; i++)
            InitEmptySponsorSaveRec(g, Layout.GarageHeaderSize + i * Layout.SlotStride, i);
        return g;
    }

    /// <summary>Joey-style lege sponsor SAVE_REC: handle A6+i, lock=0, bought=0.</summary>
    public static void InitEmptySponsorSaveRec(byte[] garageOrWorking, int saveRecOff, int garageIndex0to4)
    {
        garageOrWorking[saveRecOff + 4] = (byte)(0xA6 + garageIndex0to4);
        garageOrWorking[saveRecOff + 5] = 0x8C;
        garageOrWorking[saveRecOff + 6] = 0x9A;
        garageOrWorking[saveRecOff + 7] = 0x31;
        garageOrWorking[saveRecOff + 12] = 1;
        garageOrWorking[saveRecOff + 16] = 4;
        garageOrWorking[saveRecOff + Layout.LockInSaveRec] = 0;
        garageOrWorking[saveRecOff + Layout.BoughtInSaveRec] = 0;
    }

    /// <summary>Zet lock=0 op lege sponsor-slots (browser negeert locked empties minder, paste moet zichtbaar zijn).</summary>
    public static void UnlockAllSponsorSlots(byte[] working)
    {
        if (working.Length < Layout.ExtraSlotRecordBase + Layout.CareerSlotCount * Layout.SlotStride)
            return;
        for (int i = 0; i < Layout.CareerSlotCount; i++)
            UnlockSlot(working, Layout.CareerSlotCount + i);
    }

    public static void UpdateGarageCarCount(byte[] working)
    {
        int n = working.Length >= Layout.ExtraSlotRecordBase + Layout.CareerSlotCount * Layout.SlotStride
            ? Layout.MaxSlotCount
            : Layout.CareerSlotCount;
        int highest = 0;
        for (int i = 0; i < n; i++)
        {
            if (ReadSlot(working, i).IsOccupied)
                highest = i + 1;
        }
        working[Layout.GarageCarCountOffset] = (byte)Math.Clamp(Math.Max(5, highest), 1, 10);
    }

    public static List<SlotInfo> ReadSlots(byte[] working)
    {
        int count = working.Length >= Layout.ExtraSlotRecordBase + Layout.CareerSlotCount * Layout.SlotStride
            ? Layout.MaxSlotCount
            : Layout.CareerSlotCount;
        var list = new List<SlotInfo>(count);
        for (int i = 0; i < count; i++)
            list.Add(ReadSlot(working, i));
        return list;
    }

    public static SlotInfo ReadSlot(byte[] working, int index)
    {
        int tuned = Layout.TunedOffset(index);
        if (tuned + Layout.TunedCarSize > working.Length)
            throw new ArgumentOutOfRangeException(nameof(index));

        byte bought = working[tuned + Layout.BoughtInTuned];
        byte locked = working[tuned + Layout.LockInTuned];
        uint hash = BitConverter.ToUInt32(working, tuned + Layout.CarHashInTuned);
        bool occupied = bought == 1 && hash != 0;
        string x = occupied ? CarNames.ResolveXName(hash) : "(empty)";
        return new SlotInfo
        {
            Index = index,
            IsOccupied = occupied,
            IsLocked = locked != 0,
            CarHash = occupied ? hash : 0,
            XName = x,
            DisplayName = occupied ? CarNames.Display(hash) : (locked != 0 ? "(locked)" : "(empty)"),
        };
    }

    /// <summary>
    /// Marks a slot owned/unlocked for nfsu2-web.
    /// Does NOT write PC-style "01CR"/"CR01" over the car body (that breaks Add car + slots 6–10).
    /// Slots 6–10 keep Joey-style handles at tuned[0..3] (= SAVE_REC+4).
    /// </summary>
    public static void WriteSlotIds(byte[] tuned, int careerIndex0to9)
    {
        if (careerIndex0to9 >= Layout.CareerSlotCount)
        {
            int gi = careerIndex0to9 - Layout.CareerSlotCount;
            tuned[0] = (byte)(0xA6 + gi);
            tuned[1] = 0x8C;
            tuned[2] = 0x9A;
            tuned[3] = 0x31;
        }
        // Common record fields (donor blocks already have these; keep if present)
        if (tuned.Length > 12 && tuned[8] == 0 && tuned[12] == 0)
        {
            tuned[8] = 1;
            tuned[12] = 4;
        }
        if (tuned.Length > Layout.BoughtInTuned) tuned[Layout.BoughtInTuned] = 1;
        if (tuned.Length > Layout.LockInTuned) tuned[Layout.LockInTuned] = 0;
    }

    /// <summary>Joey/nfsu2-web handle at SAVE_REC+4 for slots 6–10 only.</summary>
    public static void EnsureExtraSlotHandle(byte[] working, int slotIndex)
    {
        if (slotIndex < Layout.CareerSlotCount) return;
        int saveRec = SaveRecOffsetSafe(working, slotIndex);
        int gi = slotIndex - Layout.CareerSlotCount;
        working[saveRec + 4] = (byte)(0xA6 + gi);
        working[saveRec + 5] = 0x8C;
        working[saveRec + 6] = 0x9A;
        working[saveRec + 7] = 0x31;
    }

    private static int SaveRecOffsetSafe(byte[] working, int slotIndex)
    {
        int saveRec = Layout.SaveRecOffset(slotIndex);
        if (saveRec + Layout.BoughtInSaveRec >= working.Length)
            throw new InvalidOperationException("Slot does not exist in this buffer.");
        return saveRec;
    }

    public static void WriteRecord(byte[] working, int slotIndex, byte[] tuned, byte[] inventory)
    {
        if (tuned.Length != Layout.TunedCarSize) throw new ArgumentException("Tuned size");
        if (inventory.Length != Layout.InventorySize) throw new ArgumentException("Inv size");

        int saveRec = Layout.SaveRecOffset(slotIndex);
        int tunedOff = Layout.TunedOffset(slotIndex);
        // Extra slots: SAVE_REC is exactly SlotStride; don't overflow into next record.
        int maxFromTuned = slotIndex >= Layout.CareerSlotCount
            ? Layout.SlotStride - Layout.SaveRecPrefix
            : Layout.SlotStride;
        if (saveRec < 0 || tunedOff + Math.Min(Layout.TunedCarSize + Layout.InventorySize, maxFromTuned) > working.Length)
            throw new InvalidOperationException("Slot does not exist in this buffer (open GARAGE.DAT for slots 6–10).");

        var t = (byte[])tuned.Clone();
        WriteSlotIds(t, slotIndex);
        Buffer.BlockCopy(t, 0, working, tunedOff, Layout.TunedCarSize);

        int invBytes = Math.Min(Layout.InventorySize, maxFromTuned - Layout.TunedCarSize);
        if (invBytes > 0)
            Buffer.BlockCopy(inventory, 0, working, tunedOff + Layout.TunedCarSize, invBytes);

        // Ensure lock/bought on SAVE_REC absolute offsets (browser eist bought=1 + lock=0)
        working[saveRec + Layout.LockInSaveRec] = 0;
        working[saveRec + Layout.BoughtInSaveRec] = 1;
        EnsureExtraSlotHandle(working, slotIndex);
        UpdateGarageCarCount(working);
    }

    public static void ReadRecord(byte[] working, int slotIndex, out byte[] tuned, out byte[] inventory)
    {
        int tunedOff = Layout.TunedOffset(slotIndex);
        tuned = new byte[Layout.TunedCarSize];
        inventory = new byte[Layout.InventorySize];
        Buffer.BlockCopy(working, tunedOff, tuned, 0, Layout.TunedCarSize);

        int maxFromTuned = slotIndex >= Layout.CareerSlotCount
            ? Layout.SlotStride - Layout.SaveRecPrefix
            : Layout.SlotStride;
        int invBytes = Math.Min(Layout.InventorySize, maxFromTuned - Layout.TunedCarSize);
        if (invBytes > 0 && tunedOff + Layout.TunedCarSize + invBytes <= working.Length)
            Buffer.BlockCopy(working, tunedOff + Layout.TunedCarSize, inventory, 0, invBytes);
    }

    public static void UnlockSlot(byte[] working, int slotIndex)
    {
        int saveRec = Layout.SaveRecOffset(slotIndex);
        if (saveRec + Layout.LockInSaveRec >= working.Length)
            throw new InvalidOperationException("Slot does not exist in this buffer.");
        working[saveRec + Layout.LockInSaveRec] = 0;
    }

    /// <summary>Empty unlocked slot — ready for Add car / paste.</summary>
    public static void ClearSlot(byte[] working, int slotIndex)
    {
        int saveRec = Layout.SaveRecOffset(slotIndex);
        int tuned = Layout.TunedOffset(slotIndex);
        if (slotIndex >= Layout.CareerSlotCount)
        {
            Array.Clear(working, saveRec, Layout.SlotStride);
            InitEmptySponsorSaveRec(working, saveRec, slotIndex - Layout.CareerSlotCount);
            return;
        }
        Array.Clear(working, saveRec, Layout.SlotStride);
        working[saveRec + 4] = (byte)(0xA1 + slotIndex);
        working[saveRec + 5] = 0x8C;
        working[saveRec + 6] = 0x9A;
        working[saveRec + 7] = 0x31;
        working[saveRec + 12] = 1;
        working[saveRec + 16] = 4;
        working[saveRec + Layout.LockInSaveRec] = 0;
        working[saveRec + Layout.BoughtInSaveRec] = 0;
        // Keep tuned region structurally sane
        if (tuned + 12 < working.Length)
        {
            working[tuned + 8] = 1;
            working[tuned + 12] = 4;
        }
    }

    public static void ClearAllSlots(byte[] working)
    {
        int n = working.Length >= Layout.ExtraSlotRecordBase + Layout.CareerSlotCount * Layout.SlotStride
            ? Layout.MaxSlotCount
            : Layout.CareerSlotCount;
        for (int i = 0; i < n; i++)
            ClearSlot(working, i);
        working[Layout.GarageCarCountOffset] = 5;
        UpdateGarageCarCount(working);
    }

    public static void EnsureExtended(ref byte[] working, byte[] career, byte[]? garage)
    {
        if (working.Length >= Layout.ExtraSlotRecordBase + Layout.CareerSlotCount * Layout.SlotStride)
            return;
        var g = garage ?? CreateEmptyGarage(career);
        if (!LooksLikeGarage(g)) g = CreateEmptyGarage(career);
        working = Merge(career, g, GarageState.Loaded);
    }
}
