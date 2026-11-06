using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NFSU2CustomCarSaves;

/// <summary>
/// Full graphics / livery pack from TunedCar visual hashes @ S+0x18:
/// body + accessory paints, vinyl layers/colours, decals, window tint, neon/frame.
/// Body-kit geometry (bumpers, hood, etc.) is intentionally not included.
/// </summary>
public sealed class LiveryPreset
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("car_name")] public string CarName { get; set; } = "";
    [JsonPropertyName("car_hash")] public string CarHash { get; set; } = "";
    [JsonPropertyName("save_name")] public string SaveName { get; set; } = "";
    [JsonPropertyName("slot")] public int Slot { get; set; }
    /// <summary>Packed visual hashes for all graphics slots (see LiveryService.GraphicsSlots).</summary>
    [JsonPropertyName("graphics_hex")] public string GraphicsHex { get; set; } = "";
    /// <summary>Legacy: 4 × u32 vinyl layers (64–67). Filled for display / old libraries.</summary>
    [JsonPropertyName("layers_hex")] public string LayersHex { get; set; } = "";
    /// <summary>Legacy: 16 × u32 vinyl colours (77–92).</summary>
    [JsonPropertyName("colors_hex")] public string ColorsHex { get; set; } = "";
    /// <summary>Legacy: body paint (63).</summary>
    [JsonPropertyName("paint_hex")] public string? PaintHex { get; set; }
    [JsonPropertyName("note")] public string Note { get; set; } = "";
    [JsonPropertyName("verified")] public string Verified { get; set; } = "library";
    [JsonPropertyName("source")] public string? Source { get; set; }

    public bool IsUserCreated =>
        Verified.Equals("user", StringComparison.OrdinalIgnoreCase) ||
        Verified.Equals("manual", StringComparison.OrdinalIgnoreCase);

    public bool IsBundled =>
        Verified.Equals("dyno-saves-pack", StringComparison.OrdinalIgnoreCase) ||
        Verified.Equals("ccs-stock", StringComparison.OrdinalIgnoreCase) ||
        (Source?.Equals("built-in", StringComparison.OrdinalIgnoreCase) ?? false);

    public bool CanDelete => !IsBundled && (IsUserCreated ||
        Verified.Equals("imported-save", StringComparison.OrdinalIgnoreCase) ||
        Verified.Equals("folder", StringComparison.OrdinalIgnoreCase));

    public string OriginLabel =>
        IsBundled ? "bundled"
        : IsUserCreated ? "user created"
        : Verified.Equals("imported-save", StringComparison.OrdinalIgnoreCase) ? "imported save"
        : Verified.Equals("folder", StringComparison.OrdinalIgnoreCase) ? "from folder"
        : "library";

    /// <summary>Rich hover text for the Liveries list.</summary>
    public string HoverTip
    {
        get
        {
            string car = string.IsNullOrWhiteSpace(CarName) ? "Unknown car" : CarName;
            string save = string.IsNullOrWhiteSpace(SaveName) ? "?" : SaveName;
            var lines = new List<string>
            {
                car,
                $"From save: {save}",
            };
            if (Slot > 0) lines.Add($"Slot: {Slot}");
            lines.Add($"Origin: {OriginLabel}");
            if (!string.IsNullOrWhiteSpace(Note)) lines.Add(Note);
            lines.Add("Double-click or Apply → selected car");
            return string.Join("\n", lines);
        }
    }

    public int ActiveLayerCount
    {
        get
        {
            try
            {
                if (string.IsNullOrWhiteSpace(LayersHex)) return 0;
                byte[] layers = Convert.FromHexString(Norm(LayersHex));
                return CountNonZero(layers);
            }
            catch { return 0; }
        }
    }

    /// <summary>List label: car · from Save · bits (+ user created only when you saved it).</summary>
    public string DisplayLabel
    {
        get
        {
            string car = string.IsNullOrWhiteSpace(CarName) ? "Unknown" : CarName;
            string save = string.IsNullOrWhiteSpace(SaveName) ? "?" : SaveName;
            string bits = string.IsNullOrWhiteSpace(Note) ? $"{ActiveLayerCount}/4 vinyl" : Note;
            string origin = IsUserCreated ? "  ·  user created" : "";
            return $"{car}  ·  from {save}  ·  {bits}{origin}";
        }
    }

    public override string ToString() => DisplayLabel;

    private static string Norm(string? hex) =>
        (hex ?? "").Replace(" ", "").Replace("-", "").ToUpperInvariant();

    private static int CountNonZero(byte[] data)
    {
        int n = 0;
        for (int i = 0; i + 4 <= data.Length; i += 4)
            if (BitConverter.ToUInt32(data, i) != 0) n++;
        return n;
    }
}

