using System.Text;



namespace NFSU2CustomCarSaves;



/// <summary>Waarom nfsu2.xyz slots 6–10 wel/niet laadt — console + UI rapport.</summary>

public static class GarageDiagnose

{

    public static string Run(BrowserSaveSession session)

    {

        var sb = new StringBuilder();

        sb.AppendLine("=== NFSU2 GARAGE DIAGNOSE ===");

        sb.AppendLine($"time        : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        sb.AppendLine($"source      : {session.SourcePath}");

        sb.AppendLine($"profile     : {session.ProfileName}");

        sb.AppendLine($"career path : {session.CareerPath}");

        sb.AppendLine($"garage path : {session.GaragePath ?? "(none)"}");

        sb.AppendLine($"dirty       : {session.Dirty}");

        sb.AppendLine();



        var career = session.Career;

        var garage = session.GarageOriginal;

        DiagnosePair(sb, career, garage, "current session");



        sb.AppendLine();

        sb.AppendLine("--- Working buffer slots (app view) ---");

        foreach (var s in session.Slots())

        {

            string tag = $"slot{s.Index + 1}";

            sb.AppendLine(

                $"  #{s.Index + 1:00} {tag,-5} lock={(s.IsLocked ? 1 : 0)} occ={(s.IsOccupied ? 1 : 0)} " +

                $"hash={s.CarHash:X8} {s.DisplayName}");

        }



        if (garage != null && session.Working.Length >= Layout.ExtraSlotRecordBase + 5 * Layout.SlotStride)

        {

            sb.AppendLine();

            sb.AppendLine("--- GARAGE.DAT SAVE_REC raw (slots 6–10) ---");

            for (int i = 0; i < 5; i++)

            {

                int off = Layout.GarageHeaderSize + i * Layout.SlotStride;

                byte lockB = garage[off + Layout.LockInSaveRec];

                byte bought = garage[off + Layout.BoughtInSaveRec];

                uint hash = BitConverter.ToUInt32(garage, off + Layout.SaveRecPrefix + Layout.CarHashInTuned);

                sb.AppendLine(

                    $"  file-slot {i + 6}: off=0x{off:X} lock@1072={lockB} bought@1073={bought} " +

                    $"hash@+28={hash:X8} head={Hex(garage, off, 16)}");

            }

        }



        sb.AppendLine();

        sb.AppendLine("--- What nfsu2.xyz needs ---");

        sb.AppendLine("  1. GARAGE.DAT next to profile: NFS Underground 2/<Name>/GARAGE.DAT");

        sb.AppendLine("  2. Magic N2GA, version=1, size=10202");

        sb.AppendLine("  3. Fingerprint @0x08 == FNV-1a64(career[36 .. 36+39230))");

        sb.AppendLine("  4. On mismatch → GARAGE.OLD, slots 6–10 are ignored");

        sb.AppendLine("  5. Locked slots (lock=1) show no car until Unlock/stage");

        sb.AppendLine();



        var verdict = Verdict(career, garage, session);

        sb.AppendLine("=== VERDICT ===");

        sb.AppendLine(verdict);

        return sb.ToString();

    }



    public static string RunOnFiles(string careerPath, string? garagePath)

    {

        var sb = new StringBuilder();

        sb.AppendLine("=== NFSU2 GARAGE DIAGNOSE (files) ===");

        byte[] career = File.ReadAllBytes(careerPath);

        byte[]? garage = garagePath != null && File.Exists(garagePath)

            ? File.ReadAllBytes(garagePath)

            : null;

        sb.AppendLine($"career : {careerPath} ({career.Length} B)");

        sb.AppendLine($"garage : {garagePath ?? "(none)"}");

        sb.AppendLine();

        DiagnosePair(sb, career, garage, "files");

        sb.AppendLine();

        sb.AppendLine("=== VERDICT ===");

        sb.AppendLine(Verdict(career, garage, session: null));

        return sb.ToString();

    }



    private static void DiagnosePair(StringBuilder sb, byte[] career, byte[]? garage, string label)

    {

        sb.AppendLine($"--- Check ({label}) ---");

        bool careerOk = GarageDatService.LooksLikeCareer(career);

        sb.AppendLine($"career 20CM     : {(careerOk ? "OK" : "FAIL")} len={career.Length} (need {Layout.ProfileSize})");

        if (careerOk && career.Length >= 4)

            sb.AppendLine($"career magic    : {Encoding.ASCII.GetString(career, 0, 4)}");



        if (garage == null)

        {

            sb.AppendLine("GARAGE.DAT      : MISSING → browser shows slots 1–5 only");

            return;

        }



        sb.AppendLine($"GARAGE size     : {garage.Length} (need {Layout.GarageSize})");

        bool gOk = GarageDatService.LooksLikeGarage(garage);

        string magic = garage.Length >= 4 ? Encoding.ASCII.GetString(garage, 0, 4) : "?";

        uint ver = garage.Length >= 8 ? BitConverter.ToUInt32(garage, 4) : 0;

        sb.AppendLine($"GARAGE magic    : {magic} (need N2GA)  version={ver} (need 1)  struct={(gOk ? "OK" : "FAIL")}");



        if (!careerOk || !gOk) return;



        ulong calc = GarageDatService.Fingerprint(career);

        ulong stored = GarageDatService.StoredFingerprint(garage);

        bool match = calc == stored;

        sb.AppendLine($"FNV calc        : {calc:X16}");

        sb.AppendLine($"FNV stored@0x08 : {stored:X16}");

        sb.AppendLine($"fingerprint     : {(match ? "MATCH ✓ — browser CAN load slots 6–10" : "MISMATCH ✗ — browser ignores 6–10 (→ GARAGE.OLD)")}");



        // header rest 0x10..0x1F

        sb.AppendLine($"header 0x10..1F : {Hex(garage, 0x10, 16)}  (drive/qr/stand handles, preserved on rebuild)");



        var check = GarageDatService.Check(garage, career);

        sb.AppendLine($"Check()         : {check.State} — {check.Note}");

    }



    private static string Verdict(byte[] career, byte[]? garage, BrowserSaveSession? session)

    {

        var check = GarageDatService.Check(garage, career);

        if (check.State == GarageState.Missing)

            return "FAIL: no GARAGE.DAT. Save zip… again (with nested NFS Underground 2/Name/).";

        if (check.State == GarageState.Invalid)

            return "FAIL: GARAGE.DAT invalid (magic/size/version).";

        if (check.State == GarageState.Stale)

            return "FAIL: fingerprint mismatch. Career changed without GARAGE fingerprint update.\n" +

                   "     Fix: open in this app → Save zip… (recalculates FNV) → Restore on nfsu2.xyz.";



        // fingerprint OK — check if extras are locked/empty

        if (session != null)

        {

            var extras = session.Slots().Where(s => s.Index >= 5).ToList();

            int locked = extras.Count(s => s.IsLocked);

            int filled = extras.Count(s => s.IsOccupied);

            if (locked == 5 && filled == 0)

                return "Fingerprint OK, but slots 6–10 are all LOCKED + empty.\n" +

                       "     → Unlock / MAX OUT, paste a car into 6–10, Save zip…, Restore.";

            if (filled == 0)

                return "Fingerprint OK, slots unlocked, but NO cars in 6–10.\n" +

                       "     → Paste/.carslot to slot 6–10, Save zip…, Restore.";

            if (locked > 0 && filled > 0)

                return $"Fingerprint OK. {filled} car(s) in 6–10, {locked} still locked.\n" +

                       "     Locked slots are invisible in-game until Unlock.";

            return $"OK: fingerprint match + {filled} car(s) in slots 6–10.\n" +

                   "     If the browser still does not show them: Restore the NEWEST zip, hard refresh,\n" +

                   "     and check that no old GARAGE.OLD is blocking the load.";

        }



        return "Fingerprint MATCH. Open the save in the app for slot details, or paste cars into 6–10.";

    }



    private static string Hex(byte[] data, int off, int len)

    {

        if (off + len > data.Length) len = Math.Max(0, data.Length - off);

        return Convert.ToHexString(data.AsSpan(off, len));

    }



    /// <summary>CLI: --diagnose [pad]</summary>

    public static int RunCli(string[] args)

    {

        int idx = Array.FindIndex(args, a => a.Equals("--diagnose", StringComparison.OrdinalIgnoreCase));

        string? path = idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;



        try

        {

            string report;

            if (string.IsNullOrWhiteSpace(path))

            {

                string fixture = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "NFSU2 BROWSER SAVE"));

                if (!Directory.Exists(fixture))

                {

                    Console.Error.WriteLine("Usage: NFSU2CustomCarSaves --diagnose <folder|zip|career-file>");

                    return 2;

                }

                path = fixture;

            }



            if (Directory.Exists(path) ||

                (File.Exists(path) && (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||

                                       path.EndsWith(".rar", StringComparison.OrdinalIgnoreCase))))

            {

                using var session = BrowserSaveSession.Open(path);

                report = Run(session);

            }

            else if (File.Exists(path))

            {

                string? g = Path.Combine(Path.GetDirectoryName(path)!, "GARAGE.DAT");

                if (!File.Exists(g)) g = null;

                report = RunOnFiles(path, g);

            }

            else

            {

                Console.Error.WriteLine("Path not found: " + path);

                return 2;

            }



            Console.WriteLine(report);

            string outFile = Path.Combine(

                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),

                $"nfsu2-garage-diagnose-{DateTime.Now:yyyyMMdd_HHmmss}.txt");

            File.WriteAllText(outFile, report);

            Console.WriteLine();

            Console.WriteLine("Report also on Desktop: " + outFile);

            return report.Contains("MISMATCH", StringComparison.Ordinal) ||

                   report.Contains("FAIL:", StringComparison.Ordinal)

                ? 1

                : 0;

        }

        catch (Exception ex)

        {

            Console.Error.WriteLine("DIAGNOSE ERROR: " + ex.Message);

            return 3;

        }

    }

}

