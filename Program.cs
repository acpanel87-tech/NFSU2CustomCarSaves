using System.Runtime.InteropServices;

namespace NFSU2CustomCarSaves;

static class Program
{
    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    [STAThread]
    static int Main(string[] args)
    {
        bool cli = args.Any(a =>
            a.Equals("--self-test", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("--diagnose", StringComparison.OrdinalIgnoreCase));
        if (cli) AllocConsole();

        if (args.Any(a => a.Equals("--self-test", StringComparison.OrdinalIgnoreCase)))
        {
            AppPaths.EnsureMigrated();
            try { AppPaths.SeedDefaultLibraries(); } catch { /* tests still need stock when possible */ }
            return SelfTest.Run();
        }
        if (args.Any(a => a.Equals("--diagnose", StringComparison.OrdinalIgnoreCase)))
            return GarageDiagnose.RunCli(args);

        ApplicationConfiguration.Initialize();

        // Restore Data\ + legacy LocalAppData (no stock seed here).
        AppPaths.EnsureMigrated();
        var settings = AppSettings.Load();
        UiTheme.ApplyFromSettings(settings);

        // Quiet auto-load when user already chose "Don't ask again" / Settings.
        if (!settings.AskLoadDefaultLibraries && settings.LoadDefaultLibraries)
        {
            try { AppPaths.SeedDefaultLibraries(); }
            catch { /* non-fatal */ }
        }

        // 1) Galaxy splash (random 5–8 s fake load)
        using (var splash = new SplashForm())
            splash.ShowDialog();

        // 2) Main window (fresh slate, no auto-open)
        var main = new MainForm();

        // 3) After first paint: library ask (if needed), then guide (if enabled)
        main.Shown += (_, _) =>
        {
            if (main.Tag as string == "startup-done") return;
            main.Tag = "startup-done";

            var s = AppSettings.Load();
            if (s.AskLoadDefaultLibraries)
            {
                using var ask = new StartupLibraryAskForm();
                ask.ShowDialog(main);
                switch (ask.Choice)
                {
                    case StartupLibraryChoice.LoadOnce:
                        try { AppPaths.SeedDefaultLibraries(); } catch { /* ignore */ }
                        main.ReloadLibrariesFromDisk();
                        break;
                    case StartupLibraryChoice.AlwaysLoad:
                        s.AskLoadDefaultLibraries = false;
                        s.LoadDefaultLibraries = true;
                        s.Save();
                        try { AppPaths.SeedDefaultLibraries(); } catch { /* ignore */ }
                        main.ReloadLibrariesFromDisk();
                        break;
                    // SkipOnce — leave racks as-is
                }
            }

            if (s.ShowGuideOnStartup)
            {
                using var guide = new GalaxyGuideForm();
                guide.ShowDialog(main);
            }
        };
        Application.Run(main);
        return 0;
    }
}
