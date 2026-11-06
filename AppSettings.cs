using System.Text.Json;
using System.Text.Json.Serialization;

namespace NFSU2CustomCarSaves;

/// <summary>Persisted app preferences in <c>Data\app_settings.json</c>.</summary>
internal sealed class AppSettings
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public bool AskLoadDefaultLibraries { get; set; } = true;
    public bool LoadDefaultLibraries { get; set; } = true;
    public bool ShowGuideOnStartup { get; set; } = true;

    /// <summary>NFSU2Classic | Dark | Light</summary>
    public string UiThemeName { get; set; } = "Dark";
    /// <summary>ARGB accent (lime for Classic default).</summary>
    public int UiAccentArgb { get; set; } = unchecked((int)0xFFB4FF2A);

    public static string StorePath
    {
        get
        {
            Directory.CreateDirectory(AppPaths.DataDir);
            return Path.Combine(AppPaths.DataDir, "app_settings.json");
        }
    }

    public static AppSettings Load()
    {
        try
        {
            string path = StorePath;
            if (!File.Exists(path)) return new AppSettings();
            var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonOpts);
            return s ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            string path = StorePath;
            File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOpts));
        }
        catch
        {
            /* ignore write errors */
        }
    }
}
