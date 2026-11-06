namespace NFSU2CustomCarSaves;

/// <summary>
/// Career / player stats helpers for the Stats editor — money, completion flag,
/// transmission, shop unlocks, Jane section applies, and win-record force.
/// Edits the open working buffer only (caller marks Dirty).
/// </summary>
public static class CareerStatsService
{
    /// <summary>UI ceiling = in-game bank max ($999,999,999).</summary>
    public const int UiMoneyMax = Layout.MaxMoney;

    public static bool BufferOk(byte[] working) =>
        working.Length > SaveMaxOut.CareerCompleteFlag
        && PlayerOptionsService.BufferHasMoney(working);

    public static int ReadMoney(byte[] working) => PlayerOptionsService.GetMoney(working);

    public static void SetMoney(byte[] working, int amount)
    {
        if (amount < 0) amount = 0;
        if (amount > UiMoneyMax) amount = UiMoneyMax;
        PlayerOptionsService.SetMoney(working, amount);
    }

    public static bool IsCareerComplete(byte[] working)
    {
        if (working.Length <= SaveMaxOut.CareerCompleteFlag) return false;
        return working[SaveMaxOut.CareerCompleteFlag] != 0;
    }

    public static void SetCareerComplete(byte[] working, bool complete)
    {
        if (working.Length <= SaveMaxOut.CareerCompleteFlag)
            throw new InvalidOperationException("Save buffer too small for career complete flag.");
        working[SaveMaxOut.CareerCompleteFlag] = (byte)(complete ? 1 : 0);
    }

    public static bool GetCareerStarted(byte[] working)
    {
        int off = Layout.CareerSaveBase + Layout.CareerIsStartedRel;
        if (working.Length <= off) return false;
        return working[off] != 0;
    }

    public static void SetCareerStarted(byte[] working, bool started)
    {
        int off = Layout.CareerSaveBase + Layout.CareerIsStartedRel;
        if (working.Length <= off)
            throw new InvalidOperationException("Save buffer too small for career started flag.");
        working[off] = (byte)(started ? 1 : 0);
    }

    public static PlayerOptionsService.TransmissionMode GetTransmission(byte[] working) =>
        PlayerOptionsService.GetTransmission(working);

    public static void SetTransmission(byte[] working, PlayerOptionsService.TransmissionMode mode) =>
        PlayerOptionsService.SetTransmission(working, mode);

    /// <summary>
    /// Garage slot counter byte — read-only for the Stats UI. Arbitrary writes can
    /// desync occupied slots; use <see cref="SyncGarageCarCount"/> instead.
    /// </summary>
    public static int GetGarageCarCount(byte[] working)
    {
        if (working.Length <= Layout.GarageCarCountOffset) return 0;
        return working[Layout.GarageCarCountOffset];
    }

    /// <summary>
    /// Safe: recompute counter like 100% Save (<see cref="GarageDatService.UpdateGarageCarCount"/>, minUnlocked 5).
    /// </summary>
    public static string SyncGarageCarCount(byte[] working)
    {
        GarageDatService.UpdateGarageCarCount(working);
        return $"Garage counter → {GetGarageCarCount(working)}";
    }

    /// <summary>
    /// Unlock all garage slots (CareerSlotCount or MaxSlotCount when ExtraSlotRecordBase fits),
    /// then UpdateGarageCarCount (minUnlocked 5). Cars unchanged.
    /// </summary>
    public static string UnlockAllGarageSlots(byte[] working)
    {
        int slotCount = SlotCountForBuffer(working);
        for (int i = 0; i < slotCount; i++)
            GarageDatService.UnlockSlot(working, i);
        GarageDatService.UpdateGarageCarCount(working);
        return $"Slots unlocked: {slotCount} · garage → {GetGarageCarCount(working)}";
    }

