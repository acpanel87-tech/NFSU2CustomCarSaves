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
    [JsonPropertyName("source_profile")] public string SourceProfile { get; set; } = "";
    [JsonPropertyName("tuned_hex")] public string TunedHex { get; set; } = "";
    [JsonPropertyName("inventory_hex")] public string? InventoryHex { get; set; }
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
