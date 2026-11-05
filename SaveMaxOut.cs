namespace NFSU2CustomCarSaves;

/// <summary>
/// Browser-safe 100% free-play (Jane-style): car lot + shops + career unlock flags.
/// Does NOT change cars in garage slots (no tune / no replace).
/// Jane template is the reference (Templates/JANE). Do not invent stage "finished"
/// bytes — those offsets sit inside event records and corrupting them re-triggers
/// Rachel / intro speech and can break specialty shop access.
/// </summary>
public static class SaveMaxOut
{
    public const int CarLotOffset = 0xBC;
    public const int CarLotEnd = 0x300; // exclusive
    public const int WorldStateOffset = 0xAD80;
    public const int WorldStateLen = 0xAD9A - 0xAD80; // 26
    public const int HiddenShopsOffset = 0xAD9A;
    public const int HiddenShopsLen = 58; // 0x3A — Starladdey / map discovery flags
    public const int CareerCompleteFlag = 0xA17A;
    /// <summary>Global performance / specialty shop tier table (Starladdey).</summary>
    public const int GlobalShopTierStart = 0xC45B;
    public const int GlobalShopTierRow1End = 0xC47B;
    public const int GlobalShopTierRow2Start = 0xC483;
    public const int GlobalShopTierRow2End = 0xC4A3;
    public const int GlobalShopTierStride = 8;
    public const byte GlobalShopTierMax = 3;

    public static string Apply(byte[] working)
    {
        var log = new List<string>();
        byte[] jane = LoadJaneTemplate();

        // 1) Car lot unlock table (stock cars in shop) — Jane floats @ 0xBC
        CopyRange(working, jane, CarLotOffset, CarLotEnd - CarLotOffset);
        log.Add("Car lot → Jane (shop stock cars unlocked)");

        // 2) World state + hidden specialty shops on the map
        CopyRange(working, jane, WorldStateOffset, WorldStateLen);
        CopyRange(working, jane, HiddenShopsOffset, HiddenShopsLen);
        for (int i = 0; i < HiddenShopsLen; i++)
            working[HiddenShopsOffset + i] = 1;
        log.Add("Specialty shops → all unlocked on map");

        // 3) Global shop catalog tiers (Street/Pro/Extreme) — needed for specialty parts
        SetGlobalShopTiers(working, GlobalShopTierMax);
        log.Add("Shop catalogs → Extreme (tier 3)");

        // 4) Career progress from Jane (stages / markers / free-roam) — do NOT poke
        //    individual stage tail bytes; that corrupts event hashes and restarts intro.
        int baseOff = Layout.CareerSaveBase;
        CopyRange(working, jane, baseOff + Layout.CareerStage0Rel, Layout.CareerStageCount * Layout.CareerStageSize);
        CopyRange(working, jane, baseOff + Layout.CareerMoneyMarkersRel, Layout.CareerMoneyMarkersLen);
        CopyRange(working, jane, baseOff + Layout.CareerField8869Rel, Layout.CareerField8869Len);

        BitConverter.GetBytes(0).CopyTo(working, baseOff + Layout.CareerCurrentStageRel);
        BitConverter.GetBytes(0).CopyTo(working, baseOff + Layout.CareerNumUsed7B00Rel);
        Array.Clear(working, baseOff + Layout.Career7B00Rel, Layout.Career7B00Count * Layout.Career7B00Stride);
        working[baseOff + Layout.CareerIsStartedRel] = 1;
        working[CareerCompleteFlag] = 1;
        log.Add("Career → Jane free-play (complete flag on; intro/Rachel not restarted)");

        // 5) Money only — leave profile name alone
        int money = Math.Min(99_999_999, Layout.MaxMoney);
        BitConverter.GetBytes(money).CopyTo(working, Layout.MoneyOffset);
        log.Add($"Money → {money:N0}");

        // 6) Unlock slot locks only — never retune / rewrite cars
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

    private static void SetGlobalShopTiers(byte[] working, byte tier)
    {
        for (int o = GlobalShopTierStart; o <= GlobalShopTierRow1End; o += GlobalShopTierStride)
        {
            if (o < working.Length && working[o] < tier)
                working[o] = tier;
        }
        for (int o = GlobalShopTierRow2Start; o <= GlobalShopTierRow2End; o += GlobalShopTierStride)
        {
            if (o < working.Length && working[o] < tier)
                working[o] = tier;
        }
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
