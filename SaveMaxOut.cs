namespace NFSU2CustomCarSaves;

/// <summary>
/// Browser-safe 100% free-play (Jane-style): car lot + shops + career unlock flags.
/// Does NOT change cars in garage slots (no tune / no replace).
/// Jane template is the reference (Templates/JANE).
/// </summary>
public static class SaveMaxOut
{
    public const int CarLotOffset = 0xBC;
    public const int CarLotEnd = 0x300; // exclusive
    public const int HiddenShopsOffset = 0xAD9A;
    public const int HiddenShopsLen = 58;

    public static string Apply(byte[] working)
    {
        var log = new List<string>();
        byte[] jane = LoadJaneTemplate();

        // 1) Car lot unlock table (stock cars in shop) — Jane/Joey floats @ 0xBC
        CopyRange(working, jane, CarLotOffset, CarLotEnd - CarLotOffset);
        log.Add("Car lot → Jane (shop stock cars unlocked)");

        // 2) Hidden specialty shops
        CopyRange(working, jane, HiddenShopsOffset, HiddenShopsLen);
        log.Add("Hidden shops → Jane");

        // 3) Career progress / free-roam flags from Jane (NOT the car slot records)
        int baseOff = Layout.CareerSaveBase;
        CopyRange(working, jane, baseOff + Layout.CareerStage0Rel, Layout.CareerStageCount * Layout.CareerStageSize);
        CopyRange(working, jane, baseOff + Layout.CareerMoneyMarkersRel, Layout.CareerMoneyMarkersLen);
        CopyRange(working, jane, baseOff + Layout.CareerField8869Rel, Layout.CareerField8869Len);

        // Stage pointer + URL table: Jane is stage=0 / numUsed=0 / empty 7B00 (not stuck mid-mission)
        BitConverter.GetBytes(0).CopyTo(working, baseOff + Layout.CareerCurrentStageRel);
        BitConverter.GetBytes(0).CopyTo(working, baseOff + Layout.CareerNumUsed7B00Rel);
        Array.Clear(working, baseOff + Layout.Career7B00Rel, Layout.Career7B00Count * Layout.Career7B00Stride);
        working[baseOff + Layout.CareerIsStartedRel] = 1;
        log.Add("Career → Jane free-play (stages/markers; not stuck in Rachel/URL)");

        // 4) Money only — leave profile name alone
        int money = Math.Min(99_999_999, Layout.MaxMoney);
        BitConverter.GetBytes(money).CopyTo(working, Layout.MoneyOffset);
        log.Add($"Money → {money:N0}");

        // 5) Unlock slot locks only — never retune / rewrite cars
        int slotCount = working.Length >= Layout.ExtraSlotRecordBase + Layout.CareerSlotCount * Layout.SlotStride
            ? Layout.MaxSlotCount
            : Layout.CareerSlotCount;
        for (int i = 0; i < slotCount; i++)
            GarageDatService.UnlockSlot(working, i);
        GarageDatService.UpdateGarageCarCount(working);
        log.Add($"Slots unlocked: {slotCount} (cars unchanged)");
        log.Add($"Garage counter → {working[Layout.GarageCarCountOffset]}");

        return string.Join(" · ", log);
    }

    private static void CopyRange(byte[] dst, byte[] src, int offset, int length)
    {
        if (offset < 0 || length <= 0) return;
        if (offset + length > dst.Length || offset + length > src.Length)
            throw new InvalidOperationException($"Cannot copy Jane unlock block @ 0x{offset:X} len {length}.");
        Buffer.BlockCopy(src, offset, dst, offset, length);
    }

    private static byte[] LoadJaneTemplate()
    {
        foreach (string path in JaneCandidates())
        {
            if (!File.Exists(path)) continue;
            byte[] data = File.ReadAllBytes(path);
            if (data.Length == Layout.ProfileSize && GarageDatService.LooksLikeCareer(data))
                return data;
        }
        throw new FileNotFoundException(
            "Jane template missing (Templates/JANE). Reinstall or copy Jane next to the app.");
    }

    private static IEnumerable<string> JaneCandidates()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "Templates", "JANE");
        yield return Path.Combine(AppContext.BaseDirectory, "Templates", "Jane");
        yield return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Templates", "JANE"));
    }
}