    /// <summary>
    /// Full Jane / 100% Save unlocks via <see cref="SaveMaxOut.Apply"/>, optionally keeping
    /// money and transmission. Does not retune cars. When <paramref name="preserveMoney"/> is
    /// false, Apply already writes <see cref="Layout.MaxMoney"/>.
    /// </summary>
    public static string ApplyFullyModded(byte[] working, bool preserveMoney = false, bool preserveTransmission = true)
    {
        int keepMoney = preserveMoney ? ReadMoney(working) : -1;
        var keepTx = preserveTransmission ? GetTransmission(working) : default;
        string report = SaveMaxOut.Apply(working);
        if (preserveMoney)
        {
            SetMoney(working, keepMoney);
            report += $" · money kept at {keepMoney:N0}";
        }
        if (preserveTransmission)
        {
            SetTransmission(working, keepTx);
            report += $" · transmission kept ({keepTx})";
        }
        return report;
    }

    /// <summary>One-line modded checklist for the Stats UI.</summary>
    public static string DescribeModdedStatus(byte[] working)
    {
        if (!BufferOk(working))
            return "Modded: unavailable (buffer too small).";

        int money = ReadMoney(working);
        bool done = IsCareerComplete(working);
        bool started = GetCareerStarted(working);
        int shops = CountHiddenShopsUnlocked(working);
        bool extreme = AreShopCatalogsExtreme(working);
        int slots = CountUnlockedSlots(working);
        int slotMax = SlotCountForBuffer(working);
        int garage = GetGarageCarCount(working);
        return $"Modded: money {money:N0}/{UiMoneyMax:N0} · complete {(done ? "yes" : "no")} · shops {shops}/{SaveMaxOut.HiddenShopsLen} · Extreme {(extreme ? "yes" : "no")} · started {(started ? "yes" : "no")} · slots unlocked {slots}/{slotMax} · garage {garage}";
    }

    private static int SlotCountForBuffer(byte[] working) =>
        working.Length >= Layout.ExtraSlotRecordBase + Layout.CareerSlotCount * Layout.SlotStride
            ? Layout.MaxSlotCount
            : Layout.CareerSlotCount;

    private static int CountUnlockedSlots(byte[] working)
    {
        int n = SlotCountForBuffer(working);
        int unlocked = 0;
        for (int i = 0; i < n; i++)
        {
            try
            {
                if (!GarageDatService.ReadSlot(working, i).IsLocked)
                    unlocked++;
            }
            catch
            {
                break;
            }
        }
        return unlocked;
    }

    public static int CountHiddenShopsUnlocked(byte[] working) =>
        SaveMaxOut.CountHiddenShopsUnlocked(working);

    public static string SetHiddenShopsUnlockedCount(byte[] working, int count) =>
        SaveMaxOut.SetHiddenShopsUnlockedCount(working, count);

    public static bool AreShopCatalogsExtreme(byte[] working) =>
        SaveMaxOut.AreShopCatalogsExtreme(working);

    public static string ApplyPerfectWinRecord(byte[] working) =>
        SaveMaxOut.ForcePerfectWinRecord(working);

    public static string ApplyCarLot(byte[] working) =>
        SaveMaxOut.ApplyCarLot(working);

    public static string ApplyWorldState(byte[] working) =>
        SaveMaxOut.ApplyWorldState(working);

    public static string ApplyCareerProgress(byte[] working, bool forceComplete = true) =>
        SaveMaxOut.ApplyCareerProgress(working, forceComplete);

    public static string ApplyRewardsAndSponsors(byte[] working) =>
        SaveMaxOut.ApplyRewardsAndSponsors(working);

    public static string UnlockAllHiddenShops(byte[] working) =>
        SaveMaxOut.UnlockAllHiddenShops(working);

    public static string SetShopCatalogsExtreme(byte[] working) =>
        SaveMaxOut.SetShopCatalogsExtreme(working);

