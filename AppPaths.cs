namespace NFSU2CustomCarSaves;

/// <summary>
/// Portable layout next to the exe:
/// <code>
/// NFSU2CustomCarSaves.exe
/// README.txt
/// Assets/     UI (logo, flags, icon)
/// Data/       user libraries (writable)
/// Stock/      shipped content (Presets, DynoSaves, Templates, Stats, car-donors.json)
/// Tools/      Wipe-All.bat
/// </code>
/// Legacy root folders (<c>Presets</c>, <c>DYNO SAVES</c>, …) still resolve so old zips keep working.
/// </summary>
internal static class AppPaths
{
    private static bool _migrated;

    public static string RootDir => AppContext.BaseDirectory;

    public static string DataDir
    {
        get
        {
            string dir = Path.Combine(RootDir, "Data");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string AssetsDir => Path.Combine(RootDir, "Assets");
    public static string StockDir => Path.Combine(RootDir, "Stock");
    public static string ToolsDir => Path.Combine(RootDir, "Tools");

    public static string SaveRackPath => Path.Combine(DataDir, "save_rack.json");
    public static string DynoLibraryPath => Path.Combine(DataDir, "dyno_library.json");
    public static string LiveryLibraryPath => Path.Combine(DataDir, "livery_library.json");
    public static string DynoClipboardPath => Path.Combine(DataDir, "dyno_clipboard.json");
    public static string LiveryClipboardPath => Path.Combine(DataDir, "livery_clipboard.json");

    public static string StockPresetsDir
    {
        get
        {
            foreach (string c in StockPresetsCandidates())
                if (Directory.Exists(c)) return c;
            string primary = Path.Combine(StockDir, "Presets");
            Directory.CreateDirectory(primary);
            return primary;
        }
    }

    public static string StockDynoSavesDir
    {
        get
        {
            foreach (string c in DynoSavesCandidates())
                if (Directory.Exists(c)) return c;
            string primary = Path.Combine(StockDir, "DynoSaves");
            Directory.CreateDirectory(primary);
            return primary;
        }
    }

    public static string TemplatesDir
    {
        get
        {
            foreach (string c in TemplatesCandidates())
                if (Directory.Exists(c)) return c;
            string primary = Path.Combine(StockDir, "Templates");
            Directory.CreateDirectory(primary);
            return primary;
        }
    }

    public static string StatsSchemaDir
    {
        get
        {
            foreach (string c in StatsCandidates())
                if (Directory.Exists(c)) return c;
            string primary = Path.Combine(StockDir, "Stats");
            Directory.CreateDirectory(primary);
            return primary;
        }
    }

    public static string CarDonorsPath
    {
        get
        {
            foreach (string c in CarDonorsCandidates())
                if (File.Exists(c)) return c;
            return Path.Combine(StockDir, "car-donors.json");
        }
    }

    public static string BundledStatsSchemaPath
    {
        get
        {
            foreach (string c in StatsSchemaFileCandidates())
                if (File.Exists(c)) return c;
            return Path.Combine(StockDir, "Stats", "stats.schema.json");
        }
    }

    public static string AssetPath(params string[] parts) =>
        Path.Combine(new[] { AssetsDir }.Concat(parts).ToArray());

    public static IEnumerable<string> StockPresetsCandidates()
    {
        yield return Path.Combine(StockDir, "Presets");
        yield return Path.Combine(RootDir, "Presets");
        string? repo = RepoPath("Presets");
        if (repo != null) yield return repo;
    }

    public static IEnumerable<string> DynoSavesCandidates()
    {
        yield return Path.Combine(StockDir, "DynoSaves");
        yield return Path.Combine(RootDir, "DYNO SAVES");
        yield return Path.Combine(RootDir, "DynoSaves");
        string? repo = RepoPath("DYNO SAVES");
        if (repo != null) yield return repo;
        string cwd = Path.Combine(Environment.CurrentDirectory, "DYNO SAVES");
        yield return cwd;
        yield return Path.Combine(Environment.CurrentDirectory, "Stock", "DynoSaves");
    }

    public static IEnumerable<string> TemplatesCandidates()
    {
        yield return Path.Combine(StockDir, "Templates");
        yield return Path.Combine(RootDir, "Templates");
        string? repo = RepoPath("Templates");
        if (repo != null) yield return repo;
    }

    public static IEnumerable<string> StatsCandidates()
    {
        yield return Path.Combine(StockDir, "Stats");
        yield return Path.Combine(RootDir, "Stats");
        string? repo = RepoPath("Stats");
        if (repo != null) yield return repo;
    }

    public static IEnumerable<string> JaneCareerCandidates()
    {
        foreach (string templates in TemplatesCandidates())
        {
            yield return Path.Combine(templates, "JANE");
            yield return Path.Combine(templates, "Jane");
        }
    }

    public static IEnumerable<string> SettingsIniCandidates()
    {
        foreach (string templates in TemplatesCandidates())
            yield return Path.Combine(templates, "settings.ini");
    }

    public static IEnumerable<string> CarDonorsCandidates()
    {
        yield return Path.Combine(StockDir, "car-donors.json");
        yield return Path.Combine(RootDir, "car-donors.json");
        string? repo = RepoFile("car-donors.json");
        if (repo != null) yield return repo;
    }

    public static IEnumerable<string> StatsSchemaFileCandidates()
    {
        foreach (string dir in StatsCandidates())
            yield return Path.Combine(dir, "stats.schema.json");
    }

    /// <summary>Create DataDir + legacy LocalAppData migrate only (no stock seed).</summary>
    public static void EnsureMigrated()
    {
        if (_migrated) return;
        _migrated = true;
        Directory.CreateDirectory(DataDir);

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
    }

    /// <summary>
    /// Seed stock libraries from Stock/Presets + bundled DynoSaves (deduped; safe to call repeatedly).
    /// </summary>
    public static string SeedDefaultLibraries()
    {
        TrySeedFromStock("dyno_library.json", DynoLibraryPath);
        TrySeedFromStock("livery_library.json", LiveryLibraryPath);
        TrySeedFromStock("save_rack.json", SaveRackPath);

        int rack = 0, liv = 0, dyno = 0;
        try { rack = SaveRack.ImportFromBundledSaves(); }
        catch { /* ignore harvest errors */ }
        try { liv = LiveryService.EnsureStockLiveriesImported(); }
        catch { /* ignore */ }
        try { dyno = TunePresetService.EnsureDynoSavesImported(); }
        catch { /* ignore */ }

        return $"Stock ready — rack +{rack} · liveries +{liv} · dynos +{dyno}.";
    }

    private static void TrySeedFromStock(string fileName, string dest)
    {
        if (File.Exists(dest)) return;
        foreach (string presets in StockPresetsCandidates())
        {
            string stock = Path.Combine(presets, fileName);
            if (!File.Exists(stock)) continue;
            try
            {
                File.Copy(stock, dest, overwrite: false);
                return;
            }
            catch { /* try next */ }
        }
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

    private static string? RepoPath(string name)
    {
        try
        {
            string p = Path.GetFullPath(Path.Combine(RootDir, "..", "..", "..", name));
            return Directory.Exists(p) ? p : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? RepoFile(string name)
    {
        try
        {
            string p = Path.GetFullPath(Path.Combine(RootDir, "..", "..", "..", name));
            return File.Exists(p) ? p : null;
        }
        catch
        {
            return null;
        }
    }
}
