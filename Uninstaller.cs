namespace NFSU2CustomCarSaves;

/// <summary>
/// Wist door Custom Car Saves aangemaakte data.
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

    /// <summary>
    /// Temp / session / LocalAppData / clipboards only.
    /// Does <b>not</b> delete save_rack / dyno_library / livery_library.
    /// </summary>
    public static WipeReport WipeAppTemp(bool includeDesktopBackups, bool clearClipboard)
    {
        var report = new WipeReport();

        TryDeleteDir(AppDataDir, report);
        TryDeleteDir(TempDir, report);
        // Legacy LocalAppData / temp from Easy Car Adder rename
        TryDeleteDir(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NFSU2CarAdder"), report);
        TryDeleteDir(Path.Combine(Path.GetTempPath(), "NFSU2CarAdder"), report);

        if (includeDesktopBackups)
            WipeDesktopBackups(report);
        else
            report.Skipped.Add("Desktop backups (*_Backup_*) skipped.");

        if (clearClipboard)
            ClearAllClipboards(report);

        report.Skipped.Add("Libraries kept (save_rack / dyno / livery).");
        return report;
    }

    /// <summary>
    /// Clears user libraries (save rack, dyno, livery) and clipboards.
    /// Caller should re-import stock afterward.
    /// </summary>
    public static WipeReport WipeLibraries(bool clearClipboard)
    {
        var report = new WipeReport();

        string data = DataDir;
        TryDeleteFile(Path.Combine(data, "save_rack.json"), report);
        TryDeleteFile(Path.Combine(data, "save_rack.json.bak125"), report);
        TryDeleteFile(Path.Combine(data, "dyno_library.json"), report);
        TryDeleteFile(Path.Combine(data, "livery_library.json"), report);

        if (clearClipboard)
            ClearAllClipboards(report);

        return report;
    }

    /// <summary>Full wipe (temp + libraries). Prefer <see cref="WipeAppTemp"/> / <see cref="WipeLibraries"/> from the UI.</summary>
    public static WipeReport Wipe(bool includeDesktopBackups, bool clearClipboard)
    {
        var report = WipeAppTemp(includeDesktopBackups, clearClipboard: false);
        // Remove the "libraries kept" note from the temp pass — full wipe deletes them.
        report.Skipped.RemoveAll(s => s.Contains("Libraries kept", StringComparison.OrdinalIgnoreCase));

        var libs = WipeLibraries(clearClipboard);
        report.Removed.AddRange(libs.Removed);
        report.Failed.AddRange(libs.Failed);
        report.Skipped.AddRange(libs.Skipped);

        // Legacy: wipe entire Data folder leftover files too
        TryDeleteDir(DataDir, report);
        return report;
    }

    /// <summary>
    /// Second confirmation: user must type "delete" (case-insensitive) to enable Delete.
    /// </summary>
    public static bool ConfirmTypeDelete(Form owner, string title, string body)
    {
        using var f = new Form
        {
            Text = title,
            ClientSize = new Size(460, 260),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
        };
        UiTheme.ApplyForm(f);

        var lbl = new Label
        {
            Text = body,
            Left = 18,
            Top = 14,
            AutoSize = true,
            MaximumSize = new Size(424, 0),
            ForeColor = UiTheme.Text,
        };

        var prompt = new Label
        {
            Text = "Type delete to confirm:",
            Left = 18,
            Top = 130,
            AutoSize = true,
            ForeColor = UiTheme.TextMuted,
        };

        var tb = new TextBox
        {
            Left = 18,
            Top = 152,
            Width = 424,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = UiTheme.Elevated,
            ForeColor = UiTheme.TextStrong,
            Font = UiTheme.UiFont(10f),
        };

        var ok = UiTheme.Btn("Delete", () => f.DialogResult = DialogResult.OK, danger: true);
        ok.Enabled = false;
        var cancel = UiTheme.Btn("Cancel", () => f.DialogResult = DialogResult.Cancel);

        void SyncOk(object? _, EventArgs __) =>
            ok.Enabled = string.Equals(tb.Text.Trim(), "delete", StringComparison.OrdinalIgnoreCase);

        tb.TextChanged += SyncOk;

        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 54,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(14, 8, 14, 4),
            BackColor = UiTheme.BgDeep,
        };
        bar.Controls.Add(ok);
        bar.Controls.Add(cancel);

        // Layout body/prompt after auto-size so long text fits
        f.Controls.Add(lbl);
        f.Controls.Add(prompt);
        f.Controls.Add(tb);
        f.Controls.Add(bar);

        f.Shown += (_, _) =>
        {
            int y = Math.Max(lbl.Bottom + 10, 100);
            prompt.Top = y;
            tb.Top = prompt.Bottom + 6;
            int need = tb.Bottom + bar.Height + 16;
            if (f.ClientSize.Height < need)
                f.ClientSize = new Size(f.ClientSize.Width, need);
        };

        f.AcceptButton = ok;
        f.CancelButton = cancel;
        f.ActiveControl = tb;
        return f.ShowDialog(owner) == DialogResult.OK
               && string.Equals(tb.Text.Trim(), "delete", StringComparison.OrdinalIgnoreCase);
    }

    private static void WipeDesktopBackups(WipeReport report)
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

    private static void ClearAllClipboards(WipeReport report)
    {
        SlotClipboard.ClearMemory();
        try { DynoService.ClearClipboard(); } catch { /* ok */ }
        try { LiveryService.ClearClipboard(); } catch { /* ok */ }
        try
        {
            if (Clipboard.ContainsText())
            {
                string t = Clipboard.GetText();
                if (t.StartsWith("NFSU2_CARSLOT_CLIP", StringComparison.Ordinal)
                    || t.Contains("tuned_hex", StringComparison.OrdinalIgnoreCase))
                    Clipboard.Clear();
            }
            report.Removed.Add("Clipboard (carslot / dyno / livery)");
        }
        catch
        {
            report.Skipped.Add("Clipboard not cleared (busy/blocked).");
        }
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
        if (!File.Exists(path))
        {
            report.Skipped.Add(path + " (did not exist)");
            return;
        }
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

    public static string Summarize(WipeReport r, string headline = "Wipe complete.")
    {
        var lines = new List<string> { headline, "" };
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
        return string.Join(Environment.NewLine, lines);
    }
}