    /// <summary>
    /// Jane race/career progress + perfect win record. Preserves money when requested.
    /// Also restores hidden-shop bytes: Career7B00 Jane copy overlaps HiddenShopsOffset.
    /// </summary>
    public static string ApplyJaneRaceAndCareerProgress(byte[] working, bool preserveMoney = true)
    {
        int keep = preserveMoney ? ReadMoney(working) : -1;
        byte[]? shops = SnapshotHiddenShops(working);
        string report = SaveMaxOut.ApplyCareerProgress(working, forceComplete: true);
        report += " · " + SaveMaxOut.ForcePerfectWinRecord(working);
        if (RestoreHiddenShops(working, shops))
            report += " · hidden shops preserved";
        if (preserveMoney)
        {
            SetMoney(working, keep);
            report += $" · money kept at {keep:N0}";
        }
        return report;
    }

    /// <summary>
    /// Jane stages + events (career progress block). Preserves money when requested.
    /// Restores hidden-shop bytes after Jane 7B00 copy (same overlap as race progress).
    /// </summary>
    public static string ApplyJaneStagesAndEvents(byte[] working, bool preserveMoney = true)
    {
        int keep = preserveMoney ? ReadMoney(working) : -1;
        byte[]? shops = SnapshotHiddenShops(working);
        string report = SaveMaxOut.ApplyCareerProgress(working, forceComplete: true);
        if (RestoreHiddenShops(working, shops))
            report += " · hidden shops preserved";
        if (preserveMoney)
        {
            SetMoney(working, keep);
            report += $" · money kept at {keep:N0}";
        }
        return report;
    }

    private static byte[]? SnapshotHiddenShops(byte[] working)
    {
        if (working.Length < SaveMaxOut.HiddenShopsOffset + SaveMaxOut.HiddenShopsLen)
            return null;
        var shops = new byte[SaveMaxOut.HiddenShopsLen];
        Buffer.BlockCopy(working, SaveMaxOut.HiddenShopsOffset, shops, 0, shops.Length);
        return shops;
    }

    private static bool RestoreHiddenShops(byte[] working, byte[]? shops)
    {
        if (shops == null || shops.Length != SaveMaxOut.HiddenShopsLen) return false;
        if (working.Length < SaveMaxOut.HiddenShopsOffset + SaveMaxOut.HiddenShopsLen) return false;
        Buffer.BlockCopy(shops, 0, working, SaveMaxOut.HiddenShopsOffset, shops.Length);
        return true;
    }

    /// <summary>
    /// Jane world/map state then unlock all hidden shops (same post-7B00 order as SaveMaxOut.Apply).
    /// World @ 0xAD80 and shops @ 0xAD9A both sit inside the Career7B00 span — apply after any Jane copy.
    /// </summary>
    public static string UnlockHiddenShopsAndWorld(byte[] working, bool preserveMoney = true)
    {
        int keep = preserveMoney ? ReadMoney(working) : -1;
        // World then shops — matches Apply(); ranges are adjacent (no mutual overwrite).
        string report = SaveMaxOut.ApplyWorldState(working);
        report += " · " + SaveMaxOut.UnlockAllHiddenShops(working);
        if (preserveMoney)
        {
            SetMoney(working, keep);
            report += $" · money kept at {keep:N0}";
        }
        return report;
    }

    /// <summary>
    /// Jane free-play unlocks (car lot, shops, stages, complete flag, win record, slots)
    /// without overwriting the money slider value when <paramref name="preserveMoney"/> is true.
    /// </summary>
    public static string ApplyJaneProgress(byte[] working, bool preserveMoney = true)
    {
        int keep = preserveMoney ? ReadMoney(working) : -1;
        string report = SaveMaxOut.Apply(working);
        if (preserveMoney)
        {
            SetMoney(working, keep);
            report += $" · money kept at {keep:N0}";
        }
        return report;
    }

    /// <summary>Full 100% Save defaults including money → MaxMoney.</summary>
    public static string ApplyFullMaxOut(byte[] working) => SaveMaxOut.Apply(working);

    // ── Packed stage reputation (Mosta / Jane) ─────────────────────────────

    public static int PackedStageRepOffset(int stageIndex1to5)
    {
        if (stageIndex1to5 < Layout.PackedStageMinIndex || stageIndex1to5 > Layout.PackedStageMaxIndex)
            throw new ArgumentOutOfRangeException(nameof(stageIndex1to5), "Stage must be 1..5.");
        return Layout.PackedStageBase + stageIndex1to5 * Layout.PackedStageStride + Layout.PackedStageRepRel;
    }

