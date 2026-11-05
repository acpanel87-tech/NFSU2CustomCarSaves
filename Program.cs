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
            return SelfTest.Run();
        if (args.Any(a => a.Equals("--diagnose", StringComparison.OrdinalIgnoreCase)))
            return GarageDiagnose.RunCli(args);

        ApplicationConfiguration.Initialize();

        // Restore rack / dyno / livery libraries into Data\ next to the exe (incl. legacy LocalAppData).
        AppPaths.EnsureMigrated();

        // 1) Galaxy splash (random 5–8 s fake load)
        using (var splash = new SplashForm())
            splash.ShowDialog();

        // 2) Main window (fresh slate, no auto-open)
        var main = new MainForm();

        // 3) Guide / credits once per process, after the first paint
        main.Shown += (_, _) =>
        {
            if (main.Tag as string == "guide-shown") return;
            main.Tag = "guide-shown";
            using var guide = new GalaxyGuideForm();
            guide.ShowDialog(main);
        };
        Application.Run(main);
        return 0;
    }
}
