namespace NFSU2CustomCarSaves;

/// <summary>
/// Browser-safe 100% free-play (Jane-style): car lot + shops + career unlock flags.
/// Does NOT change cars in garage slots (no tune / no replace).
/// Jane template is the reference (Templates/JANE), then patched so specialty shops
/// are fully on and early career / Rachel intro is skipped.
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

        // 1) Car lot unlock table (stock cars in shop) — Jane floats @ 0xBC
        CopyRange(working, jane, CarLotOffset, CarLotEnd - CarLotOffset);
        log.Add("Car lot → Jane (shop stock cars unlocked)");

        // 2) Hidden specialty shops — Jane leaves some off; force every special open
        CopyRange(working, jane, HiddenShopsOffset, HiddenShopsLen);
        for (int i = 0; i < HiddenShopsLen; i++)
            working[HiddenShopsOffset + i] = 1;
        log.Add("Specialty shops → all unlocked");

        // 3) Career progress / free-roam flags from Jane (NOT the car slot records)
        int baseOff = Layout.CareerSaveBase;
        CopyRange(working, jane, baseOff + Layout.CareerStage0Rel, Layout.CareerStageCount * Layout.CareerStageSize);
        CopyRange(working, jane, baseOff + Layout.CareerMoneyMarkersRel, Layout.CareerMoneyMarkersLen);
        CopyRange(working, jane, baseOff + Layout.CareerField8869Rel, Layout.CareerField8869Len);

        // Stage pointer + URL table: empty (not stuck mid-mission / URL)
        BitConverter.GetBytes(0).CopyTo(working, baseOff + Layout.CareerCurrentStageRel);
        BitConverter.GetBytes(0).CopyTo(working, baseOff + Layout.CareerNumUsed7B00Rel);
        Array.Clear(working, baseOff + Layout.Career7B00Rel, Layout.Career7B00Count * Layout.Career7B00Stride);
        working[baseOff + Layout.CareerIsStartedRel] = 1;

        // Skip Rachel / early career speech: mark every stage finished (Jane leaves stage 0 open)
        for (int s = 0; s < Layout.CareerStageCount; s++)
        {
            int fin = baseOff + Layout.CareerStage0Rel + s * Layout.CareerStageSize + Layout.CareerStageFinishedRel;
            if (fin < working.Length) working[fin] = 1;
        }

        // field_8869[8][2] — keep Jane's structure but ensure unlocked (1) where still 0
        int f8869 = baseOff + Layout.CareerField8869Rel;
        for (int i = 0; i < Layout.CareerField8869Len && f8869 + i < working.Length; i++)
        {
            if (working[f8869 + i] == 0) working[f8869 + i] = 1;
        }

        log.Add("Career → free-play (stages finished; Rachel/intro speech skipped)");

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
