using System.Text.Json;
using System.Text.Json.Serialization;

namespace NFSU2CustomCarSaves;

/// <summary>
/// Dyno setups: 5 events × 41 bytes at SlotEditorBase+0x354 (hackch).
/// Bytes zijn relatief t.o.v. de geïnstalleerde parts van DÉZE auto — dus kopiëren
/// mag naar elke auto, maar absolute ratio's/power voelen anders per model/parts.
/// </summary>
public sealed class DynoPackage
{
    [JsonPropertyName("format")] public string Format { get; set; } = "nfsu2-dyno-v1";
    [JsonPropertyName("source_car")] public string SourceCar { get; set; } = "";
    [JsonPropertyName("source_save")] public string SourceSave { get; set; } = "";
    [JsonPropertyName("source_slot")] public int SourceSlot { get; set; }
    [JsonPropertyName("source_hash")] public string SourceHash { get; set; } = "";
    [JsonPropertyName("note")] public string Note { get; set; } = "";
    /// <summary>205 bytes hex: Circuit, Drag, Drift, Street X, URL.</summary>
    [JsonPropertyName("dyno_hex")] public string DynoHex { get; set; } = "";
}

public static class DynoService
{
    public static readonly string[] EventNames =
        ["Circuit", "Drag", "Drift", "Street X", "URL"];

    public static DynoPackage? ClipboardPkg { get; private set; }

    /// <summary>Clear in-memory dyno clip and delete persisted file so startup stays empty.</summary>
    public static void ClearClipboard()
    {
        ClipboardPkg = null;
        try
        {
            AppPaths.EnsureMigrated();
            if (File.Exists(AppPaths.DynoClipboardPath))
                File.Delete(AppPaths.DynoClipboardPath);
        }
        catch { /* ok */ }
    }

    public static byte[] ReadRaw(byte[] working, int slot)
    {
        int baseOff = Layout.SlotEditorOffset(slot);
        Ensure(working, baseOff);
        var buf = new byte[Layout.DynoTotalSize];
        Buffer.BlockCopy(working, baseOff + Layout.DynoOff, buf, 0, Layout.DynoTotalSize);
        return buf;
    }

    public static void WriteRaw(byte[] working, int slot, byte[] dyno205)
    {
        if (dyno205.Length != Layout.DynoTotalSize)
            throw new ArgumentException($"Dyno must be {Layout.DynoTotalSize} bytes.");
        int baseOff = Layout.SlotEditorOffset(slot);
        Ensure(working, baseOff);
        Buffer.BlockCopy(dyno205, 0, working, baseOff + Layout.DynoOff, Layout.DynoTotalSize);
    }

    public static DynoPackage Export(byte[] working, int slot, string? saveName = null, string? note = null)
    {
        var info = GarageDatService.ReadSlot(working, slot);
        if (!info.IsOccupied)
            throw new InvalidOperationException("Slot is empty — no dyno to copy.");
        byte[] raw = ReadRaw(working, slot);
        string save = saveName ?? "";
        return new DynoPackage
        {
            SourceCar = info.DisplayName,
            SourceSave = save,
            SourceSlot = slot + 1,
            SourceHash = info.CarHash.ToString("X8"),
            Note = note ?? $"Dyno from {info.DisplayName} · {save} · slot {slot + 1}",
            DynoHex = Convert.ToHexString(raw),
        };
    }

    /// <summary>Read one event's 41-byte setup (0=Circuit … 4=URL).</summary>
    public static byte[] ReadEvent(byte[] working, int slot, int eventIndex)
    {
        if (eventIndex < 0 || eventIndex >= Layout.DynoSetupCount)
            throw new ArgumentOutOfRangeException(nameof(eventIndex));
        byte[] all = ReadRaw(working, slot);
        var one = new byte[Layout.DynoSetupSize];
        Buffer.BlockCopy(all, eventIndex * Layout.DynoSetupSize, one, 0, Layout.DynoSetupSize);
        return one;
    }

    public static void WriteEvent(byte[] working, int slot, int eventIndex, byte[] setup41)
    {
        if (eventIndex < 0 || eventIndex >= Layout.DynoSetupCount)
            throw new ArgumentOutOfRangeException(nameof(eventIndex));
        if (setup41.Length != Layout.DynoSetupSize)
            throw new ArgumentException($"Setup must be {Layout.DynoSetupSize} bytes.");
        byte[] all = ReadRaw(working, slot);
        Buffer.BlockCopy(setup41, 0, all, eventIndex * Layout.DynoSetupSize, Layout.DynoSetupSize);
        WriteRaw(working, slot, all);
    }

