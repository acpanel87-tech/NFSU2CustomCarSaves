namespace NFSU2CustomCarSaves;

/// <summary>Performance / unlock edit voor één career-slot (duidelijke labels).</summary>
public enum TuneLevel
{
    Stock = 0,
    Street = 1,
    Pro = 2,
    Extreme = 3,
}

/// <summary>MAX OUT: install Extreme packages, or only own/unlock them.</summary>
public enum MaxOutPerfMode
{
    InstallExtreme = 0,
    OwnOnly = 1,
}

/// <summary>MAX OUT unique flags: none, legit first-3 in order, or all nine.</summary>
public enum MaxOutUniqueMode
{
    None = 0,
    /// <summary>Weight → Transmission → Nitrous (first 3 categories).</summary>
    Legit3 = 1,
    All = 2,
}

public sealed class PerfCategoryInfo
{
    public required string Code { get; init; }
    public required string LabelNl { get; init; }
    public required string LabelEn { get; init; }
    public required int[] Indices { get; init; }
    public int UniqueIndex { get; init; } = -1;
}

public sealed class PerfPartInfo
{
    public required int Index { get; init; }
    public required string Label { get; init; }
    public required int Level { get; init; } // 1 Street, 2 Pro, 3 Extreme
    public string LevelName => Level switch { 1 => "Street", 2 => "Pro", 3 => "Extreme", _ => "" };
}

public sealed class CarTuneStats
{
    public int SlotIndex { get; init; }
    public string CarName { get; init; } = "";
    public bool IsOccupied { get; init; }
    public bool IsLocked { get; init; }
    public int PerfInstalled { get; init; }
    public int PerfTotal { get; init; }
    public int UniqueOn { get; init; }
    public int UniqueTotal { get; init; }
    public int UnlocksOn { get; init; }
    public int UnlocksTotal { get; init; }
    public TuneLevel EstimatedLevel { get; init; }
    public int[] CategoryInstalled { get; init; } = [];
    public bool[] UniqueFlags { get; init; } = [];
    public string SummaryNl { get; init; } = "";
}

public static class CarTuneService
{
    public static readonly PerfCategoryInfo[] Categories =
    [
        new() { Code = "WT", LabelNl = "Gewicht", LabelEn = "Weight reduction", UniqueIndex = 0, Indices = [0, 1, 2, 3, 4, 5] },
        new() { Code = "TR", LabelNl = "Transmissie", LabelEn = "Transmission", UniqueIndex = 1, Indices = [6, 7, 8, 9, 10, 11] },
        new() { Code = "NO", LabelNl = "Nitrous", LabelEn = "Nitrous", UniqueIndex = 2, Indices = [12, 13, 14] },
        new() { Code = "EN", LabelNl = "Motor", LabelEn = "Engine", UniqueIndex = 3, Indices = [15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25] },
        new() { Code = "TU", LabelNl = "Turbo", LabelEn = "Turbo", UniqueIndex = 4, Indices = [26, 27, 28] },
        new() { Code = "SU", LabelNl = "Ophanging", LabelEn = "Suspension", UniqueIndex = 5, Indices = [29, 30, 31, 32, 35, 36, 37] },
        // note: indices 33/34 exist in flag block but have no active item in game tables
        new() { Code = "BR", LabelNl = "Remmen", LabelEn = "Brakes", UniqueIndex = 6, Indices = [38, 39, 40, 41, 42, 43, 44] },
        new() { Code = "EC", LabelNl = "ECU / brandstof", LabelEn = "Engine control & fuel", UniqueIndex = 7, Indices = [45, 46, 47, 48, 49, 50, 51, 52] },
        new() { Code = "TI", LabelNl = "Banden", LabelEn = "Tires", UniqueIndex = 8, Indices = [53, 54, 55] },
    ];