public sealed class LiveryCatalog
{
    [JsonPropertyName("format")] public string Format { get; set; } = "nfsu2-livery-library-v2";
    [JsonPropertyName("note")] public string Note { get; set; } = "";
    [JsonPropertyName("presets")] public List<LiveryPreset> Presets { get; set; } = new();
}

public enum LiveryApplyMode
{
    /// <summary>Overwrite every graphics slot in the pack.</summary>
    Replace,
    /// <summary>Fill only empty target slots from non-empty source slots.</summary>
    MergeEmpty,
    /// <summary>Overwrite target slots that the source has set; leave empty source slots alone.</summary>
    MergeOverlay,
}

/// <summary>Capture / apply / library for full car graphics (paint, vinyl, decals, window, neon).</summary>
public static class LiveryService
{
    public const int VisualBase = 0x18; // relative SlotEditorOffset
    public const int VisualSlotCount = 170;

    /// <summary>
    /// Graphics-only visual slots (hackch). Not body kit geometry.
    /// 63 paint · 64–67 vinyl layers · 68–76 accessory paints · 77–92 vinyl colours
    /// (4 colours × 4 layers) · 97–152 decals · 153–162 window tint / neon / frame.
    /// </summary>
    public static readonly int[] GraphicsSlots = BuildGraphicsSlots();

    public static int GraphicsBytes => GraphicsSlots.Length * 4;

    private static List<LiveryPreset>? _cache;
    private static Dictionary<int, int>? _slotIndex;

    public static string LibraryPath
    {
        get
        {
            AppPaths.EnsureMigrated();
            return AppPaths.LiveryLibraryPath;
        }
    }

    private static int[] BuildGraphicsSlots()
    {
        var list = new List<int>(100);
        list.Add(63);                         // body paint
        for (int i = 64; i <= 67; i++) list.Add(i);   // vinyl layers 0–3
        for (int i = 68; i <= 76; i++) list.Add(i);   // accessory paints
        for (int i = 77; i <= 92; i++) list.Add(i);   // vinyl colours (4 per layer)
        for (int i = 97; i <= 152; i++) list.Add(i);  // decals
        for (int i = 153; i <= 162; i++) list.Add(i); // window tint + neon/frame block
        return list.ToArray();
    }

    private static int IndexOfSlot(int visualSlot)
    {
        _slotIndex ??= GraphicsSlots
            .Select((s, i) => (s, i))
            .ToDictionary(x => x.s, x => x.i);
        return _slotIndex.TryGetValue(visualSlot, out int idx) ? idx : -1;
    }

    private static bool _stockImported;
    private const int LibrarySoftCap = 2000;

