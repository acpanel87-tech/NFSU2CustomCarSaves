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
    /// <summary>
    /// Career713C.field_8 (Career+0x7144). Game Completion % uses this plus Jane career progress.
    /// Races Entered / Overall Wins / Win% are NOT saved: nfsu2-re <c>CareerStats</c> is filled at
    /// runtime by <c>Career::UpdateCareerStatsObject</c> (money@+0, racesEntered@+4, overallWins@+8,
    /// winPercent@+C). No profile int32 pair for UI values like 246/222 exists near MoneyOffset
    /// (0xA16E is field_7138; the 246 @0xA16F is _pad7139 garbage). Do not invent race/win offsets.
    /// </summary>
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
        // 1) Car lot unlock table (stock cars in shop) — Jane floats @ 0xBC
        log.Add(ApplyCarLot(working));

        // 2) Global shop catalog tiers (Street/Pro/Extreme) — needed for specialty parts
        log.Add(SetShopCatalogsExtreme(working));

        // 3) Career progress — copy Jane as one coherent block (7148 + stages + 7B00 + 887C + markers).
        //    Do NOT wipe 7B00 or poke stage tail bytes; that desyncs event hashes and
        //    can crash career My Cars / free-play navigation in the browser.
        //    Jane 7B00 also contains world/map + specialty-shop bytes (overlap @ 0xAD80…).
        log.Add(ApplyCareerProgress(working, forceComplete: true));

        // 4) Force win-record flags so UpdateCareerStatsObject can report wins==entered (100% Win%).
        //    Skips the hidden-shop window inside 7B00.
        log.Add(ForcePerfectWinRecord(working));

        // 5) World + specialty shops AFTER Jane+win — world reaffirms Jane map; shops force all 58
        //    (Jane template only has 51/58). Order matches UnlockHiddenShopsAndWorld intent:
        //    do not let an early world/shop write get wiped by the 7B00 Jane copy.
        log.Add(ApplyWorldState(working));
        log.Add(UnlockAllHiddenShops(working));

        // 7) Money only — leave profile name alone.
        int money = Layout.MaxMoney;
        BitConverter.GetBytes(money).CopyTo(working, Layout.MoneyOffset);
        log.Add($"Money → {money:N0}");

        // 8) Unlock slot locks only — never retune / rewrite cars.
        //    Keep UpdateGarageCarCount (minUnlocked: 5) like pre-Stats Apply — Sync(min=1)
        //    was a regression that hid empty Add-car slots after 100% Save.
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

    /// <summary>Jane car lot stock unlock table @ 0xBC.</summary>
    public static string ApplyCarLot(byte[] working)
    {
        byte[] jane = LoadJaneTemplate();
        CopyRange(working, jane, CarLotOffset, CarLotEnd - CarLotOffset);
        return "Car lot → Jane (shop stock cars unlocked)";
    }

    /// <summary>Jane world / map state block (before hidden-shop flags).</summary>
    public static string ApplyWorldState(byte[] working)
    {
        byte[] jane = LoadJaneTemplate();
        CopyRange(working, jane, WorldStateOffset, WorldStateLen);
        return "World state → Jane";
    }

    /// <summary>
    /// Jane career progress block: 7148 + stages + current + numUsed + 7B00 + IsStarted
    /// + moneyMarkers + 8869 + 887C. Optional complete flag=1. No win-force, shops, money, or slots.
    /// </summary>
    public static string ApplyCareerProgress(byte[] working, bool forceComplete = true)
    {
        byte[] jane = LoadJaneTemplate();
        int baseOff = Layout.CareerSaveBase;
        CopyRange(working, jane, baseOff + Layout.Career7148Rel, Layout.Career7148Size);
        CopyRange(working, jane, baseOff + Layout.CareerStage0Rel, Layout.CareerStageCount * Layout.CareerStageSize);
        CopyRange(working, jane, baseOff + Layout.CareerCurrentStageRel, 4);
        CopyRange(working, jane, baseOff + Layout.CareerNumUsed7B00Rel, 4);
        CopyRange(working, jane, baseOff + Layout.Career7B00Rel, Layout.Career7B00Count * Layout.Career7B00Stride);
        CopyRange(working, jane, baseOff + Layout.CareerIsStartedRel, 1);
        CopyRange(working, jane, baseOff + Layout.CareerMoneyMarkersRel, Layout.CareerMoneyMarkersLen);
        CopyRange(working, jane, baseOff + Layout.CareerField8869Rel, Layout.CareerField8869Len);
        CopyRange(working, jane, baseOff + Layout.Career887CRel, Layout.Career887CSize);
        if (forceComplete)
            working[CareerCompleteFlag] = 1;
        return forceComplete
            ? "Career → Jane free-play (7148+stages+7B00+887C coherent; complete flag=1)"
            : "Career → Jane free-play (7148+stages+7B00+887C coherent; complete flag unchanged)";
    }

    /// <summary>
    /// Magazines / DVD / sponsors / visual path — copy ONLY Career7148 + Career887C from Jane.
    /// </summary>
    public static string ApplyRewardsAndSponsors(byte[] working)
    {
        byte[] jane = LoadJaneTemplate();
        int baseOff = Layout.CareerSaveBase;
        CopyRange(working, jane, baseOff + Layout.Career7148Rel, Layout.Career7148Size);
        CopyRange(working, jane, baseOff + Layout.Career887CRel, Layout.Career887CSize);
        return "Rewards/sponsors → Jane (7148+887C)";
    }

    /// <summary>Set all 58 hidden-shop discovery bytes to 1.</summary>
    public static string UnlockAllHiddenShops(byte[] working)
    {
        for (int i = 0; i < HiddenShopsLen; i++)
            working[HiddenShopsOffset + i] = 1;
        return "Specialty shops → all unlocked on map";
    }

    /// <summary>Public wrapper: raise global shop catalog tiers to Extreme (3).</summary>
    public static string SetShopCatalogsExtreme(byte[] working)
    {
        SetGlobalShopTiers(working, GlobalShopTierMax);
        return "Shop catalogs → Extreme (tier 3)";
    }

    /// <summary>Count hidden-shop flags that are non-zero (0…58).</summary>
    public static int CountHiddenShopsUnlocked(byte[] working)
    {
        if (working.Length < HiddenShopsOffset + HiddenShopsLen) return 0;
        int n = 0;
        for (int i = 0; i < HiddenShopsLen; i++)
        {
            if (working[HiddenShopsOffset + i] != 0)
                n++;
        }
        return n;
    }

    /// <summary>
    /// Set the first <paramref name="count"/> hidden-shop discovery bytes to 1 and the rest to 0.
    /// Count is clamped to 0…<see cref="HiddenShopsLen"/>.
    /// </summary>
    public static string SetHiddenShopsUnlockedCount(byte[] working, int count)
    {
        count = Math.Clamp(count, 0, HiddenShopsLen);
        for (int i = 0; i < HiddenShopsLen; i++)
            working[HiddenShopsOffset + i] = (byte)(i < count ? 1 : 0);
        return $"Hidden shops → {count}/{HiddenShopsLen}";
    }

    /// <summary>True when every global shop-tier byte is already ≥ Extreme (3).</summary>
    public static bool AreShopCatalogsExtreme(byte[] working)
    {
        for (int o = GlobalShopTierStart; o <= GlobalShopTierRow1End; o += GlobalShopTierStride)
        {
            if (o >= working.Length || working[o] < GlobalShopTierMax)
                return false;
        }
        for (int o = GlobalShopTierRow2Start; o <= GlobalShopTierRow2End; o += GlobalShopTierStride)
        {
            if (o >= working.Length || working[o] < GlobalShopTierMax)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Best-effort perfect win record for runtime <c>CareerStats.winPercent</c>.
    /// <list type="bullet">
    /// <item><c>Career7148</c> 8-byte rows: finish place byte+5 <c>5→1</c> (loss→win; observed in Jane/DYNO dumps).</item>
    /// <item><c>Career7B00</c>: exact flags <c>1,0,0,1</c> → <c>1,1,0,1</c>
    /// (nfsu2-re: field_5 = race finished/won). Shops and opaque rows are left alone.</item>
    /// </list>
    /// Does not invent stage+0xFC / outrunsRaced ints (opaque on disk; unsafe).
    /// </summary>
    public static string ForcePerfectWinRecord(byte[] working)
    {
        int baseOff = Layout.CareerSaveBase;
        int flipped7148 = 0;
        int sevenOff = baseOff + Layout.Career7B00Rel;
        int flipped7B00 = 0;

        int raceOff = baseOff + Layout.Career7148Rel;
        int raceEnd = raceOff + Layout.Career7148RaceBytes;
        for (int o = raceOff; o + Layout.Career7148RaceStride <= raceEnd; o += Layout.Career7148RaceStride)
        {
            uint hash = BitConverter.ToUInt32(working, o);
            if (hash == 0) continue;
            // Typical loss row: 00 05 00 xx — only flip the known loss place byte.
            if (working[o + 4] == 0 && working[o + 5] == 5 && working[o + 6] == 0)
            {
                working[o + 5] = 1;
                flipped7148++;
            }
        }

        int shopLo = HiddenShopsOffset;
        int shopHi = HiddenShopsOffset + HiddenShopsLen;
        for (int i = 0; i < Layout.Career7B00Count; i++)
        {
            int o = sevenOff + i * Layout.Career7B00Stride;
            // Starladdey shop bitfield overlaps mid-7B00 — never treat those bytes as race flags.
            if (o < shopHi && o + Layout.Career7B00Stride > shopLo)
                continue;
            uint hash = BitConverter.ToUInt32(working, o);
            if (hash == 0) continue;
            // Entered race, not marked finished/won — do not touch shop/opaque flag packs.
            if (working[o + 4] == 1 && working[o + 5] == 0 && working[o + 6] == 0 && working[o + 7] == 1)
            {
                working[o + 5] = 1;
                flipped7B00++;
            }
        }

        return $"Win% → forced perfect record (7148 losses→wins: {flipped7148}; 7B00 field_5: {flipped7B00})";
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
            "Jane template missing (Stock/Templates/JANE). Reinstall or copy Jane into Stock/Templates.");
    }

    private static IEnumerable<string> JaneCandidates() => AppPaths.JaneCareerCandidates();
}