    /// <summary>hackch PERF_PARTS labels (active package IDs only).</summary>
    public static readonly PerfPartInfo[] Parts =
    [
        new() { Index = 0, Level = 1, Label = "Remove Rear Seats" },
        new() { Index = 1, Level = 1, Label = "Remove Interior Panels" },
        new() { Index = 2, Level = 2, Label = "Lightweight Windows" },
        new() { Index = 3, Level = 2, Label = "Lightweight Seats" },
        new() { Index = 4, Level = 3, Label = "Lightweight Doors" },
        new() { Index = 5, Level = 3, Label = "Foam Filled Interior" },
        new() { Index = 6, Level = 1, Label = "Short Throw Shift Kit" },
        new() { Index = 7, Level = 1, Label = "Light Flywheel" },
        new() { Index = 8, Level = 1, Label = "Differential" },
        new() { Index = 9, Level = 2, Label = "Limited Slip Differential" },
        new() { Index = 10, Level = 2, Label = "High Performance Clutch" },
        new() { Index = 11, Level = 3, Label = "6 Speed Transmission" },
        new() { Index = 12, Level = 1, Label = "Dry Shot Of Nitrous" },
        new() { Index = 13, Level = 2, Label = "Wet Shot Of Nitrous" },
        new() { Index = 14, Level = 3, Label = "Direct Port Nitrous Oxide" },
        new() { Index = 15, Level = 1, Label = "Muffler" },
        new() { Index = 16, Level = 1, Label = "Cold Air Intake System" },
        new() { Index = 17, Level = 1, Label = "Replace Headers" },
        new() { Index = 18, Level = 1, Label = "Mild Camshaft And Cam Gears" },
        new() { Index = 19, Level = 2, Label = "Cat Back Exhaust System" },
        new() { Index = 20, Level = 2, Label = "High Flow Intake Manifold" },
        new() { Index = 21, Level = 2, Label = "Larger Diameter Downpipe" },
        new() { Index = 22, Level = 3, Label = "Racing Camshaft And Gears" },
        new() { Index = 23, Level = 3, Label = "Port And Polish Heads" },
        new() { Index = 24, Level = 3, Label = "Blueprint The Block" },
        new() { Index = 25, Level = 3, Label = "High Flow Headers" },
        new() { Index = 26, Level = 1, Label = "Stage 1 Turbo Kit" },
        new() { Index = 27, Level = 2, Label = "Stage 2 Turbo Kit" },
        new() { Index = 28, Level = 3, Label = "Stage 3 Turbo Kit" },
        new() { Index = 29, Level = 1, Label = "Sport Springs And Shocks" },
        new() { Index = 30, Level = 1, Label = "Strut Tower Bar" },
        new() { Index = 31, Level = 2, Label = "Performance Springs And Shocks" },
        new() { Index = 32, Level = 2, Label = "Front And Rear Sway Bars" },
        new() { Index = 35, Level = 3, Label = "Coil Over Suspension System" },
        new() { Index = 36, Level = 2, Label = "Upgrade Bushings" },
        new() { Index = 37, Level = 3, Label = "Camber Kit" },
        new() { Index = 38, Level = 1, Label = "Street Compound Brake Pads" },
        new() { Index = 39, Level = 1, Label = "Steel Braided Brake Lines" },
        new() { Index = 40, Level = 1, Label = "Cross Drilled Rotors" },
        new() { Index = 41, Level = 2, Label = "Large Diameter Rotors" },
        new() { Index = 42, Level = 2, Label = "Race Compound Brake Pads" },
        new() { Index = 43, Level = 3, Label = "Cross Drilled And Slotted Rotors" },
        new() { Index = 44, Level = 3, Label = "6 Piston Racing Calipers" },
        new() { Index = 45, Level = 1, Label = "Remove Speed Limiter" },
        new() { Index = 46, Level = 2, Label = "Performance Chip" },
        new() { Index = 47, Level = 2, Label = "High Flow Fuel Pump" },
        new() { Index = 48, Level = 1, Label = "Fuel Pressure Regulator" },
        new() { Index = 49, Level = 1, Label = "Fuel Rail" },
        new() { Index = 50, Level = 1, Label = "Fuel Filter" },
        new() { Index = 51, Level = 3, Label = "Engine Management Unit" },
        new() { Index = 52, Level = 3, Label = "Fuel Injectors" },
        new() { Index = 53, Level = 1, Label = "Street Performance Tires" },
        new() { Index = 54, Level = 2, Label = "Pro Performance Tires" },
        new() { Index = 55, Level = 3, Label = "Extreme Performance Tires" },
    ];

