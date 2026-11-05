namespace NFSU2CustomCarSaves;

/// <summary>
/// User data lives in a <c>Data</c> folder next to the exe (travels with the program).
/// Stock content stays in <c>Presets</c> / <c>DYNO SAVES</c> / <c>Templates</c>.
/// One-shot migrate from old LocalAppData names so racks / libraries are not lost.
/// </summary>
internal static class AppPaths
{
    private static bool _migrated;

    public static string DataDir
    {
        get
        {
            string dir = Path.Combine(AppContext.BaseDirectory, "Data");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string SaveRackPath => Path.Combine(DataDir, "save_rack.json");
    public static string DynoLibraryPath => Path.Combine(DataDir, "dyno_library.json");
    public static string LiveryLibraryPath => Path.Combine(DataDir, "livery_library.json");
    public static string DynoClipboardPath => Path.Combine(DataDir, "dyno_clipboard.json");
    public static string LiveryClipboardPath => Path.Combine(DataDir, "livery_clipboard.json");

    public static string StockPresetsDir
    {
        get
        {
            string dir = Path.Combine(AppContext.BaseDirectory, "Presets");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string StockDynoSavesDir
    {
        get
        {
            string dir = Path.Combine(AppContext.BaseDirectory, "DYNO SAVES");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// <summary>Call once at startup before loading rack/libraries.</summary>
    public static void EnsureMigrated()
    {
        if (_migrated) return;
        _migrated = true;
        Directory.CreateDirectory(DataDir);

        // Prefer newest → oldest sources for each file (only copy if destination missing).
        string[] legacyRoots =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NFSU2CustomCarSaves"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NFSU2CarAdder"),
        ];

        TryMigrate("save_rack.json", SaveRackPath, legacyRoots);
        TryMigrate("save_rack.json.bak125", Path.Combine(DataDir, "save_rack.json.bak125"), legacyRoots);
        TryMigrate("dyno_library.json", DynoLibraryPath, legacyRoots);
        TryMigrate("livery_library.json", LiveryLibraryPath, legacyRoots);
        TryMigrate("dyno_clipboard.json", DynoClipboardPath, legacyRoots);

        // Seed Data from shipped Presets stock (beta pack) if still missing.
        TrySeedFromStock("dyno_library.json", DynoLibraryPath);
        TrySeedFromStock("livery_library.json", LiveryLibraryPath);
        TrySeedFromStock("save_rack.json", SaveRackPath);
    }

    private static void TrySeedFromStock(string fileName, string dest)
    {
        if (File.Exists(dest)) return;
        string stock = Path.Combine(AppContext.BaseDirectory, "Presets", fileName);
        if (!File.Exists(stock)) return;
        try { File.Copy(stock, dest, overwrite: false); }
        catch { /* ignore */ }
    }

    private static void TryMigrate(string fileName, string dest, IEnumerable<string> legacyRoots)
    {
        if (File.Exists(dest)) return;
        foreach (string root in legacyRoots)
        {
            string src = Path.Combine(root, fileName);
            if (!File.Exists(src)) continue;
            try
            {
                File.Copy(src, dest, overwrite: false);
                return;
            }
            catch
            {
                /* try next */
            }
        }
    }
}
