using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NFSU2CustomCarSaves;

/// <summary>
/// Dyno preset from a real save car. Label = car · save · slot.
/// Optionally carries exact upgrades (perf/unique + bumper/spoiler visuals) for drag/drift stability.
/// </summary>
public sealed class TunePreset
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("car_name")] public string CarName { get; set; } = "";
    [JsonPropertyName("save_name")] public string SaveName { get; set; } = "";
    [JsonPropertyName("slot")] public int Slot { get; set; }
    [JsonPropertyName("car_hash")] public string CarHash { get; set; } = "";
    [JsonPropertyName("dyno_hex")] public string DynoHex { get; set; } = "";
    /// <summary>68 installed-part flags @ +0x310 (includes aero 56–67).</summary>
    [JsonPropertyName("perf_hex")] public string? PerfHex { get; set; }
    /// <summary>9×u32 unique flags @ +0x2E8.</summary>
    [JsonPropertyName("unique_hex")] public string? UniqueHex { get; set; }
    /// <summary>Visual hashes: F-bumper, R-bumper, widebody, skirts, spoiler.</summary>
    [JsonPropertyName("aero_visual_hex")] public string? AeroVisualHex { get; set; }
    [JsonPropertyName("aero_note")] public string? AeroNote { get; set; }
    [JsonPropertyName("note")] public string Note { get; set; } = "";
    [JsonPropertyName("verified")] public string Verified { get; set; } = "library";

    [JsonPropertyName("name")] public string? LegacyName { get; set; }
    [JsonPropertyName("kind")] public string? Kind { get; set; }
    [JsonPropertyName("category")] public string? Category { get; set; }
    [JsonPropertyName("parts_level")] public string? PartsLevel { get; set; }
    [JsonPropertyName("source")] public string? Source { get; set; }

    public bool IsDyno =>
        !string.IsNullOrWhiteSpace(DynoHex) &&
        (string.IsNullOrWhiteSpace(Kind) ||
         Kind.Equals("dyno", StringComparison.OrdinalIgnoreCase) ||
         Kind.Equals("full", StringComparison.OrdinalIgnoreCase));

    public bool HasUpgrades =>
        !string.IsNullOrWhiteSpace(PerfHex) && PerfHex.Length >= DynoPackService.PerfBytes * 2;

    /// <summary>True when this dyno only works properly with the stored part flags.</summary>
    public bool NeedsParts => DynoPackService.DynoNeedsPartsFromHex(DynoHex, PerfHex);

    /// <summary>Saved via Save dyno to library.</summary>
    public bool IsUserCreated =>
        Verified.Equals("user", StringComparison.OrdinalIgnoreCase) ||
        Verified.Equals("manual", StringComparison.OrdinalIgnoreCase);

    /// <summary>Shipped with the app (catalog / DYNO SAVES / CCS beta stock).</summary>
    public bool IsBundled =>
        Verified.Equals("dyno-saves-pack", StringComparison.OrdinalIgnoreCase) ||
        Verified.Equals("engine", StringComparison.OrdinalIgnoreCase) ||
        Verified.Equals("ccs-stock", StringComparison.OrdinalIgnoreCase) ||
        (Source?.Equals("built-in", StringComparison.OrdinalIgnoreCase) ?? false);

    /// <summary>User may remove this entry from dyno_library.json (not bundled stock).</summary>
    public bool CanDeleteFromLibrary => !IsBundled && (IsUserCreated ||
        Verified.Equals("imported-save", StringComparison.OrdinalIgnoreCase) ||
        Verified.Equals("folder", StringComparison.OrdinalIgnoreCase));

    public string OriginLabel => IsUserCreated ? "user created" : "";

    /// <summary>List label — +parts only when this dyno requires (and has) parts.</summary>
    public string DisplayLabel
    {
        get
        {
            string car = string.IsNullOrWhiteSpace(CarName) ? (LegacyName ?? "Unknown car") : CarName;
            string save = string.IsNullOrWhiteSpace(SaveName) ? (Source ?? "?") : SaveName;
            string up = NeedsParts && HasUpgrades ? "  ·  +parts" : "";
            string origin = IsUserCreated ? "  ·  user created" : "";
            return $"{car}  ·  from {save}{up}{origin}";
        }
    }

    public override string ToString() => DisplayLabel;
}