    public static int PackedStageRepRacesOffset(int stageIndex1to5) =>
        PackedStageRepOffset(stageIndex1to5) + 4;

    public static bool BufferHasPackedStages(byte[] working) =>
        working.Length > PackedStageRepRacesOffset(Layout.PackedStageMaxIndex);

    public static float GetStageTotalReputation(byte[] working, int stageIndex1to5)
    {
        int off = PackedStageRepOffset(stageIndex1to5);
        if (working.Length < off + 4)
            throw new InvalidOperationException("Save buffer too small for stage reputation.");
        return BitConverter.ToSingle(working, off);
    }

    public static byte GetStageRepRaceCount(byte[] working, int stageIndex1to5)
    {
        int off = PackedStageRepRacesOffset(stageIndex1to5);
        if (working.Length <= off)
            throw new InvalidOperationException("Save buffer too small for stage race count.");
        return working[off];
    }

    public static float GetStageAverageReputation(byte[] working, int stageIndex1to5)
    {
        int races = GetStageRepRaceCount(working, stageIndex1to5);
        if (races <= 0) return 0f;
        return GetStageTotalReputation(working, stageIndex1to5) / races;
    }

    public static bool BufferHasTotalSpent(byte[] working, int slot0to4 = 0) =>
        working.Length >= Layout.TotalSpentOnCarOffsetForSlot(slot0to4) + 4;

    public static int GetTotalSpentOnCar(byte[] working, int slot0to4 = 0)
    {
        if (!BufferHasTotalSpent(working, slot0to4))
            throw new InvalidOperationException("Save buffer too small for Total Spent.");
        return BitConverter.ToInt32(working, Layout.TotalSpentOnCarOffsetForSlot(slot0to4));
    }

    /// <summary>Snapshot of the full career working buffer (backup before edits).</summary>
    public static byte[] Snapshot(byte[] working) => (byte[])working.Clone();

    /// <summary>Restore a previous snapshot into <paramref name="working"/> (same length).</summary>
    public static StatsEditResult RestoreSnapshot(byte[] working, byte[] snapshot)
    {
        if (snapshot.Length != working.Length)
            throw new ArgumentException("Snapshot length must match working buffer.");
        var before = Snapshot(working);
        Buffer.BlockCopy(snapshot, 0, working, 0, working.Length);
        EnsureCareerChecksums(working);
        return Diff(before, working, "restore backup");
    }

    public static StatsEditResult SetStageTotalReputation(byte[] working, int stageIndex1to5, float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            throw new ArgumentOutOfRangeException(nameof(value), "Reputation must be a finite ≥ 0 float.");
        int off = PackedStageRepOffset(stageIndex1to5);
        return Mutate(working, $"stage {stageIndex1to5} total rep → {value}", () =>
            BitConverter.GetBytes(value).CopyTo(working, off));
    }

    public static StatsEditResult SetStageRepRaceCount(byte[] working, int stageIndex1to5, int races)
    {
        if (races < 0 || races > 255)
            throw new ArgumentOutOfRangeException(nameof(races), "Race count must be 0…255.");
        int off = PackedStageRepRacesOffset(stageIndex1to5);
        return Mutate(working, $"stage {stageIndex1to5} rep-races → {races}", () =>
            working[off] = (byte)races);
    }

    /// <summary>Probable Total Spent for garage slot 0..3 — clamped ≥ 0. Slot 4 is unverified.</summary>
    public static StatsEditResult SetTotalSpentOnCar(byte[] working, int amount, int slot0to4 = 0)
    {
        if (slot0to4 < 0 || slot0to4 >= Layout.TotalSpentEditableSlotCount)
            throw new ArgumentOutOfRangeException(nameof(slot0to4),
                $"Total Spent edits are slots 0…{Layout.TotalSpentEditableSlotCount - 1} only.");
        if (!BufferHasTotalSpent(working, slot0to4))
            throw new InvalidOperationException("Save buffer too small for Total Spent.");
        if (amount < 0) amount = 0;
        int off = Layout.TotalSpentOnCarOffsetForSlot(slot0to4);
        return Mutate(working, $"total spent slot{slot0to4} → {amount}", () =>
            BitConverter.GetBytes(amount).CopyTo(working, off));
    }

