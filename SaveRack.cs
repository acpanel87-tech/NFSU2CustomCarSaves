using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NFSU2CustomCarSaves;

/// <summary>Persistente rack-opslag in de Data-map naast de exe. Uitbreiden behoudt bestaande slots.</summary>
public sealed class SaveRack
{
    public const int Capacity = 125;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly CarSlotPackage?[] _slots = new CarSlotPackage?[Capacity];

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
            // Behoud alle bestaande entries; nieuwe Capacity vult met lege slots
            int n = Math.Min(Capacity, dto.Slots.Count);
            for (int i = 0; i < n; i++)
                rack._slots[i] = dto.Slots[i];
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
        // Altijd precies Capacity slots schrijven (null = leeg) zodat uitbreiding stabiel blijft
        var list = new List<CarSlotPackage?>(Capacity);
        for (int i = 0; i < Capacity; i++)
            list.Add(_slots[i]);
        var dto = new RackFile { Slots = list };
        File.WriteAllText(StorePath, JsonSerializer.Serialize(dto, JsonOpts), new UTF8Encoding(false));
    }

    public CarSlotPackage? Get(int index)
    {
        if ((uint)index >= Capacity) throw new ArgumentOutOfRangeException(nameof(index));
        return _slots[index];
    }

    public void Set(int index, CarSlotPackage? pkg)
    {
        if ((uint)index >= Capacity) throw new ArgumentOutOfRangeException(nameof(index));
        _slots[index] = pkg;
        Persist();
    }

    public void Clear(int index) => Set(index, null);

    public string Label(int index)
    {
        var p = Get(index);
        if (p == null || string.IsNullOrWhiteSpace(p.TunedHex))
            return $"R{index + 1:000}  rack  [ ]  (empty)";
        string name = p.CarHash != 0
            ? CarNames.Display(p.CarHash)
            : (string.IsNullOrWhiteSpace(p.CarName) ? p.XName : p.CarName);
        if (string.IsNullOrWhiteSpace(name)) name = "(unknown)";
        return $"R{index + 1:000}  rack  [*]  {name}";
    }

    public string Tooltip(int index)
    {
        var p = Get(index);
        if (p == null || string.IsNullOrWhiteSpace(p.TunedHex))
            return $"Rack {index + 1}\n(empty)";
        string from = string.IsNullOrWhiteSpace(p.SourceProfile) ? "" : $"From: {p.SourceProfile}\n";
        return CarNames.Tooltip(p.CarHash, $"Rack {index + 1}\n{from}".TrimEnd());
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
