using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NFSU2CustomCarSaves;

public sealed class CarSlotPackage
{
    [JsonPropertyName("format")] public string Format { get; set; } = "NFSU2_CARSLOT_1";
    [JsonPropertyName("car_name")] public string CarName { get; set; } = "";
    [JsonPropertyName("xname")] public string XName { get; set; } = "";
    [JsonPropertyName("car_hash")] public uint CarHash { get; set; }
    /// <summary>e.g. "Jane · Career 3" or "Jane · Customize 1".</summary>
    [JsonPropertyName("source_profile")] public string SourceProfile { get; set; } = "";
    /// <summary>"Career" or "Customize" when known (optional; also encoded in SourceProfile).</summary>
    [JsonPropertyName("source_area")] public string? SourceArea { get; set; }
    [JsonPropertyName("tuned_hex")] public string TunedHex { get; set; } = "";
    [JsonPropertyName("inventory_hex")] public string? InventoryHex { get; set; }

    /// <summary>Short list tag: Career / Customize / empty.</summary>
    public string AreaTag
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(SourceArea))
            {
                if (LooksCustomize(SourceArea)) return "Customize";
                if (LooksCareer(SourceArea)) return "Career";
                return SourceArea.Trim();
            }
            string sp = SourceProfile ?? "";
            if (LooksCustomize(sp)) return "Customize";
            if (LooksCareer(sp)) return "Career";
            return "";
        }
    }

    public string AreaDisplay => AreaTag switch
    {
        "Customize" => "Customize Car Select",
        "Career" => "Career",
        _ => string.IsNullOrWhiteSpace(AreaTag) ? "" : AreaTag,
    };

    /// <summary>Save name without the · Career/Customize suffix.</summary>
    public string SaveDisplayName
    {
        get
        {
            string sp = (SourceProfile ?? "").Trim();
            if (string.IsNullOrEmpty(sp)) return "";
            int dot = sp.IndexOf(" · ", StringComparison.Ordinal);
            if (dot > 0) return sp[..dot].Trim();
            int slash = sp.IndexOf(" / My Cars", StringComparison.OrdinalIgnoreCase);
            if (slash > 0) return sp[..slash].Trim();
            return sp;
        }
    }

    public string? SlotDisplay
    {
        get
        {
            string sp = SourceProfile ?? "";
            // "… · Career 3" / "… · Customize 12"
            foreach (string key in new[] { "Career ", "Customize " })
            {
                int at = sp.LastIndexOf(key, StringComparison.OrdinalIgnoreCase);
                if (at < 0) continue;
                string num = sp[(at + key.Length)..].Trim();
                if (int.TryParse(num, out int n) && n > 0) return n.ToString();
            }
            return null;
        }
    }

    private static bool LooksCustomize(string s) =>
        s.Contains("Customize", StringComparison.OrdinalIgnoreCase) ||
        s.Contains("My Cars", StringComparison.OrdinalIgnoreCase);

    private static bool LooksCareer(string s) =>
        s.Contains("Career", StringComparison.OrdinalIgnoreCase);
}

public static class CarSlotIo
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public static CarSlotPackage Load(string path)
    {
        var pkg = JsonSerializer.Deserialize<CarSlotPackage>(File.ReadAllText(path))
                  ?? throw new InvalidDataException("Empty carslot file.");
        if (pkg.Format is not ("NFSU2_CARSLOT_1" or "NFSU2_CARSLOT" or "NFSU2_CARSLOT_2"))
            throw new InvalidDataException("Unknown carslot format.");
        if (string.IsNullOrWhiteSpace(pkg.TunedHex))
            throw new InvalidDataException("No TunedCar data.");
        return pkg;
    }

    public static void Save(CarSlotPackage pkg, string path) =>
        File.WriteAllText(path, JsonSerializer.Serialize(pkg, JsonOpts), new UTF8Encoding(false));

    public static (byte[] Tuned, byte[] Inventory) Decode(CarSlotPackage pkg)
    {
        byte[] tuned = Convert.FromHexString(pkg.TunedHex.Replace(" ", ""));
        if (tuned.Length != Layout.TunedCarSize)
            throw new InvalidDataException($"TunedCar must be {Layout.TunedCarSize} bytes.");
        byte[] inv;
        if (!string.IsNullOrWhiteSpace(pkg.InventoryHex))
        {
            inv = Convert.FromHexString(pkg.InventoryHex.Replace(" ", ""));
            if (inv.Length != Layout.InventorySize)
                throw new InvalidDataException($"Inventory must be {Layout.InventorySize} bytes.");
        }
        else
            inv = new byte[Layout.InventorySize];
        return (tuned, inv);
    }
}