    /// <summary>Unknown byte @ 0xA173 — RAW display only.</summary>
    public static byte GetUnknownNearBank(byte[] working)
    {
        if (working.Length <= Layout.UnknownNearBankOffset) return 0;
        return working[Layout.UnknownNearBankOffset];
    }

    public static bool BufferHasRacesEntered(byte[] working) =>
        working.Length >= Layout.RacesEnteredOffset + 4;

    /// <summary>Probable Races Entered @ 0xA16F (uint32 LE read; sample fits int32).</summary>
    public static uint GetRacesEntered(byte[] working)
    {
        if (!BufferHasRacesEntered(working))
            throw new InvalidOperationException("Save buffer too small for Races Entered.");
        return BitConverter.ToUInt32(working, Layout.RacesEnteredOffset);
    }

    /// <summary>
    /// Write probable Races Entered. Safe mode clamps to <see cref="Layout.RacesEnteredSafeMax"/>;
    /// Extended allows the full uint32 range (still stored as 4 LE bytes at 0xA16F).
    /// </summary>
    public static StatsEditResult SetRacesEntered(byte[] working, uint value, bool extended = false)
    {
        if (!BufferHasRacesEntered(working))
            throw new InvalidOperationException("Save buffer too small for Races Entered.");
        uint max = extended ? uint.MaxValue : (uint)Layout.RacesEnteredSafeMax;
        if (value > max) value = max;
        return Mutate(working, $"races entered → {value}", () =>
            BitConverter.GetBytes(value).CopyTo(working, Layout.RacesEnteredOffset));
    }

    /// <summary>Unknown byte after Bank — RAW display only.</summary>
    public static byte GetUnknownAfterBank(byte[] working)
    {
        if (working.Length <= Layout.UnknownAfterBankOffset) return 0;
        return working[Layout.UnknownAfterBankOffset];
    }

    public static void EnsureCareerChecksums(byte[] working)
    {
        if (working.Length < Layout.ProfileSize)
            throw new InvalidOperationException("Career buffer shorter than ProfileSize.");
        // Working may be extended (+ GARAGE.DAT slots). CRC only over the career prefix.
        if (working.Length == Layout.ProfileSize)
        {
            Checksum.RecalculateCareerChecksums(working);
            return;
        }
        var career = new byte[Layout.ProfileSize];
        Buffer.BlockCopy(working, 0, career, 0, Layout.ProfileSize);
        Checksum.RecalculateCareerChecksums(career);
        Buffer.BlockCopy(career, 0, working, 0, Layout.ProfileSize);
    }

    public static bool CareerChecksumsOk(byte[] working)
    {
        if (working.Length < Layout.ProfileSize) return false;
        if (working.Length == Layout.ProfileSize)
            return Checksum.CareerChecksumsMatch(working);
        var career = new byte[Layout.ProfileSize];
        Buffer.BlockCopy(working, 0, career, 0, Layout.ProfileSize);
        return Checksum.CareerChecksumsMatch(career);
    }

    private static StatsEditResult Mutate(byte[] working, string label, Action mutate)
    {
        var before = Snapshot(working);
        mutate();
        EnsureCareerChecksums(working);
        return Diff(before, working, label);
    }