    /// <summary>Per index: welk package-level (1–3). Indices zonder entry blijven onaangeraakt bij level-set.</summary>
    private static readonly Dictionary<int, int> PartLevel = new()
    {
        [0] = 1, [1] = 1, [2] = 2, [3] = 2, [4] = 3, [5] = 3,
        [6] = 1, [7] = 1, [8] = 1, [9] = 2, [10] = 2, [11] = 3,
        [12] = 1, [13] = 2, [14] = 3,
        [15] = 1, [16] = 1, [17] = 1, [18] = 1, [19] = 2, [20] = 2, [21] = 2, [22] = 3, [23] = 3, [24] = 3, [25] = 3,
        [26] = 1, [27] = 2, [28] = 3,
        [29] = 1, [30] = 1, [31] = 2, [32] = 2, [35] = 3, [36] = 2, [37] = 3,
        [38] = 1, [39] = 1, [40] = 1, [41] = 2, [42] = 2, [43] = 3, [44] = 3,
        [45] = 1, [46] = 2, [47] = 2, [48] = 1, [49] = 1, [50] = 1, [51] = 3, [52] = 3,
        [53] = 1, [54] = 2, [55] = 3,
    };

    /// <summary>Conflicten: als A aan gaat, zet B uit (wederzijds voor package-trappen).</summary>
    private static readonly Dictionary<int, int[]> Conflicts = new()
    {
        [8] = [9, 11], [9] = [8, 11], [11] = [8, 9],
        [12] = [13, 14], [13] = [12, 14], [14] = [12, 13],
        [15] = [19], [19] = [15],
        [17] = [25], [25] = [17],
        [18] = [22], [22] = [18],
        [26] = [27, 28], [27] = [26, 28], [28] = [26, 27],
        [29] = [31, 35], [31] = [29, 35], [35] = [29, 31],
        [32] = [37], [37] = [32],
        [38] = [42], [42] = [38],
        [40] = [41, 43], [41] = [40, 43], [43] = [40, 41],
        [46] = [51], [51] = [46],
        [53] = [54, 55], [54] = [53, 55], [55] = [53, 54],
    };

    // Unlock-table ranges relative to SlotEditorBase (inclusive).
    private static readonly (int Start, int End)[] UnlockRanges =
    [
        (0x462, 0x475), (0x47e, 0x485), (0x59a, 0x5fd), (0x61e, 0x64d), (0x696, 0x6b1), (0x6b6, 0x6bd), (0x6c2, 0x6c9),
        (0x486, 0x495), (0x49a, 0x49d), (0x4a2, 0x4ad), (0x4b6, 0x4bd), (0x5fe, 0x61d), (0x7da, 0x7e1),
        (0x4ce, 0x53d),
        (0x586, 0x58d),
    ];

    public static int SlotEditorBase(int slot) => Layout.SlotEditorOffset(slot);

