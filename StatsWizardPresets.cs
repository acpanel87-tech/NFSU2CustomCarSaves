using System.Text.Json;
using System.Text.Json.Serialization;

namespace NFSU2CustomCarSaves;

/// <summary>One Fully Modded wizard action (proven/probable only).</summary>
public sealed class StatsWizardAction
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public bool Enabled { get; set; }
    public string? Target { get; set; }
}

public sealed class StatsWizardPreset
{
    public string Name { get; set; } = "Custom";
    public List<StatsWizardAction> Actions { get; set; } = new();
}

/// <summary>Built-in + user Fully Modded presets (JSON in Data/).</summary>
public static class StatsWizardPresets
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string PresetsDir
    {
        get
        {
            string dir = Path.Combine(AppPaths.DataDir, "stats_wizard_presets");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static IReadOnlyList<StatsWizardAction> DefaultActions() =>
    [
        new() { Id = "bank", Label = "Bank", Enabled = true, Target = Layout.MaxMoney.ToString() },
        new() { Id = "complete", Label = "Game Completion flag", Enabled = true, Target = "1" },
        new() { Id = "entered", Label = "Races Entered (proven)", Enabled = false, Target = "246" },
        new() { Id = "shops_all", Label = "Hidden shops → all", Enabled = true, Target = SaveMaxOut.HiddenShopsLen.ToString() },
        new() { Id = "extreme", Label = "Shop catalogs Extreme", Enabled = true, Target = "3" },
        new() { Id = "car_lot", Label = "Car lot → Jane", Enabled = true },
        new() { Id = "win_record", Label = "Force 100% Win%", Enabled = true },
        new() { Id = "career_jane", Label = "Career progress → Jane", Enabled = false },
        new() { Id = "slots", Label = "Unlock all garage slots", Enabled = true },
        new() { Id = "rep_s1", Label = "Total Rep Stage 1", Enabled = false, Target = "800" },
        new() { Id = "rep_s2", Label = "Total Rep Stage 2", Enabled = false, Target = "4500" },
        new() { Id = "rep_s3", Label = "Total Rep Stage 3", Enabled = false, Target = "7500" },
        new() { Id = "rep_s4", Label = "Total Rep Stage 4", Enabled = false, Target = "9400" },
        new() { Id = "rep_s5", Label = "Total Rep Stage 5", Enabled = false, Target = "11800" },
        new() { Id = "spent", Label = "Total Spent slot 0 (probable)", Enabled = false, Target = "119850" },
    ];

    public static StatsWizardPreset BuiltIn(string name) => name switch
    {
        "Legit-ish" => new StatsWizardPreset
        {
            Name = "Legit-ish",
            Actions = DefaultActions().Select(a =>
            {
                a = Clone(a);
                a.Enabled = a.Id is "bank" or "shops_all" or "extreme" or "car_lot";
                if (a.Id == "bank") a.Target = "500000";
                return a;
            }).ToList(),
        },
        "Max everything" => new StatsWizardPreset
        {
            Name = "Max everything",
            Actions = DefaultActions().Select(a =>
            {
                a = Clone(a);
                a.Enabled = a.Id is not ("rep_s1" or "rep_s2" or "rep_s3" or "rep_s4" or "rep_s5" or "spent" or "entered");
                if (a.Id.StartsWith("rep_s", StringComparison.Ordinal)) { a.Enabled = true; a.Target = "20000"; }
                if (a.Id == "spent") { a.Enabled = true; a.Target = "999999"; }
                if (a.Id == "entered") { a.Enabled = true; a.Target = "999"; }
                if (a.Id == "career_jane") a.Enabled = true;
                return a;
            }).ToList(),
        },
        // Custom = mirror the open save: nothing selected; targets filled by UI sync.
        _ => new StatsWizardPreset
        {
            Name = "Custom",
            Actions = DefaultActions().Select(a =>
            {
                a = Clone(a);
                a.Enabled = false;
                return a;
            }).ToList(),
        },
    };

    private static StatsWizardAction Clone(StatsWizardAction a) => new()
    {
        Id = a.Id, Label = a.Label, Enabled = a.Enabled, Target = a.Target,
    };

    public static void SavePreset(StatsWizardPreset preset)
    {
        string safe = string.Join("_", (preset.Name ?? "Custom").Split(Path.GetInvalidFileNameChars()));
        if (string.IsNullOrWhiteSpace(safe)) safe = "Custom";
        string path = Path.Combine(PresetsDir, safe + ".json");
        File.WriteAllText(path, JsonSerializer.Serialize(preset, JsonOpts));
    }

    public static StatsWizardPreset? LoadPreset(string nameOrPath)
    {
        string path = nameOrPath;
        if (!File.Exists(path))
        {
            string safe = string.Join("_", nameOrPath.Split(Path.GetInvalidFileNameChars()));
            path = Path.Combine(PresetsDir, safe + ".json");
        }
        if (!File.Exists(path)) return null;
        return JsonSerializer.Deserialize<StatsWizardPreset>(File.ReadAllText(path), JsonOpts);
    }

    public static IEnumerable<string> ListPresetNames()
    {
        yield return "Legit-ish";
        yield return "Max everything";
        yield return "Custom";
        foreach (var f in Directory.EnumerateFiles(PresetsDir, "*.json"))
            yield return Path.GetFileNameWithoutExtension(f);
    }

    /// <summary>Apply enabled proven/probable actions. Returns combined edit summary.</summary>
    public static string Apply(byte[] working, IEnumerable<StatsWizardAction> actions)
    {
        // Bank last so later Jane/career copies never clobber an explicit money target.
        var ordered = actions
            .Where(a => a.Enabled)
            .OrderBy(a => a.Id == "bank" ? 1 : 0)
            .ToList();

        var logs = new List<string>();
        foreach (var a in ordered)
        {
            switch (a.Id)
            {
                case "bank":
                    CareerStatsService.SetMoney(working, ParseInt(a.Target, Layout.MaxMoney));
                    logs.Add($"bank={CareerStatsService.ReadMoney(working):N0}");
                    break;
                case "complete":
                    CareerStatsService.SetCareerComplete(working, ParseInt(a.Target, 1) != 0);
                    logs.Add($"complete={(CareerStatsService.IsCareerComplete(working) ? 1 : 0)}");
                    break;
                case "shops_all":
                    logs.Add(CareerStatsService.SetHiddenShopsUnlockedCount(working, ParseInt(a.Target, SaveMaxOut.HiddenShopsLen)));
                    break;
                case "extreme":
                    logs.Add(CareerStatsService.SetShopCatalogsExtreme(working));
                    break;
                case "car_lot":
                    logs.Add(CareerStatsService.ApplyCarLot(working));
                    break;
                case "win_record":
                    logs.Add(CareerStatsService.ApplyPerfectWinRecord(working));
                    break;
                case "career_jane":
                    logs.Add(CareerStatsService.ApplyCareerProgress(working, forceComplete: true));
                    break;
                case "slots":
                    logs.Add(CareerStatsService.UnlockAllGarageSlots(working));
                    break;
                case "rep_s1":
                case "rep_s2":
                case "rep_s3":
                case "rep_s4":
                case "rep_s5":
                    int stage = a.Id[^1] - '0';
                    float rep = ParseFloat(a.Target, 0f);
                    logs.Add(CareerStatsService.SetStageTotalReputation(working, stage, rep).Summary);
                    break;
                case "spent":
                    logs.Add(CareerStatsService.SetTotalSpentOnCar(working, ParseInt(a.Target, 0), slot0to4: 0).Summary);
                    break;
                case "entered":
                    uint entered = (uint)Math.Max(0, ParseInt(a.Target, 0));
                    bool ext = entered > Layout.RacesEnteredSafeMax;
                    logs.Add(CareerStatsService.SetRacesEntered(working, entered, extended: ext).Summary);
                    break;
                default:
                    logs.Add($"skip unknown {a.Id}");
                    break;
            }
        }
        CareerStatsService.EnsureCareerChecksums(working);
        return logs.Count == 0 ? "(nothing selected)" : string.Join(" · ", logs);
    }

    /// <summary>Accept plain ints or grouped forms like 999,999,999 / 999.999.999.</summary>
    private static int ParseInt(string? s, int fallback)
    {
        if (string.IsNullOrWhiteSpace(s)) return fallback;
        string cleaned = s.Trim()
            .Replace(" ", "", StringComparison.Ordinal)
            .Replace(",", "", StringComparison.Ordinal)
            .Replace(".", "", StringComparison.Ordinal)
            .Replace("_", "", StringComparison.Ordinal);
        return int.TryParse(cleaned, System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out int v)
            ? v
            : fallback;
    }

    private static float ParseFloat(string? s, float fallback)
    {
        if (string.IsNullOrWhiteSpace(s)) return fallback;
        if (float.TryParse(s, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float v))
            return v;
        // Dutch-style 20000,5 → try comma as decimal
        if (float.TryParse(s.Replace(',', '.'), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out v))
            return v;
        return fallback;
    }
}