    public static StatsEditResult Diff(byte[] before, byte[] after, string label)
    {
        int n = Math.Min(before.Length, after.Length);
        int changed = 0;
        var samples = new List<string>(8);
        for (int i = 0; i < n; i++)
        {
            if (before[i] == after[i]) continue;
            changed++;
            if (samples.Count < 8)
                samples.Add($"0x{i:X4}:{before[i]:X2}→{after[i]:X2}");
        }
        if (before.Length != after.Length)
            samples.Add($"len {before.Length}→{after.Length}");
        bool crc = CareerChecksumsOk(after);
        string preview = samples.Count == 0 ? "(no byte changes)" : string.Join(" ", samples);
        if (changed > 8) preview += $" … +{changed - 8} more";
        return new StatsEditResult(
            $"{label}: {changed} byte(s) · CRC {(crc ? "ok" : "BAD")} · {preview}",
            changed,
            preview,
            crc);
    }

    /// <summary>Map changed bytes to schema fields (or raw offsets) for the undo list.</summary>
    public static IReadOnlyList<StatsChangeEntry> DescribeChanges(byte[] before, byte[] after)
    {
        var fields = StatsSchemaStore.LoadAll()
            .Select(f => (Field: f, Off: f.ResolveOffset()))
            .Where(x => x.Off.HasValue)
            .OrderBy(x => x.Off!.Value)
            .ToList();
        var covered = new HashSet<int>();
        var list = new List<StatsChangeEntry>();
        int n = Math.Min(before.Length, after.Length);

        foreach (var (field, offN) in fields)
        {
            int off = offN!.Value;
            int size = SizeOfSchemaType(field.Type);
            if (off < 0 || off + size > n) continue;
            bool changed = false;
            for (int i = 0; i < size; i++)
            {
                if (before[off + i] != after[off + i]) { changed = true; break; }
            }
            if (!changed) continue;
            for (int i = 0; i < size; i++) covered.Add(off + i);
            list.Add(new StatsChangeEntry(
                field.Id,
                string.IsNullOrWhiteSpace(field.Label) ? field.Id : field.Label,
                off,
                FormatTyped(before, off, field.Type),
                FormatTyped(after, off, field.Type)));
        }

        // Leftover raw bytes (cluster runs)
        int runStart = -1;
        for (int i = 0; i <= n; i++)
        {
            bool diff = i < n && !covered.Contains(i) && before[i] != after[i];
            if (diff && runStart < 0) runStart = i;
            if ((!diff || i == n) && runStart >= 0)
            {
                int len = i - runStart;
                list.Add(new StatsChangeEntry(
                    $"raw_{runStart:X4}",
                    "Raw bytes",
                    runStart,
                    FormatHex(before, runStart, len),
                    FormatHex(after, runStart, len)));
                runStart = -1;
            }
        }
        return list;
    }

    public static int SizeOfSchemaType(string? type) => (type ?? "").ToLowerInvariant() switch
    {
        "int8" or "uint8" or "byte" => 1,
        "int16" or "uint16" => 2,
        "int32" or "uint32" or "float32" or "float" => 4,
        "int64" or "uint64" or "float64" or "double" => 8,
        _ => 4,
    };

    public static string FormatTyped(byte[] buf, int offset, string? type)
    {
        if (offset < 0 || offset >= buf.Length) return "?";
        string t = (type ?? "hex").ToLowerInvariant();
        try
        {
            return t switch
            {
                "int8" => ((sbyte)buf[offset]).ToString(),
                "uint8" or "byte" => buf[offset].ToString(),
                "int16" => BitConverter.ToInt16(buf, offset).ToString(),
                "uint16" => BitConverter.ToUInt16(buf, offset).ToString(),
                "int32" => BitConverter.ToInt32(buf, offset).ToString(),
                "uint32" => BitConverter.ToUInt32(buf, offset).ToString(),
                "float32" or "float" or "float_derived" => BitConverter.ToSingle(buf, offset).ToString("G9"),
                "hex" => FormatHex(buf, offset, Math.Min(4, buf.Length - offset)),
                _ => FormatHex(buf, offset, Math.Min(SizeOfSchemaType(t), buf.Length - offset)),
            };
        }
        catch
        {
            return FormatHex(buf, offset, 1);
        }
    }

    public static string FormatHex(byte[] buf, int offset, int len)
    {
        len = Math.Clamp(len, 0, Math.Max(0, buf.Length - offset));
        if (len <= 0) return "";
        return string.Join(" ", Enumerable.Range(0, len).Select(i => buf[offset + i].ToString("X2")));
    }