    public static readonly (string Label, int Index)[] SetupFields =
    [
        ("Spring F", 0), ("Spring R", 1), ("Shock F", 2), ("Shock R", 3),
        ("Sway F", 4), ("Sway R", 5), ("Ride height", 6), ("Steering", 7),
        ("Final drive", 9),
        ("Gear 1", 10), ("Gear 2", 11), ("Gear 3", 12), ("Gear 4", 13), ("Gear 5", 14), ("Gear 6", 15),
        ("NOS duration", 16), ("NOS flow", 17),
        ("Brake bias", 18), ("Grip F", 19), ("Grip R", 20), ("Downforce F", 21), ("Downforce R", 22),
    ];


    public static void Apply(byte[] working, int slot, DynoPackage pkg)
    {
        var info = GarageDatService.ReadSlot(working, slot);
        if (!info.IsOccupied)
            throw new InvalidOperationException("Target slot has no car.");
        string hex = (pkg.DynoHex ?? "").Replace(" ", "").Replace("-", "");
        if (hex.Length != Layout.DynoTotalSize * 2)
            throw new InvalidOperationException($"Invalid dyno ({hex.Length / 2} bytes, expected {Layout.DynoTotalSize}).");
        WriteRaw(working, slot, Convert.FromHexString(hex));
    }

    /// <summary>Kopieer alleen de gekozen events (bitmask bit0=Circuit … bit4=URL).</summary>
    public static void ApplySelective(byte[] working, int slot, DynoPackage pkg, int eventMask)
    {
        var info = GarageDatService.ReadSlot(working, slot);
        if (!info.IsOccupied)
            throw new InvalidOperationException("Target slot has no car.");
        string hex = (pkg.DynoHex ?? "").Replace(" ", "").Replace("-", "");
        if (hex.Length != Layout.DynoTotalSize * 2)
            throw new InvalidOperationException("Invalid dyno package.");
        byte[] src = Convert.FromHexString(hex);
        byte[] dst = ReadRaw(working, slot);
        for (int i = 0; i < Layout.DynoSetupCount; i++)
        {
            if ((eventMask & (1 << i)) == 0) continue;
            Buffer.BlockCopy(src, i * Layout.DynoSetupSize, dst, i * Layout.DynoSetupSize, Layout.DynoSetupSize);
        }
        WriteRaw(working, slot, dst);
    }

    public static void CopyToClipboard(DynoPackage pkg)
    {
        ClipboardPkg = pkg;
        try
        {
            string json = JsonSerializer.Serialize(pkg, new JsonSerializerOptions { WriteIndented = true });
            Clipboard.SetText("NFSU2_DYNO_CLIP\n" + json);
        }
        catch { /* ok */ }
        try
        {
            AppPaths.EnsureMigrated();
            File.WriteAllText(AppPaths.DynoClipboardPath,
                JsonSerializer.Serialize(pkg, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* ok */ }
    }

    public static DynoPackage? PasteFromClipboard()
    {
        try
        {
            if (Clipboard.ContainsText())
            {
                string t = Clipboard.GetText();
                if (t.StartsWith("NFSU2_DYNO_CLIP\n", StringComparison.Ordinal))
                    t = t["NFSU2_DYNO_CLIP\n".Length..];
                if (t.Contains("dyno_hex", StringComparison.OrdinalIgnoreCase))
                {
                    var pkg = JsonSerializer.Deserialize<DynoPackage>(t);
                    if (pkg != null && !string.IsNullOrWhiteSpace(pkg.DynoHex))
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
            string path = AppPaths.DynoClipboardPath;
            if (File.Exists(path))
            {
                var pkg = JsonSerializer.Deserialize<DynoPackage>(File.ReadAllText(path));
                if (pkg != null && !string.IsNullOrWhiteSpace(pkg.DynoHex))
                {
                    ClipboardPkg = pkg;
                    return pkg;
                }
            }
        }
        catch { /* ignore */ }
        return null;
    }

    public static string Summarize(DynoPackage? pkg)
    {
        if (pkg == null || string.IsNullOrWhiteSpace(pkg.DynoHex)) return "Dyno clipboard: empty";
        return $"Dyno clipboard: {pkg.SourceCar} ({pkg.SourceHash})";
    }

    private static void Ensure(byte[] working, int baseOff)
    {
        if (baseOff + Layout.DynoOff + Layout.DynoTotalSize > working.Length)
            throw new InvalidOperationException("Slot out of save range (dyno).");
    }
}
