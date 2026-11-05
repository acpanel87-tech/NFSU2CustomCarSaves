using System.Text.Json;
using System.Text.Json.Serialization;

namespace NFSU2CustomCarSaves;

/// <summary>One absolute-offset hex block from hackch/GabriLex car-donors.json.</summary>
public sealed class CarDonorBlockJson
{
    [JsonPropertyName("off")] public int Off { get; set; }
    [JsonPropertyName("hex")] public string Hex { get; set; } = "";
}

/// <summary>In-game donor car that can be written into a garage slot (hackch Replace/Add car).</summary>
public sealed class CarDonor
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public uint CarHash { get; init; }
    public string KeyHex { get; init; } = "";
    /// <summary>Slot-relative patches (off - SLOT_BASE_0).</summary>
    public IReadOnlyList<(int Rel, byte[] Data)> SlotBlocks { get; init; } = Array.Empty<(int, byte[])>();

    public override string ToString() => Name;
}

/// <summary>
/// Loads hackch <c>car-donors.json</c> and writes a stock donor into a slot
/// (appearance reset, vinyl/decal/neon cleared, perf/unique stock, dyno reset to midpoint 100).
/// </summary>
public static class CarDonorService
{
    /// <summary>hackch SLOT_BASE for career slot 0.</summary>
    public const int SlotBase0 = 0x5870;
    public static readonly int[] SlotBlockAbsOffs = [0x5870, 0x5950, 0x5B20];

    private static List<CarDonor>? _cache;

    public static string DonorsPath
    {
        get
        {
            string beside = Path.Combine(AppContext.BaseDirectory, "car-donors.json");
            if (File.Exists(beside)) return beside;
            string src = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "car-donors.json"));
            return File.Exists(src) ? src : beside;
        }
    }

    public static IReadOnlyList<CarDonor> LoadAll(bool refresh = false)
    {
        if (_cache != null && !refresh) return _cache;
        string path = DonorsPath;
        if (!File.Exists(path))
            throw new FileNotFoundException("car-donors.json missing — reinstall or copy from hackch NFSU2-Savegame-Editor.", path);

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var list = new List<CarDonor>();
        int id = 0;
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            var blocks = prop.Value.Deserialize<List<CarDonorBlockJson>>() ?? new();
            var slotBlocks = new List<(int Rel, byte[] Data)>();
            foreach (var b in blocks)
            {
                if (!SlotBlockAbsOffs.Contains(b.Off)) continue;
                byte[] data = HexToBytes(b.Hex);
                slotBlocks.Add((b.Off - SlotBase0, data));
            }
            if (slotBlocks.Count != SlotBlockAbsOffs.Length) continue;

            var first = slotBlocks.First(x => x.Rel == 0).Data;
            if (first.Length < 0x14 + 4) continue;
            uint hash = BitConverter.ToUInt32(first, 0x14);

            var keyBlock = slotBlocks.First(x => x.Rel == 0x5B20 - SlotBase0).Data;
            string key = keyBlock.Length >= 4
                ? Convert.ToHexString(keyBlock.AsSpan(keyBlock.Length - 4))
                : "";

            list.Add(new CarDonor
            {
                Id = id++,
                Name = prop.Name.Trim(),
                CarHash = hash,
                KeyHex = key,
                SlotBlocks = slotBlocks,
            });
        }

        _cache = list
            .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return _cache;
    }

    /// <summary>
    /// Write stock car into any slot 1–10 (hackch <c>setSlotCar</c>).
    /// Keeps donor graphics (Snoop/Capone/Rachel vinyls). Resets parts + dyno to stock midpoint
    /// (byte 100) so leftover ECU/turbo curves from the previous car do not stick.
    /// </summary>
    public static void PlaceInSlot(byte[] working, int slot, CarDonor donor)
    {
        if (slot < 0 || slot >= Layout.MaxSlotCount)
            throw new ArgumentOutOfRangeException(nameof(slot));
        if (working.Length < Layout.TunedOffset(slot) + Layout.TunedCarSize)
            throw new InvalidOperationException("Save buffer too small for this slot — Save zip needs slots 1–10.");

        int editor = Layout.SlotEditorOffset(slot);
        int saveRec = Layout.SaveRecOffset(slot);

        // Wipe previous visuals, then write the three per-slot donor blocks.
        int visualOff = editor + 0x18;
        int visualBytes = 170 * 4;
        if (visualOff + visualBytes <= working.Length)
            Array.Clear(working, visualOff, visualBytes);

        foreach (var (rel, data) in donor.SlotBlocks)
        {
            int at = editor + rel;
            if (at < 0 || at + data.Length > working.Length)
                throw new InvalidOperationException($"Car data does not fit in slot {slot + 1}.");
            Buffer.BlockCopy(data, 0, working, at, data.Length);
        }

        // Donor blocks stop before PERF — clear old car's installed parts + uniques
        int perfAt = editor + Layout.PerfOff;
        if (perfAt + Layout.PerfCount <= working.Length)
            Array.Clear(working, perfAt, Layout.PerfCount);

        int uniqAt = editor + Layout.UniquePerfOff;
        int uniqBytes = Layout.UniquePerfCount * 4;
        if (uniqAt + uniqBytes <= working.Length)
            Array.Clear(working, uniqAt, uniqBytes);

        // Stock relative midpoint (hackch): 100 = no ECU/turbo/gear offset from part defaults
        var stockDyno = new byte[Layout.DynoTotalSize];
        Array.Fill(stockDyno, (byte)100);
        DynoService.WriteRaw(working, slot, stockDyno);

        // hackch: lock=0, bought=1 — do not overwrite car body with "01CR"/"CR01"
        working[saveRec + Layout.LockInSaveRec] = 0;
        working[saveRec + Layout.BoughtInSaveRec] = 1;
        GarageDatService.EnsureExtraSlotHandle(working, slot);

        // Career garage counter (slots 1–5); slots 6–10 live in GARAGE.DAT
        if (slot < Layout.CareerSlotCount)
        {
            byte cur = working[Layout.GarageCarCountOffset];
            working[Layout.GarageCarCountOffset] = (byte)Math.Max(cur, Math.Min(slot + 1, 6));
        }
        else
        {
            GarageDatService.UpdateGarageCarCount(working);
        }
    }

    private static byte[] HexToBytes(string hex)
    {
        string[] parts = hex.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var buf = new byte[parts.Length];
        for (int i = 0; i < parts.Length; i++)
            buf[i] = Convert.ToByte(parts[i], 16);
        return buf;
    }
}