    public static IReadOnlyList<LiveryPreset> LoadAll(bool refresh = false)
    {
        if (_cache != null && !refresh) return _cache;
        // Pull liveries from DYNO SAVES (same folder as dyno packs) every refresh so new dumps show up.
        ImportFromDynoSavesFolder();
        CompactLibraryDuplicates();

        var byKey = new Dictionary<string, LiveryPreset>(StringComparer.OrdinalIgnoreCase);
        void Add(LiveryPreset p)
        {
            NormalizePreset(p);
            if (!IsValid(p)) return;
            string key = ContentKey(p);
            if (!byKey.TryGetValue(key, out var existing) || Prefer(p, existing))
                byKey[key] = p;
        }

        var lib = LoadLibraryMutable();
        foreach (var p in lib.Presets) Add(p);

        // Shipped stock (Stock/Presets/livery_library.json) — always available in beta builds.
        string stock = Path.Combine(AppPaths.StockPresetsDir, "livery_library.json");
        if (File.Exists(stock))
        {
            try
            {
                var cat = System.Text.Json.JsonSerializer.Deserialize<LiveryCatalog>(File.ReadAllText(stock));
                if (cat?.Presets != null)
                    foreach (var p in cat.Presets) Add(p);
            }
            catch { /* ignore bad stock file */ }
        }

        _cache = byKey.Values
            .OrderByDescending(p => p.IsUserCreated)
            .ThenBy(p => p.CarName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.SaveName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return _cache;
    }

    /// <summary>
    /// One-shot at startup (after migrate) so the Liveries tab has bundled stock
    /// before the user opens a save.
    /// </summary>
    public static int EnsureStockLiveriesImported()
    {
        if (_stockImported) return 0;
        _stockImported = true;
        return ImportFromDynoSavesFolder();
    }

    /// <summary>After WipeLibraries — allow EnsureStockLiveriesImported to run again.</summary>
    public static void ResetStockImportFlag()
    {
        _stockImported = false;
        _cache = null;
    }

    /// <summary>Harvest graphics/liveries from every DYNO SAVES folder + optional Templates/JANE.</summary>
    public static int ImportFromDynoSavesFolder()
    {
        int total = 0;
        foreach (string dir in TunePresetService.DynoSavesDirs())
            total += HarvestFromFolder(dir, verified: "dyno-saves-pack");

        // Optional Jane free-play template (same career dump as DYNO SAVES/Jane, may differ by build).
        foreach (string jane in JaneTemplateCandidates())
        {
            if (!File.Exists(jane)) continue;
            try
            {
                total += HarvestCareerFile(jane, verified: "dyno-saves-pack");
            }
            catch { /* skip */ }
            break;
        }
        return total;
    }

    private static IEnumerable<string> JaneTemplateCandidates() => AppPaths.JaneCareerCandidates();

    public static int HarvestFromFolder(string folder, string verified = "folder")
    {
        if (!Directory.Exists(folder)) return 0;
        int total = 0;
        foreach (string file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
        {
            try
            {
                // Skip obvious non-saves
                string ext = Path.GetExtension(file);
                if (ext.Equals(".json", StringComparison.OrdinalIgnoreCase) ||
                    ext.Equals(".txt", StringComparison.OrdinalIgnoreCase) ||
                    ext.Equals(".md", StringComparison.OrdinalIgnoreCase) ||
                    ext.Equals(".ini", StringComparison.OrdinalIgnoreCase))
                    continue;

                total += HarvestCareerFile(file, verified);
            }
            catch { /* skip bad file */ }
        }
        return total;
    }

    /// <summary>Read one career dump; merge sibling GARAGE.DAT so My Cars / slots 6–10 count.</summary>
    private static int HarvestCareerFile(string file, string verified)
    {
        byte[] data = File.ReadAllBytes(file);
        if (data.Length < Layout.ProfileSize) return 0;
        if (data.Length != Layout.ProfileSize && data.Length != Layout.ProfileSize + Layout.GarageSize)
            return 0;

        // Merge sibling GARAGE.DAT so My Cars (slots 6–10) liveries are harvested too.
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
        return HarvestFromSave(data, saveName, verified);
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

    public static LiveryPreset CaptureFromSlot(byte[] working, int slot, string saveName, string verified = "user", bool menuMyCars = false)
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

        byte[] graphics = ReadGraphics(working, slot, menuMyCars);
        var stats = SummarizeGraphics(graphics);
        if (stats.TotalSet == 0)
            throw new InvalidOperationException("This car has no paint / vinyl / decals / window / neon to save.");

        var preset = new LiveryPreset
        {
            Id = ShortId(Convert.ToHexString(graphics)),
            Name = string.IsNullOrWhiteSpace(saveName) ? carName : saveName.Trim(),
            CarName = carName,
            CarHash = carHash.ToString("X8"),
            SaveName = string.IsNullOrWhiteSpace(saveName) ? "save" : saveName.Trim(),
            Slot = slot + 1,
            GraphicsHex = Convert.ToHexString(graphics),
            Verified = verified,
            Source = verified.Equals("user", StringComparison.OrdinalIgnoreCase) ? "user-created" : null,
            Note = stats.ShortLabel,
        };
        FillLegacyFields(preset, graphics);
        return preset;
    }

    public static LiveryPreset SaveToLibrary(byte[] working, int slot, string saveName, bool menuMyCars = false)
    {
        var preset = CaptureFromSlot(working, slot, saveName, "user", menuMyCars);
        var lib = LoadLibraryMutable();
        string key = ContentKey(preset);
        lib.Presets.RemoveAll(p => ContentKey(p) == key);
        lib.Presets.Insert(0, preset);
        CompactPresetsInPlace(lib.Presets);
        SaveLibrary(lib);
        _cache = null;
        return preset;
    }

    public static bool DeleteFromLibrary(LiveryPreset preset)
    {
        if (!preset.CanDelete)
            throw new InvalidOperationException("That livery cannot be deleted.");
        var lib = LoadLibraryMutable();
        string key = ContentKey(preset);
        int removed = lib.Presets.RemoveAll(p =>
            ContentKey(p) == key ||
            (!string.IsNullOrWhiteSpace(preset.Id) && p.Id.Equals(preset.Id, StringComparison.OrdinalIgnoreCase)));
        if (removed == 0)
            throw new InvalidOperationException("Livery is not in your library.");
        SaveLibrary(lib);
        _cache = null;
        return true;
    }

    public static int HarvestFromSave(byte[] working, string saveName, string verified = "imported-save")
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
                var preset = CaptureFromSlot(working, s.Index, saveName, verified);
                string key = ContentKey(preset);
                if (!existing.Add(key)) continue;
                lib.Presets.Insert(0, preset);
                added++;
            }
            catch { /* empty graphics or bad slot */ }
        }
        foreach (var c in MyCarsService.ReadAll(working))
        {
            if (!c.IsOccupied) continue;
            try
            {
                var preset = CaptureFromSlot(working, c.Index, saveName + " / My Cars", verified, menuMyCars: true);
                string key = ContentKey(preset);
                if (!existing.Add(key)) continue;
                lib.Presets.Insert(0, preset);
                added++;
            }
            catch { /* empty graphics or bad slot */ }
        }
        if (added > 0)
        {
            CompactPresetsInPlace(lib.Presets);
            SaveLibrary(lib);
            _cache = null;
        }
        return added;
    }

    public static string Apply(byte[] working, int slot, LiveryPreset preset, LiveryApplyMode mode, bool menuMyCars = false)
    {
        uint carHash;
        bool occupied;
        if (menuMyCars)
        {
            var mc = MyCarsService.Read(working, slot);
            occupied = mc.IsOccupied;
            carHash = mc.CarHash;
        }
        else
        {
            var info = GarageDatService.ReadSlot(working, slot);
            occupied = info.IsOccupied;
            carHash = info.CarHash;
        }
        if (!occupied)
            throw new InvalidOperationException("Target slot has no car.");

        NormalizePreset(preset);
        byte[] src = ResolveGraphics(preset);
        byte[] dst = ReadGraphics(working, slot, menuMyCars);

        // Vinyl layers are atomic: each layer hash (64–67) moves with its 4 colours (77–92).
        var vinylTouched = new bool[GraphicsSlots.Length];
        int layersCopied = ApplyVinylLayersAtomic(src, dst, mode, vinylTouched);

        int copied = layersCopied;
        int kept = 0;
        for (int i = 0; i < GraphicsSlots.Length; i++)
        {
            if (vinylTouched[i]) continue; // already handled with layer group
            uint s = BitConverter.ToUInt32(src, i * 4);
            uint d = BitConverter.ToUInt32(dst, i * 4);
            bool take = mode switch
            {
                LiveryApplyMode.Replace => true,
                LiveryApplyMode.MergeEmpty => s != 0 && d == 0,
                LiveryApplyMode.MergeOverlay => s != 0,
                _ => false,
            };
            if (take)
            {
                Buffer.BlockCopy(src, i * 4, dst, i * 4, 4);
                if (s != 0) copied++;
            }
            else if (d != 0)
            {
                kept++;
            }
        }

        WriteGraphics(working, slot, dst, menuMyCars);

        var parts = new List<string>
        {
            mode.ToString(),
            $"{layersCopied}/4 vinyl layers",
            $"{copied} graphics slot(s)",
        };
        if (kept > 0) parts.Add($"kept {kept}");

        bool sameCar = !string.IsNullOrWhiteSpace(preset.CarHash) &&
                       uint.TryParse(preset.CarHash, System.Globalization.NumberStyles.HexNumber, null, out uint ph) &&
                       ph == carHash;
        if (!sameCar)
            parts.Add("cross-car (some paints/decals may be car-specific)");

        return string.Join(" · ", parts);
    }

    /// <summary>
    /// Copy vinyl layer groups intact (layer hash + 4 colour hashes), so no layer loses its colours.
    /// </summary>
    private static int ApplyVinylLayersAtomic(byte[] src, byte[] dst, LiveryApplyMode mode, bool[] touched)
    {
        int layersCopied = 0;
        for (int layer = 0; layer < 4; layer++)
        {
            int layerIdx = IndexOfSlot(64 + layer);
            int color0 = IndexOfSlot(77 + layer * 4);
            if (layerIdx < 0 || color0 < 0) continue;

            uint srcHash = BitConverter.ToUInt32(src, layerIdx * 4);
            uint dstHash = BitConverter.ToUInt32(dst, layerIdx * 4);
            bool srcHas = srcHash != 0;
            bool dstHas = dstHash != 0;
            for (int c = 0; c < 4; c++)
            {
                if (BitConverter.ToUInt32(src, (color0 + c) * 4) != 0) srcHas = true;
                if (BitConverter.ToUInt32(dst, (color0 + c) * 4) != 0) dstHas = true;
            }

            bool take = mode switch
            {
                LiveryApplyMode.Replace => true,
                LiveryApplyMode.MergeEmpty => srcHas && !dstHas,
                LiveryApplyMode.MergeOverlay => srcHas,
                _ => false,
            };

            touched[layerIdx] = true;
            for (int c = 0; c < 4; c++) touched[color0 + c] = true;

            if (!take)
                continue;

            Buffer.BlockCopy(src, layerIdx * 4, dst, layerIdx * 4, 4);
            for (int c = 0; c < 4; c++)
                Buffer.BlockCopy(src, (color0 + c) * 4, dst, (color0 + c) * 4, 4);
            if (srcHas) layersCopied++;
        }
        return layersCopied;
    }

    public static string SummarizeSlot(byte[] working, int slot, bool menuMyCars = false)
    {
        try
        {
            var stats = SummarizeGraphics(ReadGraphics(working, slot, menuMyCars));
            return stats.TotalSet == 0 ? "No graphics on this car" : stats.DetailLabel;
        }
        catch { return "—"; }
    }

    public static LiveryPreset? ClipboardPkg { get; private set; }

    public static void ClearClipboard()
    {
        ClipboardPkg = null;
        try
        {
            AppPaths.EnsureMigrated();
            if (File.Exists(AppPaths.LiveryClipboardPath))
                File.Delete(AppPaths.LiveryClipboardPath);
        }
        catch { /* ok */ }
    }

    /// <summary>Copy livery from Career or Customize Car Select into the livery clipboard.</summary>
    public static LiveryPreset ExportToClipboard(byte[] working, int slot, string? saveName = null, bool menuMyCars = false)
    {
        var preset = CaptureFromSlot(working, slot, saveName ?? "clipboard", "clipboard", menuMyCars);
        CopyToClipboard(preset);
        return preset;
    }

    public static void CopyToClipboard(LiveryPreset preset)
    {
        ClipboardPkg = preset;
        try
        {
            string json = JsonSerializer.Serialize(preset, new JsonSerializerOptions { WriteIndented = true });
            Clipboard.SetText("NFSU2_LIVERY_CLIP\n" + json);
        }
        catch { /* ok */ }
        try
        {
            AppPaths.EnsureMigrated();
            File.WriteAllText(AppPaths.LiveryClipboardPath,
                JsonSerializer.Serialize(preset, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* ok */ }
    }

    public static LiveryPreset? PasteFromClipboard()
    {
        try
        {
            if (Clipboard.ContainsText())
            {
                string t = Clipboard.GetText();
                if (t.StartsWith("NFSU2_LIVERY_CLIP\n", StringComparison.Ordinal))
                    t = t["NFSU2_LIVERY_CLIP\n".Length..];
                if (t.Contains("graphics_hex", StringComparison.OrdinalIgnoreCase) ||
                    t.Contains("layers_hex", StringComparison.OrdinalIgnoreCase))
                {
                    var pkg = JsonSerializer.Deserialize<LiveryPreset>(t);
                    if (pkg != null && (!string.IsNullOrWhiteSpace(pkg.GraphicsHex) || !string.IsNullOrWhiteSpace(pkg.LayersHex)))
                    {
                        ClipboardPkg = pkg;
                        return pkg;
                    }
                }
            }
        }
        catch { /* fall through */ }

        if (ClipboardPkg != null) return ClipboardPkg;

        try
        {
            AppPaths.EnsureMigrated();
            string path = AppPaths.LiveryClipboardPath;
            if (File.Exists(path))
            {
                var pkg = JsonSerializer.Deserialize<LiveryPreset>(File.ReadAllText(path));
                if (pkg != null && (!string.IsNullOrWhiteSpace(pkg.GraphicsHex) || !string.IsNullOrWhiteSpace(pkg.LayersHex)))
                {
                    ClipboardPkg = pkg;
                    return pkg;
                }
            }
        }
        catch { /* ignore */ }
        return null;
    }

    public static string SummarizeClipboard(LiveryPreset? pkg)
    {
        if (pkg == null) return "Livery clipboard: empty";
        string car = string.IsNullOrWhiteSpace(pkg.CarName) ? "?" : pkg.CarName;
        string bits = string.IsNullOrWhiteSpace(pkg.Note) ? "graphics" : pkg.Note;
        return $"Livery clipboard: {car} ({bits})";
    }

    private readonly record struct GraphicsStats(int Paint, int Vinyl, int Decals, int Window, int Neon, int TotalSet)
    {
        public string ShortLabel
        {
            get
            {
                var bits = new List<string>();
                if (Paint > 0) bits.Add($"{Paint} paint");
                if (Vinyl > 0) bits.Add($"{Vinyl}/4 vinyl");
                if (Decals > 0) bits.Add($"{Decals} decals");
                if (Window > 0) bits.Add("window");
                if (Neon > 0) bits.Add("neon");
                return bits.Count == 0 ? "empty" : string.Join(" · ", bits);
            }
        }

        public string DetailLabel => ShortLabel + $"  ({TotalSet} hashes)";
    }

    private static GraphicsStats SummarizeGraphics(byte[] graphics)
    {
        int paint = 0, vinyl = 0, decals = 0, window = 0, neon = 0, total = 0;
        for (int i = 0; i < GraphicsSlots.Length; i++)
        {
            uint v = BitConverter.ToUInt32(graphics, i * 4);
            if (v == 0) continue;
            total++;
            int slot = GraphicsSlots[i];
            if (slot == 63 || (slot >= 68 && slot <= 76)) paint++;
            else if (slot >= 64 && slot <= 67) vinyl++;
            else if (slot >= 77 && slot <= 92) { /* vinyl colours — count with vinyl presence */ }
            else if (slot >= 97 && slot <= 152) decals++;
            else if (slot == 153) window++;
            else if (slot >= 154 && slot <= 162) neon++;
        }
        return new GraphicsStats(paint, vinyl, decals, window, neon, total);
    }

    private static byte[] ReadGraphics(byte[] working, int slot, bool menuMyCars = false)
    {
        int editor = DynoService.EditorBase(slot, menuMyCars);
        Ensure(working, editor + VisualBase + VisualSlotCount * 4);
        byte[] buf = new byte[GraphicsBytes];
        for (int i = 0; i < GraphicsSlots.Length; i++)
            Buffer.BlockCopy(working, editor + VisualBase + GraphicsSlots[i] * 4, buf, i * 4, 4);
        return buf;
    }

    private static void WriteGraphics(byte[] working, int slot, byte[] graphics, bool menuMyCars = false)
    {
        if (graphics.Length != GraphicsBytes)
            throw new InvalidOperationException("Invalid graphics pack size.");
        int editor = DynoService.EditorBase(slot, menuMyCars);
        Ensure(working, editor + VisualBase + VisualSlotCount * 4);
        for (int i = 0; i < GraphicsSlots.Length; i++)
            Buffer.BlockCopy(graphics, i * 4, working, editor + VisualBase + GraphicsSlots[i] * 4, 4);
    }

    private static void FillLegacyFields(LiveryPreset p, byte[] graphics)
    {
        uint Get(int visualSlot)
        {
            int idx = Array.IndexOf(GraphicsSlots, visualSlot);
            return idx < 0 ? 0u : BitConverter.ToUInt32(graphics, idx * 4);
        }

        var layers = new byte[16];
        var colors = new byte[64];
        for (int i = 0; i < 4; i++)
            BitConverter.GetBytes(Get(64 + i)).CopyTo(layers, i * 4);
        for (int i = 0; i < 16; i++)
            BitConverter.GetBytes(Get(77 + i)).CopyTo(colors, i * 4);
        p.LayersHex = Convert.ToHexString(layers);
        p.ColorsHex = Convert.ToHexString(colors);
        uint paint = Get(63);
        p.PaintHex = paint == 0 ? null : paint.ToString("X8");
    }

    /// <summary>Upgrade old library entries that only stored layers/colours/paint.</summary>
    private static void NormalizePreset(LiveryPreset p)
    {
        if (!string.IsNullOrWhiteSpace(p.GraphicsHex))
        {
            try
            {
                byte[] g = FromHexExact(p.GraphicsHex, GraphicsBytes);
                FillLegacyFields(p, g);
                p.Note = SummarizeGraphics(g).ShortLabel; // always keep list label short
                return;
            }
            catch { /* rebuild below */ }
        }

        // Legacy rebuild: sparse pack from layers/colors/paint only
        byte[] graphics = new byte[GraphicsBytes];
        void Set(int visualSlot, uint hash)
        {
            int idx = Array.IndexOf(GraphicsSlots, visualSlot);
            if (idx >= 0) BitConverter.GetBytes(hash).CopyTo(graphics, idx * 4);
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(p.PaintHex) &&
                uint.TryParse(p.PaintHex, System.Globalization.NumberStyles.HexNumber, null, out uint paint))
                Set(63, paint);

            if (!string.IsNullOrWhiteSpace(p.LayersHex))
            {
                byte[] layers = Convert.FromHexString(Norm(p.LayersHex));
                for (int i = 0; i + 4 <= layers.Length && i / 4 < 4; i += 4)
                    Set(64 + i / 4, BitConverter.ToUInt32(layers, i));
            }
            if (!string.IsNullOrWhiteSpace(p.ColorsHex))
            {
                byte[] colors = Convert.FromHexString(Norm(p.ColorsHex));
                for (int i = 0; i + 4 <= colors.Length && i / 4 < 16; i += 4)
                    Set(77 + i / 4, BitConverter.ToUInt32(colors, i));
            }
        }
        catch { /* leave zeros */ }

        p.GraphicsHex = Convert.ToHexString(graphics);
        FillLegacyFields(p, graphics);
        p.Note = SummarizeGraphics(graphics).ShortLabel;
    }

    private static byte[] ResolveGraphics(LiveryPreset p)
    {
        NormalizePreset(p);
        return FromHexExact(p.GraphicsHex, GraphicsBytes);
    }

    private static void Ensure(byte[] working, int end) =>
        ArgumentOutOfRangeException.ThrowIfGreaterThan(end, working.Length, nameof(working));

    private static bool IsValid(LiveryPreset p)
    {
        try
        {
            byte[] g = ResolveGraphics(p);
            return SummarizeGraphics(g).TotalSet > 0;
        }
        catch { return false; }
    }

    private static byte[] FromHexExact(string hex, int bytes)
    {
        string n = Norm(hex);
        if (n.Length != bytes * 2) throw new InvalidOperationException("Invalid livery hex size.");
        return Convert.FromHexString(n);
    }

    private static string ContentKey(LiveryPreset p)
    {
        NormalizePreset(p);
        return Norm(p.GraphicsHex);
    }

    private static string Norm(string? hex) =>
        (hex ?? "").Replace(" ", "").Replace("-", "").ToUpperInvariant();

    private static bool Prefer(LiveryPreset a, LiveryPreset b)
    {
        if (a.IsUserCreated != b.IsUserCreated) return a.IsUserCreated;
        if (a.IsBundled != b.IsBundled) return a.IsBundled;
        // Prefer fuller packs (v2 graphics over legacy vinyl-only)
        int sa = SafeSetCount(a), sb = SafeSetCount(b);
        return sa > sb;
    }

    private static int SafeSetCount(LiveryPreset p)
    {
        try { return SummarizeGraphics(ResolveGraphics(p)).TotalSet; }
        catch { return 0; }
    }

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

    private static void CompactPresetsInPlace(List<LiveryPreset> presets)
    {
        var keep = new Dictionary<string, LiveryPreset>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in presets)
        {
            NormalizePreset(p);
            if (!IsValid(p)) continue;
            string key = ContentKey(p);
            if (!keep.TryGetValue(key, out var existing) || Prefer(p, existing))
                keep[key] = p;
        }
        presets.Clear();
        presets.AddRange(keep.Values);
    }

    private static LiveryCatalog LoadLibraryMutable()
    {
        try
        {
            if (File.Exists(LibraryPath))
            {
                var cat = JsonSerializer.Deserialize<LiveryCatalog>(File.ReadAllText(LibraryPath));
                if (cat != null)
                {
                    cat.Presets ??= new List<LiveryPreset>();
                    return cat;
                }
            }
        }
        catch { /* fresh */ }
        return new LiveryCatalog
        {
            Note = "Full graphics packs: paint, vinyl, decals, window tint, neon.",
            Presets = new List<LiveryPreset>(),
        };
    }

    private static void SaveLibrary(LiveryCatalog cat)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath)!);
        if (cat.Presets.Count > LibrarySoftCap)
        {
            // Prefer keeping harvested / bundled stock; truncate overflow from non-bundled first.
            cat.Presets = cat.Presets
                .OrderByDescending(p => p.IsBundled)
                .ThenByDescending(p => p.IsUserCreated)
                .ThenBy(p => p.CarName, StringComparer.OrdinalIgnoreCase)
                .Take(LibrarySoftCap)
                .ToList();
        }
        File.WriteAllText(LibraryPath, JsonSerializer.Serialize(cat, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string ShortId(string material)
    {
        byte[] hash = SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(material));
        return Convert.ToHexString(hash)[..10];
    }
}
