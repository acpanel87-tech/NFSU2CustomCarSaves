using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NFSU2CustomCarSaves;

/// <summary>
/// Persistente car Save rack in de Data-map. Onbeperkte lijst (alleen gevulde entries) —
/// JSON blijft <c>{ "slots": [ ...packages... ] }</c> (oude 125-slot null-padding wordt bij Load genegeerd).
/// </summary>
public sealed class SaveRack
{
    /// <summary>Zachte bovengrens; 0 = geen limiet. Voorkomt per ongeluk gigantische racks.</summary>
    public const int SoftCap = 2000;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly List<CarSlotPackage> _slots = new();

    public int Count => _slots.Count;
    public int OccupiedCount => _slots.Count;
    public IReadOnlyList<CarSlotPackage> Entries => _slots;

    public static string StorePath
    {
        get
        {
            AppPaths.EnsureMigrated();
            return AppPaths.SaveRackPath;
        }
    }

    public static SaveRack Load()
    {
        AppPaths.EnsureMigrated();
        var rack = new SaveRack();
        try
        {
            if (!File.Exists(StorePath)) return rack;
            var dto = JsonSerializer.Deserialize<RackFile>(File.ReadAllText(StorePath));
            if (dto?.Slots == null) return rack;
            foreach (var pkg in dto.Slots)
            {
                if (pkg == null || string.IsNullOrWhiteSpace(pkg.TunedHex)) continue;
                rack._slots.Add(pkg);
            }
        }
        catch
        {
            /* corrupt → empty rack */
        }
        return rack;
    }

