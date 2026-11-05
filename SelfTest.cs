namespace NFSU2CustomCarSaves;

internal static class SelfTest
{
    public static int Run()
    {
        string fixture = FindFixture();
        Console.WriteLine("fixture=" + fixture);
        using var session = BrowserSaveSession.Open(fixture);
        Console.WriteLine($"profile={session.ProfileName} garage={session.GarageState} note={session.GarageNote}");

        if (session.GarageState != GarageState.Loaded)
        {
            Console.WriteLine("FAIL: expected Loaded garage");
            return 1;
        }

        var slots = session.Slots();
        Console.WriteLine($"slots={slots.Count}");
        foreach (var s in slots)
            Console.WriteLine($"  #{s.Index + 1} occ={s.IsOccupied} lock={s.IsLocked} {s.DisplayName}");

        // Export first occupied, import into last slot (10), save to temp copy
        var occ = slots.FirstOrDefault(s => s.IsOccupied);
        if (occ == null)
        {
            Console.WriteLine("WARN: no occupied slot to round-trip; fingerprint-only OK");
            return 0;
        }

        var pkg = session.ExportPackage(occ.Index, session.ProfileName);
        int dst = 9;
        session.ImportPackage(pkg, dst);
        var after = session.Slots()[dst];
        if (!after.IsOccupied || after.CarHash != occ.CarHash)
        {
            Console.WriteLine($"FAIL: import slot 10 hash {after.CarHash:X8} != {occ.CarHash:X8}");
            return 2;
        }

        // Round-trip fingerprint after CRC without writing user files: operate on copies
        var career = new byte[Layout.ProfileSize];
        Buffer.BlockCopy(session.Working, 0, career, 0, Layout.ProfileSize);
        Checksum.RecalculateCareerChecksums(career);
        var g = GarageDatService.BuildGarage(session.GarageOriginal!, session.Working, career);
        var check = GarageDatService.Check(g, career);
        Console.WriteLine($"after-build garage={check.State} {check.Note}");
        if (check.State != GarageState.Loaded)
        {
            Console.WriteLine("FAIL: fingerprint after rebuild");
            return 3;
        }

        string oldName = session.ReadInGameName();
        session.Rename("ZZTEST");
        if (session.ProfileName != "ZZTEST" || session.ReadInGameName() != "ZZTEST")
        {
            Console.WriteLine($"FAIL: rename → {session.ProfileName}/{session.ReadInGameName()}");
            return 4;
        }
        string zipPath = Path.Combine(Path.GetTempPath(), "NFSU2CustomCarSaves", "selftest_" + Guid.NewGuid().ToString("N") + ".zip");
        Directory.CreateDirectory(Path.GetDirectoryName(zipPath)!);
        session.ExportZip(zipPath);
        if (!File.Exists(zipPath) || new FileInfo(zipPath).Length < 1000)
        {
            Console.WriteLine("FAIL: zip export");
            return 5;
        }
        using (var z = System.IO.Compression.ZipFile.OpenRead(zipPath))
        {
            string expectCareer = $"NFS Underground 2/{session.ProfileName}/{session.ProfileName}";
            string expectGarage = $"NFS Underground 2/{session.ProfileName}/GARAGE.DAT";
            bool hasCareer = z.Entries.Any(e => e.FullName.Replace('\\', '/') == expectCareer);
            bool hasGarage = z.Entries.Any(e => e.FullName.Replace('\\', '/') == expectGarage);
            bool hasRegistry = z.Entries.Any(e => e.FullName.Replace('\\', '/') == "registry.txt");
            var settingsEntry = z.Entries.FirstOrDefault(e => e.FullName.Replace('\\', '/') == "settings.ini");
            bool hasOld = z.Entries.Any(e => e.FullName.EndsWith(".OLD", StringComparison.OrdinalIgnoreCase));
            Console.WriteLine($"zip entries={z.Entries.Count} career={hasCareer} garage={hasGarage} registry={hasRegistry} settings={settingsEntry != null} old={hasOld}");
            foreach (var e in z.Entries) Console.WriteLine("  " + e.FullName);
            if (!hasCareer || !hasGarage || !hasRegistry || settingsEntry == null || hasOld)
            {
                Console.WriteLine("FAIL: zip must be NFS Underground 2/Name/Name + GARAGE.DAT + registry.txt + settings.ini, no .OLD");
                return 6;
            }
            using (var sr = new StreamReader(settingsEntry.Open()))
            {
                string ini = sr.ReadToEnd();
                if (!ini.Contains($"roam_name = {session.ProfileName}", StringComparison.Ordinal) ||
                    ini.Contains("{{ROAM_NAME}}", StringComparison.Ordinal))
                {
                    Console.WriteLine("FAIL: settings.ini roam_name must match profile");
                    return 6;
                }
                if (!ini.Contains("language = english", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("FAIL: settings.ini must force language = english");
                    return 6;
                }
                if (ini.Contains("{{ROAM_FLAG}}", StringComparison.Ordinal))
                {
                    Console.WriteLine("FAIL: settings.ini still has {{ROAM_FLAG}} placeholder");
                    return 6;
                }
            }
        }
        Console.WriteLine($"rename+zip OK old={oldName} suggest={session.SuggestZipFileName()} zip={zipPath}");
        try { File.Delete(zipPath); } catch { /* ignore */ }

        var beforeTune = CarTuneService.Read(session.Working, occ.Index);
        string maxReport = session.MaxOut();
        Console.WriteLine("maxout=" + maxReport);
        var money = BitConverter.ToInt32(session.Working, Layout.MoneyOffset);
        if (money < 1_000_000)
        {
            Console.WriteLine($"FAIL: money after maxout={money}");
            return 7;
        }
        // 100% Save must NOT retune garage cars
        var afterTune = CarTuneService.Read(session.Working, occ.Index);
        if (afterTune.EstimatedLevel != beforeTune.EstimatedLevel || afterTune.PerfInstalled != beforeTune.PerfInstalled)
        {
            Console.WriteLine($"FAIL: maxout changed car tune level={beforeTune.EstimatedLevel}->{afterTune.EstimatedLevel}");
            return 8;
        }
        if (session.Working[0xBC + 4] != 1)
        {
            Console.WriteLine("FAIL: car lot unlock byte missing after maxout");
            return 8;
        }
        if (session.Working[SaveMaxOut.CareerCompleteFlag] != 1)
        {
            Console.WriteLine("FAIL: career complete flag missing after maxout");
            return 8;
        }
        if (session.Working[SaveMaxOut.GlobalShopTierStart] < SaveMaxOut.GlobalShopTierMax)
        {
            Console.WriteLine($"FAIL: global shop tier={session.Working[SaveMaxOut.GlobalShopTierStart]} after maxout");
            return 8;
        }
        int shopZeros = 0;
        for (int i = 0; i < SaveMaxOut.HiddenShopsLen; i++)
            if (session.Working[SaveMaxOut.HiddenShopsOffset + i] == 0) shopZeros++;
        if (shopZeros != 0)
        {
            Console.WriteLine($"FAIL: {shopZeros} hidden shops still locked after maxout");
            return 8;
        }
        Console.WriteLine($"cars unchanged OK · car lot + shops + tiers unlocked · slot{occ.Index + 1}");

        // Add-car into slot 6 (must keep Joey handle, not "06CR")
        var donors = CarDonorService.LoadAll();
        if (donors.Count == 0)
        {
            Console.WriteLine("FAIL: no cars in car-donors.json");
            return 9;
        }
        var donor = donors[0];
        session.PlaceDonorCar(5, donor);
        var s6 = session.Slots()[5];
        if (!s6.IsOccupied || s6.IsLocked || s6.CarHash != donor.CarHash)
        {
            Console.WriteLine($"FAIL: slot6 after Add car occ={s6.IsOccupied} lock={s6.IsLocked} hash={s6.CarHash:X8} expect={donor.CarHash:X8}");
            return 9;
        }
        int saveRec6 = Layout.SaveRecOffset(5);
        byte h0 = session.Working[saveRec6 + 4];
        if (h0 != 0xA6)
        {
            Console.WriteLine($"FAIL: slot6 handle@+4 should be 0xA6 (Joey), got 0x{h0:X2} — '01CR' bug?");
            return 9;
        }
        // Also paste into slot 7
        var pkg6 = session.ExportPackage(occ.Index, session.ProfileName);
        session.ImportPackage(pkg6, 6);
        var s7 = session.Slots()[6];
        if (!s7.IsOccupied || s7.CarHash != occ.CarHash)
        {
            Console.WriteLine($"FAIL: slot7 after import occ={s7.IsOccupied} hash={s7.CarHash:X8}");
            return 9;
        }
        string zip2 = Path.Combine(Path.GetTempPath(), "NFSU2CustomCarSaves", "slot6_" + Guid.NewGuid().ToString("N") + ".zip");
        session.ExportZip(zip2);
        using (var again = BrowserSaveSession.Open(zip2))
        {
            var chk = GarageDatService.Check(again.GarageOriginal, again.Career);
            Console.WriteLine($"reload garage={chk.State} {chk.Note}");
            if (chk.State != GarageState.Loaded) return 10;
            var r6 = again.Slots()[5];
            if (!r6.IsOccupied || r6.CarHash != donor.CarHash)
            {
                Console.WriteLine($"FAIL: reload slot6 occ={r6.IsOccupied} hash={r6.CarHash:X8} expect={donor.CarHash:X8}");
                return 11;
            }
            int saveRec = Layout.SaveRecOffset(5);
            byte bought = again.GarageOriginal![Layout.GarageHeaderSize + Layout.BoughtInSaveRec];
            byte locked = again.GarageOriginal[Layout.GarageHeaderSize + Layout.LockInSaveRec];
            byte handle = again.GarageOriginal[Layout.GarageHeaderSize + 4];
            Console.WriteLine($"garage slot6 bought={bought} lock={locked} handle=0x{handle:X2} (working saveRec={saveRec})");
            if (bought != 1 || locked != 0 || handle != 0xA6) return 12;
        }
        try { File.Delete(zip2); } catch { /* ignore */ }

        Console.WriteLine("OK");
        return 0;
    }

    private static string FindFixture()
    {
        string[] candidates =
        [
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "NFSU2 BROWSER SAVE")),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "nfsu2profilemaster-main", "NFSU2 BROWSER SAVE"),
            @"C:\Users\Leroyzn\Desktop\nfsu2profilemaster-main\NFSU2 BROWSER SAVE",
        ];
        foreach (var c in candidates)
            if (Directory.Exists(c) && File.Exists(Path.Combine(c, "GARAGE.DAT")))
                return c;
        throw new DirectoryNotFoundException("NFSU2 BROWSER SAVE fixture niet gevonden.");
    }
}
