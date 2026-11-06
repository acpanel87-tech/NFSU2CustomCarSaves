using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace NFSU2CustomCarSaves;

/// <summary>One Career Stats schema field (bundled + optional user-defined).</summary>
public sealed class StatsSchemaField
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string Section { get; set; } = "";
    public string Status { get; set; } = "unverified";
    public string Type { get; set; } = "unknown";
    public string? Offset { get; set; }
    public int? OffsetDec { get; set; }
    public string? Control { get; set; }
    public string? Reason { get; set; }
    public bool Writable { get; set; }
    public string? Formula { get; set; }
    public StatsSchemaLimit? SaveLimit { get; set; }
    public StatsSchemaLimit? GameLimit { get; set; }
    public double? Step { get; set; }
    public bool IsUserDefined { get; set; }

    public bool IsEditableStatus =>
        Status is "proven" or "probable" or "blob" or "derived";

    public int? ResolveOffset()
    {
        if (OffsetDec.HasValue) return OffsetDec;
        if (string.IsNullOrWhiteSpace(Offset)) return null;
        string s = Offset.Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(s.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int hex))
            return hex;
        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int dec))
            return dec;
        return null;
    }
}

public sealed class StatsSchemaLimit
{
    public double Min { get; set; }
    public double Max { get; set; }
}

/// <summary>Loads <c>Stats/stats.schema.json</c> + merges user fields from Data.</summary>
public static class StatsSchemaStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string BundledPath
    {
        get
        {
            string nextToExe = Path.Combine(AppContext.BaseDirectory, "Stats", "stats.schema.json");
            if (File.Exists(nextToExe)) return nextToExe;
            string dev = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Stats", "stats.schema.json"));
            return dev;
        }
    }

    public static string UserFieldsPath => Path.Combine(AppPaths.DataDir, "stats.schema.user.json");

    public static IReadOnlyList<StatsSchemaField> LoadAll()
    {
        var list = new List<StatsSchemaField>();
        TryLoadFile(BundledPath, list, userDefined: false);
        TryLoadFile(UserFieldsPath, list, userDefined: true);
        return list;
    }

    private static void TryLoadFile(string path, List<StatsSchemaField> into, bool userDefined)
    {
        try
        {
            if (!File.Exists(path)) return;
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (!doc.RootElement.TryGetProperty("fields", out var fields) || fields.ValueKind != JsonValueKind.Array)
                return;
            foreach (var el in fields.EnumerateArray())
            {
                var f = el.Deserialize<StatsSchemaField>(JsonOpts);
                if (f == null || string.IsNullOrWhiteSpace(f.Id)) continue;
                f.IsUserDefined = userDefined || f.IsUserDefined;
                int existing = into.FindIndex(x => x.Id.Equals(f.Id, StringComparison.OrdinalIgnoreCase));
                if (existing >= 0) into[existing] = f;
                else into.Add(f);
            }
        }
        catch
        {
            /* keep whatever loaded */
        }
    }

    /// <summary>Add or replace a user-defined field and persist to Data/stats.schema.user.json.</summary>
    public static void UpsertUserField(StatsSchemaField field)
    {
        ArgumentNullException.ThrowIfNull(field);
        if (string.IsNullOrWhiteSpace(field.Id))
            throw new ArgumentException("Field id required.", nameof(field));
        field.IsUserDefined = true;
        field.Status = string.IsNullOrWhiteSpace(field.Status) ? "probable" : field.Status;
        field.Writable = true;

        var root = new JsonObject();
        var arr = new JsonArray();
        if (File.Exists(UserFieldsPath))
        {
            try
            {
                var existing = JsonNode.Parse(File.ReadAllText(UserFieldsPath)) as JsonObject;
                if (existing?["fields"] is JsonArray old)
                {
                    foreach (var n in old)
                    {
                        if (n is not JsonObject o) continue;
                        string? id = o["id"]?.GetValue<string>();
                        if (id != null && id.Equals(field.Id, StringComparison.OrdinalIgnoreCase))
                            continue;
                        arr.Add(n.DeepClone());
                    }
                }
            }
            catch { /* start fresh array */ }
        }

        arr.Add(JsonSerializer.SerializeToNode(field, JsonOpts)!);
        root["fields"] = arr;
        root["notes"] = "User-defined Career Stats fields (Raw tab). Merged on load with bundled stats.schema.json.";
        Directory.CreateDirectory(AppPaths.DataDir);
        File.WriteAllText(UserFieldsPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    public static StatsSchemaField? FindById(string id) =>
        LoadAll().FirstOrDefault(f => f.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public static StatsSchemaField? FindByOffset(int offset) =>
        LoadAll().FirstOrDefault(f => f.ResolveOffset() == offset);
}