    public void Persist()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);
        // Alleen gevulde packages — geen null-padding
        var dto = new RackFile { Slots = new List<CarSlotPackage?>(_slots) };
        File.WriteAllText(StorePath, JsonSerializer.Serialize(dto, JsonOpts), new UTF8Encoding(false));
    }

    public CarSlotPackage? Get(int index)
    {
        if ((uint)index >= (uint)_slots.Count) throw new ArgumentOutOfRangeException(nameof(index));
        return _slots[index];
    }

    public void Set(int index, CarSlotPackage pkg)
    {
        if (pkg == null || string.IsNullOrWhiteSpace(pkg.TunedHex))
            throw new ArgumentException("Package has no TunedCar data.", nameof(pkg));
        if ((uint)index >= (uint)_slots.Count) throw new ArgumentOutOfRangeException(nameof(index));
        _slots[index] = pkg;
        Persist();
    }

    public void Add(CarSlotPackage pkg)
    {
        if (pkg == null || string.IsNullOrWhiteSpace(pkg.TunedHex))
            throw new ArgumentException("Package has no TunedCar data.", nameof(pkg));
        if (SoftCap > 0 && _slots.Count >= SoftCap)
            throw new InvalidOperationException($"Save rack soft cap ({SoftCap}) reached.");
        _slots.Add(pkg);
        Persist();
    }

    public void Clear(int index) => RemoveAt(index);

    public void RemoveAt(int index)
    {
        if ((uint)index >= (uint)_slots.Count) throw new ArgumentOutOfRangeException(nameof(index));
        _slots.RemoveAt(index);
        Persist();
    }

    public string Label(int index)
    {
        var p = Get(index);
        if (p == null || string.IsNullOrWhiteSpace(p.TunedHex))
            return $"R{index + 1:000}  rack  [ ]  (empty)";
        string name = p.CarHash != 0
            ? CarNames.Display(p.CarHash)
            : (string.IsNullOrWhiteSpace(p.CarName) ? p.XName : p.CarName);
        if (string.IsNullOrWhiteSpace(name)) name = "(unknown)";
        string area = p.AreaTag;
        string suffix = string.IsNullOrEmpty(area) ? "" : $"  ·  {area}";
        return $"R{index + 1:000}  rack  [*]  {name}{suffix}";
    }

    public string Tooltip(int index)
    {
        var p = Get(index);
        if (p == null || string.IsNullOrWhiteSpace(p.TunedHex))
            return $"Rack {index + 1}\n(empty)";

        string car = p.CarHash != 0
            ? CarNames.Display(p.CarHash)
            : (string.IsNullOrWhiteSpace(p.CarName) ? p.XName : p.CarName);
        if (string.IsNullOrWhiteSpace(car)) car = "(unknown)";

        var lines = new List<string> { car };
        string save = p.SaveDisplayName;
        if (!string.IsNullOrWhiteSpace(save))
            lines.Add($"From save: {save}");
        else if (!string.IsNullOrWhiteSpace(p.SourceProfile))
            lines.Add($"From: {p.SourceProfile}");

        string area = p.AreaDisplay;
        if (!string.IsNullOrEmpty(area))
            lines.Add($"Area: {area}");

        string? slot = p.SlotDisplay;
        if (!string.IsNullOrEmpty(slot))
            lines.Add($"Slot: {slot}");

        lines.Add($"Rack: {index + 1}");
        if (p.CarHash != 0)
        {
            lines.Add($"Internal name: {CarNames.ResolveXName(p.CarHash)}");
            lines.Add($"Car hash: 0x{p.CarHash:X8}");
        }
        return string.Join("\n", lines);
    }

    /// <summary>
    /// Harvest full cars from DYNO SAVES dirs + Templates/JANE into the rack.
    /// Only Adds missing stock (dedupe by car hash + tuned hex) — never wipes user cars.
    /// </summary>
    public static int ImportFromBundledSaves()
    {
        var rack = Load();
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in rack._slots)
            existing.Add(DedupeKey(p));

        int added = 0;
        bool dirty = false;
        foreach (string dir in TunePresetService.DynoSavesDirs())
            added += HarvestFolderInto(rack, existing, dir, ref dirty);

        foreach (string jane in JaneCareerCandidates())
        {
            if (!File.Exists(jane)) continue;
            try
            {
                added += HarvestFileInto(rack, existing, jane, Path.GetFileName(jane), ref dirty);
            }
            catch { /* skip */ }
            break; // first hit is enough
        }

        if (added > 0 || dirty) rack.Persist();
        return added;
    }

    private static IEnumerable<string> JaneCareerCandidates() => AppPaths.JaneCareerCandidates();

    private static int HarvestFolderInto(SaveRack rack, HashSet<string> existing, string folder, ref bool dirty)
    {
        if (!Directory.Exists(folder)) return 0;
        int total = 0;
        foreach (string file in Directory.EnumerateFiles(folder))
        {
            try
            {
                total += HarvestFileInto(rack, existing, file, Path.GetFileName(file), ref dirty);
            }
            catch { /* skip bad file */ }
        }
        return total;
    }

    private static int HarvestFileInto(SaveRack rack, HashSet<string> existing, string file, string saveName, ref bool dirty)
    {
        byte[] data = File.ReadAllBytes(file);
        if (data.Length < Layout.ProfileSize) return 0;
        if (data.Length != Layout.ProfileSize && data.Length != Layout.ProfileSize + Layout.GarageSize)
            return 0;

        // Same merge path as TunePresetService.HarvestFromFolder
        if (data.Length == Layout.ProfileSize)
        {
            if (!GarageDatService.LooksLikeCareer(data)) return 0;
            string? garagePath = FindSiblingGarage(file);
            if (garagePath != null)
            {
                byte[] garage = File.ReadAllBytes(garagePath);
                if (GarageDatService.LooksLikeGarage(garage))
                    data = GarageDatService.Merge(data, garage, GarageState.Loaded);
            }
        }
        else
        {
            var career = new byte[Layout.ProfileSize];
            var garage = new byte[Layout.GarageSize];
            Buffer.BlockCopy(data, 0, career, 0, Layout.ProfileSize);
            Buffer.BlockCopy(data, Layout.ProfileSize, garage, 0, Layout.GarageSize);
            if (!GarageDatService.LooksLikeCareer(career)) return 0;
            data = GarageDatService.LooksLikeGarage(garage)
                ? GarageDatService.Merge(career, garage, GarageState.Loaded)
                : career;
        }

        return HarvestWorkingInto(rack, existing, data, saveName, ref dirty);
    }

    private static int HarvestWorkingInto(SaveRack rack, HashSet<string> existing, byte[] working, string saveName, ref bool dirty)
    {
        int added = 0;
        string save = string.IsNullOrWhiteSpace(saveName) ? "save" : saveName.Trim();

        foreach (var s in GarageDatService.ReadSlots(working))
        {
            if (!s.IsOccupied) continue;
            try
            {
                GarageDatService.ReadRecord(working, s.Index, out var tuned, out var inv);
                string car = s.CarHash != 0 ? CarNames.Display(s.CarHash) : s.DisplayName;
                var pkg = new CarSlotPackage
                {
                    CarName = car,
                    XName = s.XName,
                    CarHash = s.CarHash,
                    SourceProfile = $"{save} · Career {s.Index + 1}",
                    SourceArea = "Career",
                    TunedHex = Convert.ToHexString(tuned),
                    InventoryHex = Convert.ToHexString(inv),
                };
                if (TryAddOrEnrich(rack, existing, pkg, ref added, ref dirty))
                {
                    if (SoftCap > 0 && rack._slots.Count >= SoftCap) return added;
                }
            }
            catch { /* skip */ }
        }

        foreach (var c in MyCarsService.ReadAll(working))
        {
            if (!c.IsOccupied) continue;
            try
            {
                var pkg = MyCarsService.ExportPackage(working, c.Index, save);
                if (TryAddOrEnrich(rack, existing, pkg, ref added, ref dirty))
                {
                    if (SoftCap > 0 && rack._slots.Count >= SoftCap) return added;
                }
            }
            catch { /* skip */ }
        }

        return added;
    }

    /// <summary>
    /// Add a new rack entry, or refresh SourceProfile/SourceArea on an existing dedupe match
    /// so Career vs Customize tags show up after older imports.
    /// Returns true when a new slot was added.
    /// </summary>
    private static bool TryAddOrEnrich(SaveRack rack, HashSet<string> existing, CarSlotPackage pkg, ref int added, ref bool dirty)
    {
        string key = DedupeKey(pkg);
        if (!existing.Add(key))
        {
            for (int i = 0; i < rack._slots.Count; i++)
            {
                if (!string.Equals(DedupeKey(rack._slots[i]), key, StringComparison.OrdinalIgnoreCase))
                    continue;
                var cur = rack._slots[i];
                bool change = false;
                if (!string.IsNullOrWhiteSpace(pkg.SourceProfile) &&
                    !string.Equals(cur.SourceProfile, pkg.SourceProfile, StringComparison.Ordinal))
                {
                    cur.SourceProfile = pkg.SourceProfile;
                    change = true;
                }
                if (!string.IsNullOrWhiteSpace(pkg.SourceArea) &&
                    !string.Equals(cur.SourceArea, pkg.SourceArea, StringComparison.OrdinalIgnoreCase))
                {
                    cur.SourceArea = pkg.SourceArea;
                    change = true;
                }
                if (string.IsNullOrWhiteSpace(cur.CarName) && !string.IsNullOrWhiteSpace(pkg.CarName))
                {
                    cur.CarName = pkg.CarName;
                    change = true;
                }
                if (change) dirty = true;
                break;
            }
            return false;
        }

        rack._slots.Add(pkg);
        added++;
        dirty = true;
        return true;
    }

    private static string? FindSiblingGarage(string careerFile)
    {
        string? dir = Path.GetDirectoryName(careerFile);
        if (string.IsNullOrEmpty(dir)) return null;
        foreach (string name in new[] { "GARAGE.DAT", "garage.dat", "Garage.dat" })
        {
            string p = Path.Combine(dir, name);
            if (File.Exists(p)) return p;
        }
        return null;
    }

    private static string DedupeKey(CarSlotPackage p) =>
        p.CarHash.ToString("X8") + "|" + NormHex(p.TunedHex);

    private static string NormHex(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return "";
        return hex.Replace(" ", "", StringComparison.Ordinal).Trim().ToUpperInvariant();
    }

    private sealed class RackFile
    {
        [JsonPropertyName("slots")] public List<CarSlotPackage?> Slots { get; set; } = new();
    }
}