    public static CarTuneStats Read(byte[] working, int slot)
    {
        var info = GarageDatService.ReadSlot(working, slot);
        int baseOff = SlotEditorBase(slot);
        EnsureInRange(working, baseOff);

        int perfOn = 0;
        var catOn = new int[Categories.Length];
        for (int c = 0; c < Categories.Length; c++)
        {
            foreach (int idx in Categories[c].Indices)
            {
                if (idx < 0 || idx >= Layout.PerfCount) continue;
                if (working[baseOff + Layout.PerfOff + idx] != 0)
                {
                    perfOn++;
                    catOn[c]++;
                }
            }
        }

        var uniq = new bool[Layout.UniquePerfCount];
        int uniqOn = 0;
        for (int i = 0; i < Layout.UniquePerfCount; i++)
        {
            uint v = BitConverter.ToUInt32(working, baseOff + Layout.UniquePerfOff + i * 4);
            uniq[i] = v != 0;
            if (uniq[i]) uniqOn++;
        }

        int unlockTotal = 0, unlockOn = 0;
        foreach (var (start, end) in UnlockRanges)
        {
            for (int rel = start; rel <= end; rel++)
            {
                unlockTotal++;
                if (working[baseOff + rel] == 0xFF) unlockOn++;
            }
        }

        int tuneable = PartLevel.Count;
        var level = EstimateLevel(working, baseOff);
        string pct = tuneable == 0 ? "0" : ((perfOn * 100) / tuneable).ToString();

        return new CarTuneStats
        {
            SlotIndex = slot,
            CarName = info.DisplayName,
            IsOccupied = info.IsOccupied,
            IsLocked = info.IsLocked,
            PerfInstalled = perfOn,
            PerfTotal = PartLevel.Count,
            UniqueOn = uniqOn,
            UniqueTotal = Layout.UniquePerfCount,
            UnlocksOn = unlockOn,
            UnlocksTotal = unlockTotal,
            EstimatedLevel = level,
            CategoryInstalled = catOn,
            UniqueFlags = uniq,
            SummaryNl =
                $"{info.DisplayName}\n" +
                $"Level: {LevelLabel(level)}\n" +
                $"Parts: {perfOn}/{PartLevel.Count} installed (~{pct}%)\n" +
                $"Unique upgrades: {uniqOn}/{Layout.UniquePerfCount}\n" +
                $"Shop unlocks: {unlockOn}/{unlockTotal}",
        };
    }

    public static string LevelLabel(TuneLevel level) => level switch
    {
        TuneLevel.Stock => "Stock (default)",
        TuneLevel.Street => "Street (basic)",
        TuneLevel.Pro => "Pro (strong)",
        TuneLevel.Extreme => "Extreme (max)",
        _ => "?",
    };

    private static TuneLevel EstimateLevel(byte[] working, int baseOff)
    {
        bool any3 = false, any2 = false, any1 = false;
        foreach (var (idx, lvl) in PartLevel)
        {
            if (working[baseOff + Layout.PerfOff + idx] == 0) continue;
            if (lvl >= 3) any3 = true;
            else if (lvl == 2) any2 = true;
            else any1 = true;
        }
        if (any3) return TuneLevel.Extreme;
        if (any2) return TuneLevel.Pro;
        if (any1) return TuneLevel.Street;
        return TuneLevel.Stock;
    }

    public static void SetLevel(byte[] working, int slot, TuneLevel level)
    {
        int baseOff = SlotEditorBase(slot);
        EnsureInRange(working, baseOff);

        foreach (int idx in PartLevel.Keys)
            working[baseOff + Layout.PerfOff + idx] = 0;

        if (level == TuneLevel.Stock)
        {
            for (int i = 0; i < Layout.UniquePerfCount; i++)
                BitConverter.GetBytes(0u).CopyTo(working, baseOff + Layout.UniquePerfOff + i * 4);
            return;
        }

        int want = (int)level;
        // Hoogste eerst: zet alle onderdelen ≤ want aan die niet conflicteren
        foreach (int idx in PartLevel.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).Select(kv => kv.Key))
        {
            if (PartLevel[idx] > want) continue;
            if (Conflicts.TryGetValue(idx, out var bad) &&
                bad.Any(b => working[baseOff + Layout.PerfOff + b] != 0))
                continue;
            working[baseOff + Layout.PerfOff + idx] = 1;
        }

        bool uniqueOn = level >= TuneLevel.Pro;
        for (int i = 0; i < Layout.UniquePerfCount; i++)
            BitConverter.GetBytes(uniqueOn ? 1u : 0u).CopyTo(working, baseOff + Layout.UniquePerfOff + i * 4);

