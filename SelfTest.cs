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
        if (money != Layout.MaxMoney)
        {
            Console.WriteLine($"FAIL: money after maxout={money} (expected {Layout.MaxMoney})");
            return 7;
        }
        int shopsUnlocked = SaveMaxOut.CountHiddenShopsUnlocked(session.Working);
        if (shopsUnlocked != SaveMaxOut.HiddenShopsLen)
        {
            Console.WriteLine($"FAIL: shops after maxout={shopsUnlocked}/{SaveMaxOut.HiddenShopsLen}");
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
        // Overall Wins / type wins / Win% remain runtime; Races Entered is probable @ 0xA16F (tested below).
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

        // Fresh Start = post-Rachel empty base (no cars, starter cash, CRC ok; not full MaxOut)
        using (var fresh = BrowserSaveSession.CreateFreshDefault("SelfTst"))
        {
            int myCars = MyCarsService.ReadAll(fresh.Working).Count(c => c.IsOccupied);
            int careerCars = fresh.Slots().Count(s => s.IsOccupied);
            if (myCars != 0 || careerCars != 0)
            {
                Console.WriteLine($"FAIL: fresh save still has cars career={careerCars} myCars={myCars}");
                return 11;
            }
            if (PlayerOptionsService.GetMoney(fresh.Working) != Layout.FreshStartMoney)
            {
                Console.WriteLine($"FAIL: fresh money={PlayerOptionsService.GetMoney(fresh.Working)} expect {Layout.FreshStartMoney}");
                return 11;
            }
            if (CareerStatsService.GetRacesEntered(fresh.Working) != 0
                || CareerStatsService.GetTotalSpentOnCar(fresh.Working, 0) != 0
                || CareerStatsService.GetTotalSpentOnCar(fresh.Working, 1) != 0
                || Math.Abs(CareerStatsService.GetStageTotalReputation(fresh.Working, 5)) > 0.01f
                || CareerStatsService.GetStageRepRaceCount(fresh.Working, 5) != 0)
            {
                Console.WriteLine("FAIL: fresh editable stats not zeroed (entered/spent/rep)");
                return 11;
            }
            if (CareerStatsService.IsCareerComplete(fresh.Working)
                || !CareerStatsService.GetCareerStarted(fresh.Working)
                || !CareerStatsService.CareerChecksumsOk(fresh.Working))
            {
                Console.WriteLine("FAIL: fresh base flags/CRC (expect started=1 complete=0 CRC ok)");
                return 11;
            }
            int emptyOff = MyCarsService.BlockOffset(0);
            if (fresh.Working[emptyOff + 12] != 2 || fresh.Working[emptyOff + MyCarsLayout.DynoRel] != 100)
            {
                Console.WriteLine("FAIL: fresh My Cars empty block missing Jane type/dyno markers");
                return 11;
            }

            var placeDonors = CarDonorService.LoadAll();
            if (placeDonors.Count < 2)
            {
                Console.WriteLine("FAIL: need ≥2 donors for fresh place test");
                return 11;
            }
            fresh.PlaceDonorCar(0, placeDonors[0]);
            fresh.PlaceDonorCar(1, placeDonors[1]);
            MyCarsService.PlaceDonor(fresh.Working, 0, placeDonors[0]);
            MyCarsService.PlaceDonor(fresh.Working, 1, placeDonors[1]);
            string max = SaveMaxOut.Apply(fresh.Working);
            Console.WriteLine("fresh+cars maxout=" + max);

            string janePath = AppPaths.JaneCareerCandidates().FirstOrDefault(File.Exists) ?? "";
            if (!File.Exists(janePath))
            {
                Console.WriteLine("FAIL: Jane template missing for fresh+cars CRC compare");
                return 14;
            }
            byte[] jane = File.ReadAllBytes(janePath);
            int seven = Layout.CareerSaveBase + Layout.Career7B00Rel;
            int shopLo = SaveMaxOut.HiddenShopsOffset;
            int shopHi = shopLo + SaveMaxOut.HiddenShopsLen;
            // Outside the Starladdey shop wipe window: Jane hashes + ForcePerfectWinRecord (1,0,0,1 → 1,1,0,1).
            // Inside that window MaxOut forces 0x01 bytes (overlaps mid-7B00).
            for (int i = 0; i < Layout.Career7B00Count; i++)
            {
                int o = seven + i * Layout.Career7B00Stride;
                bool overlapsShopWipe = o < shopHi && o + Layout.Career7B00Stride > shopLo;
                if (overlapsShopWipe)
                {
                    for (int b = 0; b < Layout.Career7B00Stride; b++)
                    {
                        int abs = o + b;
                        if (abs < shopLo || abs >= shopHi) continue;
                        if (fresh.Working[abs] != 1)
                        {
                            Console.WriteLine($"FAIL: shop-wipe window @0x{abs:X} expected 1 got {fresh.Working[abs]}");
                            return 12;
                        }
                    }
                    continue;
                }
                for (int b = 0; b < 4; b++)
                {
                    if (fresh.Working[o + b] != jane[o + b])
                    {
                        Console.WriteLine($"FAIL: 7B00 hash diverged from Jane at entry {i}+{b}");
                        return 12;
                    }
                }
                byte j4 = jane[o + 4], j5 = jane[o + 5], j6 = jane[o + 6], j7 = jane[o + 7];
                byte w4 = fresh.Working[o + 4], w5 = fresh.Working[o + 5], w6 = fresh.Working[o + 6], w7 = fresh.Working[o + 7];
                bool janeLoss = j4 == 1 && j5 == 0 && j6 == 0 && j7 == 1;
                if (janeLoss)
                {
                    if (w4 != 1 || w5 != 1 || w6 != 0 || w7 != 1)
                    {
                        Console.WriteLine($"FAIL: 7B00 entry {i} not force-won (got {w4},{w5},{w6},{w7})");
                        return 12;
                    }
                }
                else if (w4 != j4 || w5 != j5 || w6 != j6 || w7 != j7)
                {
                    Console.WriteLine($"FAIL: 7B00 flags diverged from Jane at entry {i}");
                    return 12;
                }
            }
            int c7148 = Layout.CareerSaveBase + Layout.Career7148Rel;
            for (int i = 0; i < Layout.Career7148RaceBytes; i += Layout.Career7148RaceStride)
            {
                int o = c7148 + i;
                uint hash = BitConverter.ToUInt32(jane, o);
                if (hash == 0) continue;
                if (jane[o + 4] == 0 && jane[o + 5] == 5 && jane[o + 6] == 0)
                {
                    if (fresh.Working[o + 5] != 1)
                    {
                        Console.WriteLine($"FAIL: Career7148 loss at +{i:X} not flipped to win");
                        return 12;
                    }
                }
            }
            int c887 = Layout.CareerSaveBase + Layout.Career887CRel;
            for (int i = 0; i < Layout.Career887CSize; i++)
            {
                if (fresh.Working[c887 + i] != jane[c887 + i])
                {
                    Console.WriteLine($"FAIL: Career887C diverged from Jane at +{i}");
                    return 12;
                }
            }
            int mc0 = MyCarsService.BlockOffset(0);
            if (fresh.Working[mc0] != 0x83 || fresh.Working[mc0 + 12] != 2 || fresh.Working[mc0 + 8] != 1)
            {
                Console.WriteLine("FAIL: My Cars slot1 header not Jane-valid after PlaceDonor");
                return 12;
            }
            int mc2 = MyCarsService.BlockOffset(2);
            if (fresh.Working[mc2 + 12] != 2 || fresh.Working[mc2 + 8] != 0 || fresh.Working[mc2 + MyCarsLayout.DynoRel] != 100)
            {
                Console.WriteLine("FAIL: empty My Cars slot3 lost Jane empty structure after place/maxout");
                return 12;
            }
            Console.WriteLine("fresh Start + place + 100% My Cars/7B00 OK");

            PlayerOptionsService.SetTransmission(fresh.Working, PlayerOptionsService.TransmissionMode.Automatic);
            if (PlayerOptionsService.GetTransmission(fresh.Working) != PlayerOptionsService.TransmissionMode.Automatic
                || fresh.Working[Layout.TransmissionManualOffset] != 0
                || fresh.Working[Layout.TransmissionAutoFlagOffset] != 1)
            {
                Console.WriteLine("FAIL: transmission Automatic write");
                return 13;
            }
            PlayerOptionsService.SetTransmission(fresh.Working, PlayerOptionsService.TransmissionMode.Manual);
            if (PlayerOptionsService.GetTransmission(fresh.Working) != PlayerOptionsService.TransmissionMode.Manual
                || fresh.Working[Layout.TransmissionManualOffset] != 1
                || fresh.Working[Layout.TransmissionAutoFlagOffset] != 0)
            {
                Console.WriteLine("FAIL: transmission Manual write");
                return 13;
            }
            Console.WriteLine("Player Transmission toggle OK");
        }

        // Packed-stage reputation + probable spent: set → CRC → read back → restore
        {
            string janePath = AppPaths.JaneCareerCandidates().FirstOrDefault(File.Exists) ?? "";
            if (!File.Exists(janePath))
            {
                Console.WriteLine("FAIL: Jane template missing for stats roundtrip");
                return 14;
            }
            byte[] statsBuf = File.ReadAllBytes(janePath);
            if (statsBuf.Length != Layout.ProfileSize)
            {
                Console.WriteLine($"FAIL: Jane size {statsBuf.Length} != {Layout.ProfileSize}");
                return 14;
            }
            Checksum.RecalculateCareerChecksums(statsBuf);
            if (!Checksum.CareerChecksumsMatch(statsBuf))
            {
                Console.WriteLine("FAIL: Jane CRC after recalc");
                return 14;
            }

            float origRep = CareerStatsService.GetStageTotalReputation(statsBuf, 5);
            byte origRaces = CareerStatsService.GetStageRepRaceCount(statsBuf, 5);
            int origSpent0 = CareerStatsService.GetTotalSpentOnCar(statsBuf, 0);
            int origSpent1 = CareerStatsService.GetTotalSpentOnCar(statsBuf, 1);
            uint origEntered = CareerStatsService.GetRacesEntered(statsBuf);
            byte origAfterBank = CareerStatsService.GetUnknownAfterBank(statsBuf);
            float expectAvg = origRaces == 0 ? 0f : origRep / origRaces;
            float gotAvg = CareerStatsService.GetStageAverageReputation(statsBuf, 5);
            if (Math.Abs(gotAvg - expectAvg) > 0.01f)
            {
                Console.WriteLine($"FAIL: avg formula {gotAvg} != {expectAvg}");
                return 14;
            }
            if (origEntered != 246 || origAfterBank != 1)
            {
                Console.WriteLine($"FAIL: Jane baseline entered={origEntered} afterBank={origAfterBank} (expect 246 / 1)");
                return 14;
            }
            // Spent slot offsets: editable 0..3; slot 4 is unverified junk.
            if (Layout.TotalSpentEditableSlotCount != 4
                || Layout.TotalSpentOnCarOffsetForSlot(0) != 0x6720
                || Layout.TotalSpentOnCarOffsetForSlot(1) != 0x6F12
                || Layout.TotalSpentOnCarOffsetForSlot(2) != 0x7704
                || Layout.TotalSpentOnCarOffsetForSlot(3) != 0x7EF6
                || Layout.TotalSpentOnCarOffsetForSlot(4) != 0x86E8)
            {
                Console.WriteLine("FAIL: TotalSpentOnCarOffsetForSlot map");
                return 14;
            }
            try
            {
                CareerStatsService.SetTotalSpentOnCar(statsBuf, 1, slot0to4: 4);
                Console.WriteLine("FAIL: spent slot4 should reject writes");
                return 14;
            }
            catch (ArgumentOutOfRangeException) { /* expected */ }

            var r1 = CareerStatsService.SetStageTotalReputation(statsBuf, 5, 20000f);
            if (!r1.ChecksumsOk || Math.Abs(CareerStatsService.GetStageTotalReputation(statsBuf, 5) - 20000f) > 0.01f)
            {
                Console.WriteLine("FAIL: stage5 rep roundtrip write " + r1.Summary);
                return 14;
            }
            var r2 = CareerStatsService.SetStageRepRaceCount(statsBuf, 5, 50);
            if (!r2.ChecksumsOk || CareerStatsService.GetStageRepRaceCount(statsBuf, 5) != 50)
            {
                Console.WriteLine("FAIL: stage5 races roundtrip " + r2.Summary);
                return 14;
            }
            var r3 = CareerStatsService.SetTotalSpentOnCar(statsBuf, 123456, slot0to4: 0);
            if (!r3.ChecksumsOk || CareerStatsService.GetTotalSpentOnCar(statsBuf, 0) != 123456)
            {
                Console.WriteLine("FAIL: spent slot0 roundtrip " + r3.Summary);
                return 14;
            }
            if (CareerStatsService.GetTotalSpentOnCar(statsBuf, 1) != origSpent1)
            {
                Console.WriteLine("FAIL: spent slot0 write touched slot1");
                return 14;
            }
            var r3b = CareerStatsService.SetTotalSpentOnCar(statsBuf, 654321, slot0to4: 1);
            if (!r3b.ChecksumsOk || CareerStatsService.GetTotalSpentOnCar(statsBuf, 1) != 654321
                || CareerStatsService.GetTotalSpentOnCar(statsBuf, 0) != 123456)
            {
                Console.WriteLine("FAIL: spent slot1 roundtrip " + r3b.Summary);
                return 14;
            }
            var r4 = CareerStatsService.SetRacesEntered(statsBuf, 777, extended: false);
            if (!r4.ChecksumsOk || CareerStatsService.GetRacesEntered(statsBuf) != 777)
            {
                Console.WriteLine("FAIL: races entered roundtrip " + r4.Summary);
                return 14;
            }
            if (CareerStatsService.GetUnknownAfterBank(statsBuf) != origAfterBank)
            {
                Console.WriteLine("FAIL: writing Entered touched unknown after Bank");
                return 14;
            }
            var r5 = CareerStatsService.SetRacesEntered(statsBuf, 100_000, extended: true);
            if (!r5.ChecksumsOk || CareerStatsService.GetRacesEntered(statsBuf) != 100_000)
            {
                Console.WriteLine("FAIL: races entered extended roundtrip " + r5.Summary);
                return 14;
            }
            // Safe mode must clamp >999
            var r6 = CareerStatsService.SetRacesEntered(statsBuf, 5000, extended: false);
            if (!r6.ChecksumsOk || CareerStatsService.GetRacesEntered(statsBuf) != Layout.RacesEnteredSafeMax)
            {
                Console.WriteLine("FAIL: races entered safe clamp " + r6.Summary);
                return 14;
            }

            CareerStatsService.SetStageTotalReputation(statsBuf, 5, origRep);
            CareerStatsService.SetStageRepRaceCount(statsBuf, 5, origRaces);
            CareerStatsService.SetTotalSpentOnCar(statsBuf, origSpent0, slot0to4: 0);
            CareerStatsService.SetTotalSpentOnCar(statsBuf, origSpent1, slot0to4: 1);
            CareerStatsService.SetRacesEntered(statsBuf, origEntered, extended: true);
            if (Math.Abs(CareerStatsService.GetStageTotalReputation(statsBuf, 5) - origRep) > 0.01f
                || CareerStatsService.GetStageRepRaceCount(statsBuf, 5) != origRaces
                || CareerStatsService.GetTotalSpentOnCar(statsBuf, 0) != origSpent0
                || CareerStatsService.GetTotalSpentOnCar(statsBuf, 1) != origSpent1
                || CareerStatsService.GetRacesEntered(statsBuf) != origEntered
                || CareerStatsService.GetUnknownAfterBank(statsBuf) != origAfterBank
                || !Checksum.CareerChecksumsMatch(statsBuf))
            {
                Console.WriteLine("FAIL: stats restore after roundtrip");
                return 14;
            }
            Console.WriteLine($"Stats rep/spent/entered roundtrip OK (S5 was {origRep}/{origRaces}, spent0 {origSpent0}, entered {origEntered})");

            // Verify Desktop samples (Mosta + Usman) when present — schema proof values.
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string mostaPath = Path.Combine(desktop, "Mosta", "Mosta");
            string usmanPath = Path.Combine(desktop, "Usman");
            if (File.Exists(mostaPath) && File.Exists(usmanPath))
            {
                byte[] mosta = File.ReadAllBytes(mostaPath);
                byte[] usman = File.ReadAllBytes(usmanPath);
                if (mosta.Length != Layout.ProfileSize || usman.Length != Layout.ProfileSize)
                {
                    Console.WriteLine($"FAIL: sample sizes mosta={mosta.Length} usman={usman.Length}");
                    return 14;
                }
                if (CareerStatsService.GetRacesEntered(mosta) != 246
                    || CareerStatsService.GetRacesEntered(usman) != 223)
                {
                    Console.WriteLine($"FAIL: sample entered mosta={CareerStatsService.GetRacesEntered(mosta)} usman={CareerStatsService.GetRacesEntered(usman)}");
                    return 14;
                }
                if (CareerStatsService.GetTotalSpentOnCar(mosta, 0) != 119850
                    || CareerStatsService.GetTotalSpentOnCar(usman, 0) != 17800
                    || CareerStatsService.GetTotalSpentOnCar(usman, 2) != 102950)
                {
                    Console.WriteLine("FAIL: sample spent slot map (expect Mosta0=119850 Usman0=17800 Usman2=102950)");
                    return 14;
                }
                uint junk4 = BitConverter.ToUInt32(mosta, Layout.TotalSpentOnCarOffsetForSlot(4));
                uint junk4u = BitConverter.ToUInt32(usman, Layout.TotalSpentOnCarOffsetForSlot(4));
                if (junk4 != 2520688044u || junk4u != 2520688044u)
                {
                    Console.WriteLine($"FAIL: sample spent slot4 junk mosta={junk4} usman={junk4u}");
                    return 14;
                }
                if (CareerStatsService.GetUnknownNearBank(mosta) != 0
                    || CareerStatsService.GetUnknownNearBank(usman) != 1)
                {
                    Console.WriteLine($"FAIL: unknown_near_bank mosta={CareerStatsService.GetUnknownNearBank(mosta)} usman={CareerStatsService.GetUnknownNearBank(usman)}");
                    return 14;
                }
                if (Math.Abs(CareerStatsService.GetStageTotalReputation(mosta, 1) - 800f) > 0.01f
                    || Math.Abs(CareerStatsService.GetStageTotalReputation(usman, 1) - 1500f) > 0.01f)
                {
                    Console.WriteLine("FAIL: sample stage1 rep");
                    return 14;
                }
                Console.WriteLine("Stats sample verify OK (Mosta/Usman entered+spent0..3+slot4junk+A173)");
            }
            else
            {
                Console.WriteLine("Stats sample verify SKIP (Desktop/Mosta/Mosta or Desktop/Usman missing)");
            }

            // Undo/redo history
            var hist = new StatsEditHistory();
            float beforeRep = CareerStatsService.GetStageTotalReputation(statsBuf, 5);
            hist.Apply(statsBuf, "test rep", () => CareerStatsService.SetStageTotalReputation(statsBuf, 5, 1234f));
            if (Math.Abs(CareerStatsService.GetStageTotalReputation(statsBuf, 5) - 1234f) > 0.01f || !hist.CanUndo)
            {
                Console.WriteLine("FAIL: history apply");
                return 15;
            }
            hist.Undo(statsBuf);
            if (Math.Abs(CareerStatsService.GetStageTotalReputation(statsBuf, 5) - beforeRep) > 0.01f || !hist.CanRedo)
            {
                Console.WriteLine("FAIL: history undo");
                return 15;
            }
            hist.Redo(statsBuf);
            if (Math.Abs(CareerStatsService.GetStageTotalReputation(statsBuf, 5) - 1234f) > 0.01f
                || !Checksum.CareerChecksumsMatch(statsBuf))
            {
                Console.WriteLine("FAIL: history redo/CRC");
                return 15;
            }
            hist.Undo(statsBuf);
            Console.WriteLine("Stats undo/redo OK");

            // Raw write
            int spentOff = Layout.TotalSpentOnCarOffset;
            int spentBefore = BitConverter.ToInt32(statsBuf, spentOff);
            var raw = CareerStatsService.RawWrite(statsBuf, spentOff, CareerStatsService.RawValueKind.Int32, "424242");
            if (!raw.ChecksumsOk || BitConverter.ToInt32(statsBuf, spentOff) != 424242)
            {
                Console.WriteLine("FAIL: raw write " + raw.Summary);
                return 16;
            }
            CareerStatsService.RawWrite(statsBuf, spentOff, CareerStatsService.RawValueKind.Int32, spentBefore.ToString());
            if (BitConverter.ToInt32(statsBuf, spentOff) != spentBefore || !Checksum.CareerChecksumsMatch(statsBuf))
            {
                Console.WriteLine("FAIL: raw restore");
                return 16;
            }
            Console.WriteLine("Stats raw-write OK");

            // Wizard
            int moneyBefore = CareerStatsService.ReadMoney(statsBuf);
            byte[] wizSnap = CareerStatsService.Snapshot(statsBuf);
            string wiz = StatsWizardPresets.Apply(statsBuf, new[]
            {
                new StatsWizardAction { Id = "bank", Enabled = true, Target = "777777" },
                new StatsWizardAction { Id = "rep_s5", Enabled = true, Target = "15000" },
            });
            if (CareerStatsService.ReadMoney(statsBuf) != 777777
                || Math.Abs(CareerStatsService.GetStageTotalReputation(statsBuf, 5) - 15000f) > 0.01f
                || !Checksum.CareerChecksumsMatch(statsBuf))
            {
                Console.WriteLine("FAIL: wizard apply " + wiz);
                return 17;
            }
            // Grouped Max-money target (preset style) must land on Layout.MaxMoney
            string wizMax = StatsWizardPresets.Apply(statsBuf, new[]
            {
                new StatsWizardAction { Id = "career_jane", Enabled = true },
                new StatsWizardAction { Id = "bank", Enabled = true, Target = "999,999,999" },
            });
            if (CareerStatsService.ReadMoney(statsBuf) != Layout.MaxMoney || !Checksum.CareerChecksumsMatch(statsBuf))
            {
                Console.WriteLine($"FAIL: wizard max bank after career_jane money={CareerStatsService.ReadMoney(statsBuf)} {wizMax}");
                return 17;
            }
            Buffer.BlockCopy(wizSnap, 0, statsBuf, 0, statsBuf.Length);
            if (CareerStatsService.ReadMoney(statsBuf) != moneyBefore)
            {
                Console.WriteLine("FAIL: wizard restore");
                return 17;
            }
            Console.WriteLine("Stats wizard OK");
        }

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