/// <summary>In-app + Windows clipboard voor carslot JSON.</summary>
public static class SlotClipboard
{
    private const string Prefix = "NFSU2_CARSLOT_CLIP\n";
    public static CarSlotPackage? Memory { get; private set; }

    public static void ClearMemory() => Memory = null;

    public static void Copy(CarSlotPackage pkg)
    {
        Memory = pkg;
        try
        {
            string json = JsonSerializer.Serialize(pkg, new JsonSerializerOptions { WriteIndented = false });
            Clipboard.SetText(Prefix + json);
        }
        catch
        {
            /* clipboard kan geblokkeerd zijn */
        }
    }

    public static CarSlotPackage? Paste()
    {
        try
        {
            if (Clipboard.ContainsText())
            {
                string t = Clipboard.GetText();
                if (t.StartsWith(Prefix, StringComparison.Ordinal))
                {
                    var pkg = JsonSerializer.Deserialize<CarSlotPackage>(t[Prefix.Length..]);
                    if (pkg != null && !string.IsNullOrWhiteSpace(pkg.TunedHex))
                    {
                        Memory = pkg;
                        return pkg;
                    }
                }
                // raw carslot json
                if (t.Contains("tuned_hex", StringComparison.OrdinalIgnoreCase))
                {
                    var pkg = JsonSerializer.Deserialize<CarSlotPackage>(t);
                    if (pkg != null && !string.IsNullOrWhiteSpace(pkg.TunedHex))
                    {
                        Memory = pkg;
                        return pkg;
                    }
                }
            }
        }
        catch { /* fall through */ }
        return Memory;
    }

    public static bool HasData => Memory != null || TryPeekClipboard();

    private static bool TryPeekClipboard()
    {
        try
        {
            if (!Clipboard.ContainsText()) return false;
            string t = Clipboard.GetText();
            return t.StartsWith(Prefix, StringComparison.Ordinal) || t.Contains("tuned_hex", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }
}
