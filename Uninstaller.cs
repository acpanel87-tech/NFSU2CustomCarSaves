namespace NFSU2CustomCarSaves;

/// <summary>
/// Wist alle door Custom Car Saves aangemaakte data.
/// Raakt je game-saves / NFSU2 BROWSER SAVE / nfsu2.xyz cloud NIET aan.
/// </summary>
public static class Uninstaller
{
    /// <summary>User libraries next to the exe (rack / dyno / livery).</summary>
    public static string DataDir => Path.Combine(AppContext.BaseDirectory, "Data");

    public static string AppDataDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NFSU2CustomCarSaves");

    public static string TempDir =>
        Path.Combine(Path.GetTempPath(), "NFSU2CustomCarSaves");

    public static string DesktopDir =>
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

    public sealed class WipeReport
    {
        public List<string> Removed { get; } = new();
        public List<string> Failed { get; } = new();
        public List<string> Skipped { get; } = new();
    }

    public static WipeReport Wipe(bool includeDesktopBackups, bool clearClipboard)
    {
        var report = new WipeReport();

        TryDeleteDir(DataDir, report);
        TryDeleteDir(AppDataDir, report);
        TryDeleteDir(TempDir, report);
        // Legacy LocalAppData / temp from Easy Car Adder rename
        TryDeleteDir(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NFSU2CarAdder"), report);
        TryDeleteDir(Path.Combine(Path.GetTempPath(), "NFSU2CarAdder"), report);

        if (includeDesktopBackups)
        {
            try
            {
                foreach (var pattern in new[] { "NFSU2CustomCarSaves_Backup_*", "NFSU2CarAdder_Backup_*" })
                {
                    foreach (var dir in Directory.GetDirectories(DesktopDir, pattern))
                        TryDeleteDir(dir, report);
                    foreach (var file in Directory.GetFiles(DesktopDir, pattern))
                        TryDeleteFile(file, report);
                }
            }
            catch (Exception ex)
            {
                report.Failed.Add("Desktop backups: " + ex.Message);
            }
        }
        else
        {
            report.Skipped.Add("Desktop backups (*_Backup_*) skipped.");
        }

        if (clearClipboard)
        {
            SlotClipboard.ClearMemory();
            try
            {
                if (Clipboard.ContainsText())
                {
                    string t = Clipboard.GetText();
                    if (t.StartsWith("NFSU2_CARSLOT_CLIP", StringComparison.Ordinal)
                        || t.Contains("tuned_hex", StringComparison.OrdinalIgnoreCase))
                        Clipboard.Clear();
                }
                report.Removed.Add("Clipboard (carslot)");
            }
            catch
            {
                report.Skipped.Add("Clipboard not cleared (busy/blocked).");
            }
        }

        return report;
    }

    private static void TryDeleteDir(string path, WipeReport report)
    {
        if (!Directory.Exists(path))
        {
            report.Skipped.Add(path + " (did not exist)");
            return;
        }
        try
        {
            Directory.Delete(path, recursive: true);
            report.Removed.Add(path);
        }
        catch (Exception ex)
        {
            report.Failed.Add(path + " — " + ex.Message);
        }
    }

    private static void TryDeleteFile(string path, WipeReport report)
    {
        try
        {
            File.Delete(path);
            report.Removed.Add(path);
        }
        catch (Exception ex)
        {
            report.Failed.Add(path + " — " + ex.Message);
        }
    }

    public static string Summarize(WipeReport r)
    {
        var lines = new List<string> { "Wipe complete.", "" };
        if (r.Removed.Count > 0)
        {
            lines.Add("Removed:");
            lines.AddRange(r.Removed.Select(x => "  • " + x));
            lines.Add("");
        }
        if (r.Skipped.Count > 0)
        {
            lines.Add("Skipped:");
            lines.AddRange(r.Skipped.Select(x => "  • " + x));
            lines.Add("");
        }
        if (r.Failed.Count > 0)
        {
            lines.Add("Failed (close the app and retry / Wipe-All.bat):");
            lines.AddRange(r.Failed.Select(x => "  • " + x));
            lines.Add("");
        }
        lines.Add("Untouched: game saves, 'NFSU2 BROWSER SAVE', Profile Master, Unity preview.");
        lines.Add("The program folder is still on disk — delete it manually if you want.");
        return string.Join(Environment.NewLine, lines);
    }
}