    public enum RawValueKind
    {
        Int8, UInt8, Int16, UInt16, Int32, UInt32, Float32, HexBytes
    }

    /// <summary>Write a typed value at absolute offset; caller should wrap with history. Recalculates CRC.</summary>
    public static StatsEditResult RawWrite(byte[] working, int offset, RawValueKind kind, string valueText)
    {
        if (offset < 0 || offset >= Layout.ProfileSize)
            throw new ArgumentOutOfRangeException(nameof(offset), "Offset must be inside the career profile.");
        byte[] payload = EncodeRaw(kind, valueText);
        if (offset + payload.Length > Layout.ProfileSize)
            throw new ArgumentOutOfRangeException(nameof(offset), "Write would exceed profile size.");
        return Mutate(working, $"raw @ 0x{offset:X} ({kind})", () =>
            Buffer.BlockCopy(payload, 0, working, offset, payload.Length));
    }

    public static byte[] EncodeRaw(RawValueKind kind, string text)
    {
        text = (text ?? "").Trim();
        return kind switch
        {
            RawValueKind.Int8 => [(byte)(sbyte)ParseLong(text)],
            RawValueKind.UInt8 => [(byte)ParseULong(text)],
            RawValueKind.Int16 => BitConverter.GetBytes(checked((short)ParseLong(text))),
            RawValueKind.UInt16 => BitConverter.GetBytes(checked((ushort)ParseULong(text))),
            RawValueKind.Int32 => BitConverter.GetBytes(checked((int)ParseLong(text))),
            RawValueKind.UInt32 => BitConverter.GetBytes(checked((uint)ParseULong(text))),
            RawValueKind.Float32 => BitConverter.GetBytes(float.Parse(text, System.Globalization.CultureInfo.InvariantCulture)),
            RawValueKind.HexBytes => ParseHexBytes(text),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    private static long ParseLong(string text)
    {
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return Convert.ToInt64(text, 16);
        return long.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static ulong ParseULong(string text)
    {
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return Convert.ToUInt64(text, 16);
        return ulong.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static byte[] ParseHexBytes(string text)
    {
        var parts = text.Split(new[] { ' ', '-', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) throw new FormatException("Hex value required.");
        var bytes = new byte[parts.Length];
        for (int i = 0; i < parts.Length; i++)
            bytes[i] = Convert.ToByte(parts[i], 16);
        return bytes;
    }

    public static string Summarize(byte[] working)
    {
        if (!BufferOk(working))
            return "Career stats unavailable (buffer too small).";

        int money = ReadMoney(working);
        bool done = IsCareerComplete(working);
        bool started = GetCareerStarted(working);
        var tx = GetTransmission(working);
        int garage = GetGarageCarCount(working);
        int shops = CountHiddenShopsUnlocked(working);
        bool extreme = AreShopCatalogsExtreme(working);
        string rep = "";
        if (BufferHasPackedStages(working))
        {
            float r5 = GetStageTotalReputation(working, 5);
            byte n5 = GetStageRepRaceCount(working, 5);
            rep = $" · S5 rep {r5:0.#}/{n5}";
        }
        string spent = BufferHasTotalSpent(working, 0) ? $" · spent0 {GetTotalSpentOnCar(working, 0):N0}" : "";
        string entered = BufferHasRacesEntered(working) ? $" · entered {GetRacesEntered(working):N0}" : "";
        return $"Stats: money {money:N0} · complete {(done ? "100%" : "off")} · started {(started ? "yes" : "no")} · {tx} · shops {shops}/{SaveMaxOut.HiddenShopsLen} · Extreme {(extreme ? "yes" : "no")} · garage {garage}{rep}{spent}{entered}";
    }
}

/// <summary>Result of a Stats buffer mutation (backup/diff + CRC).</summary>
public readonly record struct StatsEditResult(string Summary, int ChangedBytes, string DiffPreview, bool ChecksumsOk);