        MarkPurchasedBits(working, baseOff);
    }

    /// <summary>
    /// MAX OUT: own/unlock all parts + cosmetics shop. Optionally install Extreme packages.
    /// Unique flags follow <paramref name="uniqueMode"/> (none / legit 3-in-order / all).
    /// Does not change body/livery visuals or dyno.
    /// </summary>
    public static void FullyTune(
        byte[] working,
        int slot,
        MaxOutPerfMode perfMode = MaxOutPerfMode.InstallExtreme,
        MaxOutUniqueMode uniqueMode = MaxOutUniqueMode.Legit3)
    {
        int baseOff = SlotEditorBase(slot);
        EnsureInRange(working, baseOff);

        if (perfMode == MaxOutPerfMode.InstallExtreme)
        {
            // Best packages on the car (SetLevel also flips uniques — we override next).
            SetLevel(working, slot, TuneLevel.Extreme);
        }

        ApplyUniqueMode(working, slot, uniqueMode);
        OwnAllPackageBits(working, baseOff);
        SetShopUnlocks(working, slot, on: true);
        GarageDatService.UnlockSlot(working, slot);
    }

    /// <summary>
    /// Legit = first 3 unique categories in order (Weight, Transmission, Nitrous).
    /// </summary>
    public static void ApplyUniqueMode(byte[] working, int slot, MaxOutUniqueMode mode)
    {
        int baseOff = SlotEditorBase(slot);
        EnsureInRange(working, baseOff);
        for (int i = 0; i < Layout.UniquePerfCount; i++)
            BitConverter.GetBytes(0u).CopyTo(working, baseOff + Layout.UniquePerfOff + i * 4);

        int count = mode switch
        {
            MaxOutUniqueMode.None => 0,
            MaxOutUniqueMode.Legit3 => 3,
            MaxOutUniqueMode.All => Layout.UniquePerfCount,
            _ => 0,
        };
        for (int i = 0; i < count; i++)
            BitConverter.GetBytes(1u).CopyTo(working, baseOff + Layout.UniquePerfOff + i * 4);
    }

    public static bool IsPartOn(byte[] working, int slot, int partIndex)
    {
        int baseOff = SlotEditorBase(slot);
        EnsureInRange(working, baseOff);
        if (partIndex < 0 || partIndex >= Layout.PerfCount) return false;
        return working[baseOff + Layout.PerfOff + partIndex] != 0;
    }

    public static void SetPartOn(byte[] working, int slot, int partIndex, bool on, bool respectConflicts = true)
    {
        int baseOff = SlotEditorBase(slot);
        EnsureInRange(working, baseOff);
        if (partIndex < 0 || partIndex >= Layout.PerfCount) return;
        if (on && respectConflicts)
            EnablePart(working, baseOff, partIndex);
        else
            working[baseOff + Layout.PerfOff + partIndex] = on ? (byte)1 : (byte)0;
        if (on) MarkPurchasedBits(working, baseOff);
    }

    public static bool IsUniqueOn(byte[] working, int slot, int uniqueIndex)
    {
        int baseOff = SlotEditorBase(slot);
        EnsureInRange(working, baseOff);
        if (uniqueIndex < 0 || uniqueIndex >= Layout.UniquePerfCount) return false;
        return BitConverter.ToUInt32(working, baseOff + Layout.UniquePerfOff + uniqueIndex * 4) != 0;
    }

    public static void SetUniqueOn(byte[] working, int slot, int uniqueIndex, bool on)
    {
        int baseOff = SlotEditorBase(slot);
        EnsureInRange(working, baseOff);
        if (uniqueIndex < 0 || uniqueIndex >= Layout.UniquePerfCount) return;
        BitConverter.GetBytes(on ? 1u : 0u).CopyTo(working, baseOff + Layout.UniquePerfOff + uniqueIndex * 4);
    }

    public static IEnumerable<PerfPartInfo> PartsInCategory(int categoryIndex)
    {
        if ((uint)categoryIndex >= (uint)Categories.Length) yield break;
        var set = new HashSet<int>(Categories[categoryIndex].Indices);
        foreach (var p in Parts)
            if (set.Contains(p.Index)) yield return p;
    }

    public static void SetShopUnlocks(byte[] working, int slot, bool on)
    {
        int baseOff = SlotEditorBase(slot);
        EnsureInRange(working, baseOff);
        byte v = on ? (byte)0xFF : (byte)0x00;
        foreach (var (start, end) in UnlockRanges)
        {
            for (int rel = start; rel <= end; rel++)
                working[baseOff + rel] = v;
        }
    }

    public static void SetUniqueAll(byte[] working, int slot, bool on)
    {
        int baseOff = SlotEditorBase(slot);
        EnsureInRange(working, baseOff);
        for (int i = 0; i < Layout.UniquePerfCount; i++)
            BitConverter.GetBytes(on ? 1u : 0u).CopyTo(working, baseOff + Layout.UniquePerfOff + i * 4);
    }

    public static void SetCategoryLevel(byte[] working, int slot, int categoryIndex, TuneLevel level)
    {
        if ((uint)categoryIndex >= (uint)Categories.Length) throw new ArgumentOutOfRangeException(nameof(categoryIndex));
        int baseOff = SlotEditorBase(slot);
        EnsureInRange(working, baseOff);
        var cat = Categories[categoryIndex];

        foreach (int idx in cat.Indices)
        {
            if (PartLevel.ContainsKey(idx))
                working[baseOff + Layout.PerfOff + idx] = 0;
        }

        if (level != TuneLevel.Stock)
        {
            int want = (int)level;
            int best = -1;
            foreach (int idx in cat.Indices)
            {
                if (!PartLevel.TryGetValue(idx, out int lvl)) continue;
                if (lvl <= want && (best < 0 || lvl > PartLevel[best]))
                    best = idx;
            }
            if (best >= 0) EnablePart(working, baseOff, best);
        }

        if (cat.UniqueIndex >= 0 && cat.UniqueIndex < Layout.UniquePerfCount)
            BitConverter.GetBytes(level >= TuneLevel.Pro ? 1u : 0u)
                .CopyTo(working, baseOff + Layout.UniquePerfOff + cat.UniqueIndex * 4);

        MarkPurchasedBits(working, baseOff);
    }

    /// <summary>
    /// "Snelheid / power multiplier" proxy: Extreme packs + unique + unlocks.
    /// factor 1.0 = Street, 1.5 = Pro, 2.0+ = Extreme fully.
    /// </summary>
    public static void ApplyPowerMultiplier(byte[] working, int slot, double factor)
    {
        if (factor < 1.1) SetLevel(working, slot, TuneLevel.Street);
        else if (factor < 1.6) SetLevel(working, slot, TuneLevel.Pro);
        else FullyTune(working, slot);
    }

    private static void EnablePart(byte[] working, int baseOff, int index)
    {
        if (Conflicts.TryGetValue(index, out var bad))
        {
            foreach (int b in bad)
                working[baseOff + Layout.PerfOff + b] = 0;
        }
        working[baseOff + Layout.PerfOff + index] = 1;
    }

    private static void OwnAllPackageBits(byte[] working, int baseOff)
    {
        foreach (int idx in PartLevel.Keys)
        {
            int byteOff = baseOff + Layout.PerfPurchaseOff + (idx >> 3);
            if (byteOff >= working.Length) continue;
            working[byteOff] |= (byte)(1 << (idx & 7));
        }
    }

    private static void MarkPurchasedBits(byte[] working, int baseOff)
    {
        // Ownership bitset: mark every installed active part as purchased.
        for (int idx = 0; idx < Layout.PerfCount; idx++)
        {
            if (working[baseOff + Layout.PerfOff + idx] == 0) continue;
            int byteOff = baseOff + Layout.PerfPurchaseOff + (idx >> 3);
            if (byteOff >= working.Length) continue;
            working[byteOff] |= (byte)(1 << (idx & 7));
        }
    }

    private static void EnsureInRange(byte[] working, int baseOff)
    {
        int saveRec = baseOff - Layout.SlotEditorFromSaveRec;
        int need = saveRec + Layout.SlotStride;
        if (saveRec < 0 || working.Length < need)
            throw new InvalidOperationException("Slot does not exist in this buffer (open GARAGE.DAT for slots 6–10).");
    }
}