public sealed class TunePresetCatalog
{
    [JsonPropertyName("format")] public string Format { get; set; } = "nfsu2-dyno-library-v3";
    [JsonPropertyName("note")] public string Note { get; set; } = "";
    [JsonPropertyName("presets")] public List<TunePreset> Presets { get; set; } = new();
}

public static class TunePresetService
{
    private static List<TunePreset>? _cache;

    public static string PresetsDir => AppPaths.StockPresetsDir;

    public static string LibraryPath
    {
        get
        {
            AppPaths.EnsureMigrated();
            return AppPaths.DynoLibraryPath;
        }
    }

    public static string DynoSavesDir
    {
        get
        {
            var dirs = DynoSavesDirs();
            return dirs.Count > 0 ? dirs[0] : Path.Combine(AppContext.BaseDirectory, "DYNO SAVES");
        }
    }

    /// <summary>All DYNO SAVES folders (output + project source) so new dumps are found without a rebuild.</summary>
    public static IReadOnlyList<string> DynoSavesDirs()
    {
        var list = new List<string>();
        void Try(string path)
        {
            if (!Directory.Exists(path)) return;
            string full = Path.GetFullPath(path);
            if (list.Any(x => x.Equals(full, StringComparison.OrdinalIgnoreCase))) return;
            list.Add(full);
        }
        Try(Path.Combine(AppContext.BaseDirectory, "DYNO SAVES"));
        Try(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "DYNO SAVES")));
        Try(Path.Combine(Environment.CurrentDirectory, "DYNO SAVES"));
        return list;
    }

    public static IReadOnlyList<TunePreset> LoadDynoPresets(bool refresh = false)
    {
        if (_cache != null && !refresh) return _cache;
        EnsureDynoSavesImported();
        CompactLibraryDuplicates();

        // Same car + same dyno (+ same parts payload) = one entry. Prefer user-created.
        var byContent = new Dictionary<string, TunePreset>(StringComparer.OrdinalIgnoreCase);

        void Add(TunePreset p)
        {
            if (!p.IsDyno) return;
            string hex = NormHex(p.DynoHex);
            if (hex.Length != Layout.DynoTotalSize * 2) return;
            p.DynoHex = hex;
            NormalizePartsPolicy(p);
            // Incomplete: aggressive dyno without the parts it needs → hide
            if (p.NeedsParts && !p.HasUpgrades) return;
            if (string.IsNullOrWhiteSpace(p.CarName) && !string.IsNullOrWhiteSpace(p.LegacyName))
                p.CarName = p.LegacyName!;
            if (string.IsNullOrWhiteSpace(p.SaveName) && !string.IsNullOrWhiteSpace(p.Source))
                p.SaveName = p.Source!;

            string key = ContentKey(p);
            if (string.IsNullOrWhiteSpace(p.Id)) p.Id = ShortId(key);
            if (!byContent.TryGetValue(key, out var existing))
            {
                byContent[key] = p;
                return;
            }
            if (PreferPreset(p, existing))
                byContent[key] = p;
        }

        TryLoadFile(LibraryPath, Add);
        TryLoadFile(Path.Combine(PresetsDir, "catalog.json"), Add);
        TryLoadFile(Path.Combine(PresetsDir, "dyno_saves_catalog.json"), Add);

        if (Directory.Exists(PresetsDir))
        {
            foreach (string file in Directory.EnumerateFiles(PresetsDir, "*.json"))
            {
                string name = Path.GetFileName(file);
                if (name.Equals("catalog.json", StringComparison.OrdinalIgnoreCase)) continue;
                if (name.Equals("dyno_saves_catalog.json", StringComparison.OrdinalIgnoreCase)) continue;
                TryLoadFile(file, Add);
            }
        }

        _cache = byContent.Values
            .OrderBy(p => p.CarName, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(p => p.IsUserCreated)
            .ThenBy(p => p.SaveName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return _cache;
    }

    /// <summary>Import career dumps from every DYNO SAVES folder (deduped into library).</summary>
    public static int EnsureDynoSavesImported()
    {
        // Always rescan — cheap, and picks up new files like Spono without restarting.
        int total = 0;
        foreach (string dir in DynoSavesDirs())
            total += HarvestFromFolder(dir, verified: "dyno-saves-pack");
        return total;
    }

    /// <summary>After WipeLibraries — drop in-memory dyno list so UI reloads from disk.</summary>
    public static void InvalidateCache() => _cache = null;

    public static int HarvestFromFolder(string folder, string verified = "folder")
    {
        int total = 0;
        foreach (string file in Directory.EnumerateFiles(folder))
        {
            try
            {
                byte[] data = File.ReadAllBytes(file);
                if (data.Length < Layout.ProfileSize) continue;
                // Career payload may be bare 54966 or with header — accept exact profile size
                if (data.Length != Layout.ProfileSize && data.Length != Layout.ProfileSize + Layout.GarageSize)
                    continue;

                // Merge sibling GARAGE.DAT so My Cars (slots 6–10) dynos are harvested too.
                if (data.Length == Layout.ProfileSize && GarageDatService.LooksLikeCareer(data))
                {
                    string? garagePath = FindSiblingGarage(file);
                    if (garagePath != null)
                    {
                        byte[] garage = File.ReadAllBytes(garagePath);
                        if (GarageDatService.LooksLikeGarage(garage))
                            data = GarageDatService.Merge(data, garage, GarageState.Loaded);
                    }
                }

                string saveName = Path.GetFileName(file);
                total += HarvestFromSave(data, saveName, verified, skipDedupeWrite: false);
            }
            catch { /* skip bad file */ }
        }
        return total;
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

    public static int HarvestFromSave(byte[] working, string saveName, string verified = "imported-save", bool skipDedupeWrite = false)
    {
        if (string.IsNullOrWhiteSpace(saveName)) saveName = "save";
        var lib = LoadLibraryMutable();
        var existing = new HashSet<string>(lib.Presets.Select(ContentKey), StringComparer.OrdinalIgnoreCase);

        int added = 0;
        foreach (var s in GarageDatService.ReadSlots(working))
        {
            if (!s.IsOccupied) continue;
            try
            {
                var preset = CapturePreset(working, s.Index, saveName, verified);
                string key = ContentKey(preset);
                if (!existing.Add(key)) continue;
                lib.Presets.Insert(0, preset);
                added++;
            }
            catch { /* skip */ }
        }
        foreach (var c in MyCarsService.ReadAll(working))
        {
            if (!c.IsOccupied) continue;
            try
            {
                var preset = CapturePreset(working, c.Index, saveName + " / My Cars", verified, menuMyCars: true);
                string key = ContentKey(preset);
                if (!existing.Add(key)) continue;
                lib.Presets.Insert(0, preset);
                added++;
            }
            catch { /* skip */ }
        }

        if (added > 0 && !skipDedupeWrite)
        {
            CompactPresetsInPlace(lib.Presets);
            SaveLibrary(lib);
            _cache = null;
        }
        return added;
    }

    public static TunePreset HarvestFromSlot(byte[] working, int slot, string saveName, bool menuMyCars = false)
    {
        var preset = CapturePreset(working, slot, saveName, "user", menuMyCars);
        preset.Source = "user-created";
        var lib = LoadLibraryMutable();
        string key = ContentKey(preset);
        lib.Presets.RemoveAll(p => ContentKey(p) == key ||
            (!string.IsNullOrWhiteSpace(preset.Id) && p.Id.Equals(preset.Id, StringComparison.OrdinalIgnoreCase)));
        lib.Presets.Insert(0, preset);
        CompactPresetsInPlace(lib.Presets);
        SaveLibrary(lib);
        _cache = null;
        return preset;
    }

    /// <summary>Removes a user/imported preset from the personal library. Bundled presets cannot be deleted.</summary>
    public static bool DeleteFromLibrary(TunePreset preset)
    {
        if (preset == null) throw new ArgumentNullException(nameof(preset));
        if (!preset.CanDeleteFromLibrary)
            throw new InvalidOperationException("Included presets ship with the program and cannot be deleted.");

        var lib = LoadLibraryMutable();
        string key = ContentKey(preset);
        int removed = lib.Presets.RemoveAll(p =>
            ContentKey(p) == key ||
            (!string.IsNullOrWhiteSpace(preset.Id) && p.Id.Equals(preset.Id, StringComparison.OrdinalIgnoreCase)));
        if (removed == 0)
            throw new InvalidOperationException("That preset is not in your library (or was already removed).");

        SaveLibrary(lib);
        _cache = null;
        return true;
    }

    /// <summary>Rewrite library so identical dyno+parts for the same car appear once.</summary>
    private static void CompactLibraryDuplicates()
    {
        var lib = LoadLibraryMutable();
        int before = lib.Presets.Count;
        CompactPresetsInPlace(lib.Presets);
        if (lib.Presets.Count != before)
        {
            SaveLibrary(lib);
            _cache = null;
        }
    }

    private static void CompactPresetsInPlace(List<TunePreset> presets)
    {
        var keep = new Dictionary<string, TunePreset>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in presets)
        {
            if (!p.IsDyno) continue;
            string key = ContentKey(p);
            if (!keep.TryGetValue(key, out var existing) || PreferPreset(p, existing))
                keep[key] = p;
        }
        presets.Clear();
        presets.AddRange(keep.Values);
    }

    /// <summary>True if candidate should replace existing for the same content key.</summary>
    private static bool PreferPreset(TunePreset candidate, TunePreset existing)
    {
        if (candidate.IsUserCreated != existing.IsUserCreated) return candidate.IsUserCreated;
        if (candidate.HasUpgrades != existing.HasUpgrades) return candidate.HasUpgrades;
        if (candidate.IsBundled != existing.IsBundled) return candidate.IsBundled;
        return false;
    }

    public static string ApplyDyno(
        byte[] working,
        int slot,
        TunePreset preset,
        bool includeUpgrades = false,
        bool copyBodyVisuals = false,
        bool menuMyCars = false)
    {
        if (!preset.IsDyno)
            throw new InvalidOperationException("Only dyno presets can be applied here.");
        NormalizePartsPolicy(preset);
        if (preset.NeedsParts && !preset.HasUpgrades)
            throw new InvalidOperationException(
                "This dyno needs matching parts, but the preset has none — cannot apply.");

        // Mild dynos: never force parts. Parts-required: always apply stored parts with dyno.
        bool useParts = preset.NeedsParts && preset.HasUpgrades && includeUpgrades;
        return DynoPackService.Apply(
            working, slot,
            preset.DynoHex,
            useParts ? preset.PerfHex : null,
            useParts ? preset.UniqueHex : null,
            useParts ? preset.AeroVisualHex : null,
            preset.CarHash,
            useParts,
            copyBodyVisuals && useParts,
            menuMyCars);
    }

    private static TunePreset CapturePreset(byte[] working, int slot, string saveName, string verified, bool menuMyCars = false)
    {
        string carName;
        uint carHash;
        if (menuMyCars)
        {
            var mc = MyCarsService.Read(working, slot);
            if (!mc.IsOccupied) throw new InvalidOperationException("My Cars slot has no car.");
            carName = mc.DisplayName;
            carHash = mc.CarHash;
        }
        else
        {
            var info = GarageDatService.ReadSlot(working, slot);
            if (!info.IsOccupied) throw new InvalidOperationException("Slot has no car.");
            carName = info.DisplayName;
            carHash = info.CarHash;
        }

        DynoPackService.Capture(working, slot, out var dyno, out var perf, out var unique, out var aeroVis, menuMyCars);
        bool needs = DynoPackService.DynoNeedsParts(dyno, perf);
        string aeroNote = DynoPackService.AeroSummary(perf);

        var preset = new TunePreset
        {
            Id = ShortId(Convert.ToHexString(dyno) + Convert.ToHexString(perf)) + "-" + Sanitize(saveName).ToLowerInvariant(),
            CarName = carName,
            SaveName = saveName,
            Slot = slot + 1,
            CarHash = carHash.ToString("X8"),
            DynoHex = Convert.ToHexString(dyno),
            Verified = verified,
            Kind = "dyno",
        };

        if (needs)
        {
            preset.PerfHex = Convert.ToHexString(perf);
            preset.UniqueHex = Convert.ToHexString(unique);
            preset.AeroVisualHex = Convert.ToHexString(aeroVis);
            preset.AeroNote = aeroNote;
            preset.Note = $"From {saveName}. Needs parts. {aeroNote}.";
        }
        else
        {
            // Mild dyno — no parts payload, no +parts label
            preset.AeroNote = "mild dyno (no parts required)";
            preset.Note = $"From {saveName}. Mild setup — apply without changing parts.";
        }

        return preset;
    }

    /// <summary>Strip parts from mild presets; keep parts only when the dyno needs them.</summary>
    private static void NormalizePartsPolicy(TunePreset p)
    {
        bool needs = DynoPackService.DynoNeedsPartsFromHex(p.DynoHex, p.PerfHex);
        if (!needs)
        {
            p.PerfHex = null;
            p.UniqueHex = null;
            p.AeroVisualHex = null;
            if (string.IsNullOrWhiteSpace(p.AeroNote) || p.AeroNote.StartsWith("aero:", StringComparison.Ordinal))
                p.AeroNote = "mild dyno (no parts required)";
        }
    }

    private static void TryLoadFile(string path, Action<TunePreset> add)
    {
        if (!File.Exists(path)) return;
        try
        {
            string text = File.ReadAllText(path);
            if (text.Contains("\"presets\"", StringComparison.Ordinal))
            {
                var cat = JsonSerializer.Deserialize<TunePresetCatalog>(text);
                if (cat?.Presets != null)
                    foreach (var p in cat.Presets) add(p);
                return;
            }
            var dyno = JsonSerializer.Deserialize<DynoPackage>(text);
            if (dyno == null || string.IsNullOrWhiteSpace(dyno.DynoHex)) return;
            add(new TunePreset
            {
                Id = "file-" + Path.GetFileNameWithoutExtension(path),
                CarName = string.IsNullOrWhiteSpace(dyno.SourceCar) ? Path.GetFileNameWithoutExtension(path) : dyno.SourceCar,
                SaveName = string.IsNullOrWhiteSpace(dyno.SourceSave) ? "file" : dyno.SourceSave,
                Slot = dyno.SourceSlot,
                CarHash = dyno.SourceHash,
                DynoHex = dyno.DynoHex,
                Note = dyno.Note,
                Verified = "user-file",
                Kind = "dyno",
            });
        }
        catch { /* skip */ }
    }

    private static TunePresetCatalog LoadLibraryMutable()
    {
        try
        {
            if (File.Exists(LibraryPath))
            {
                var cat = JsonSerializer.Deserialize<TunePresetCatalog>(File.ReadAllText(LibraryPath));
                if (cat != null)
                {
                    cat.Presets ??= new List<TunePreset>();
                    return cat;
                }
            }
        }
        catch { /* fresh */ }
        return new TunePresetCatalog
        {
            Note = "Dyno + upgrades harvested from career saves (DYNO SAVES / imports).",
            Presets = new List<TunePreset>(),
        };
    }

    private static void SaveLibrary(TunePresetCatalog cat)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath)!);
        if (cat.Presets.Count > 500)
            cat.Presets = cat.Presets.Take(500).ToList();
        File.WriteAllText(LibraryPath, JsonSerializer.Serialize(cat, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>Same car + identical dyno (+ parts) = duplicate, regardless of save/slot label.</summary>
    private static string ContentKey(TunePreset p) =>
        (p.CarHash ?? "").Trim().ToUpperInvariant() + "|" +
        NormHex(p.DynoHex) + "|" +
        NormHex(p.PerfHex);

    private static string NormHex(string? hex) =>
        (hex ?? "").Replace(" ", "").Replace("-", "").ToUpperInvariant();

    private static string ShortId(string material)
    {
        byte[] hash = SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(material));
        return Convert.ToHexString(hash)[..10];
    }

    private static string Sanitize(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "save" : name.Trim();
    }
}
