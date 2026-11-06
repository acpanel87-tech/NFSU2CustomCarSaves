using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace NFSU2CustomCarSaves;

/// <summary>Opent platte map (BARKIE+GARAGE.DAT), profielmap, of zip-backup.</summary>
public sealed class BrowserSaveSession : IDisposable
{
    public string SourcePath { get; }
    public bool SourceIsZip { get; }
    public string WorkRoot { get; }
    public string CareerPath { get; private set; } = "";
    public string? GaragePath { get; private set; }
    public string ProfileName { get; private set; } = "";
    public GarageState GarageState { get; private set; } = GarageState.Missing;
    public string GarageNote { get; private set; } = "";
    public byte[] Working { get; private set; } = Array.Empty<byte>();
    public byte[] Career { get; private set; } = Array.Empty<byte>();
    public byte[]? GarageOriginal { get; private set; }
    public bool Dirty { get; set; }

    private BrowserSaveSession(string source, bool isZip, string work)
    {
        SourcePath = source;
        SourceIsZip = isZip;
        WorkRoot = work;
    }

    public static BrowserSaveSession Open(string zipOrFolder)
    {
        string work = Path.Combine(Path.GetTempPath(), "NFSU2CustomCarSaves", "ws_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            bool isArchive = File.Exists(zipOrFolder) && IsArchive(zipOrFolder);
            if (isArchive)
                DropImport.ExtractArchive(zipOrFolder, work);
            else if (Directory.Exists(zipOrFolder))
                CopyDir(zipOrFolder, work);
            else
                throw new FileNotFoundException("Folder or zip/rar not found.", zipOrFolder);

            var session = new BrowserSaveSession(Path.GetFullPath(zipOrFolder), isArchive, work);
            session.LocateAndLoad();
            return session;
        }
        catch
        {
            TryDelete(work);
            throw;
        }
    }

    /// <summary>
    /// Yellow Start → empty mod-tool base:
    /// Jane coherent career (Rachel already cleared → freeroam, no intro restart),
    /// starter car / all garage + Customize cars wiped, starter cash, empty unlocked slots.
    /// Does <b>not</b> run 100% SaveMaxOut — use 100% Save / Stats wizard for full unlocks.
    /// </summary>
    public static BrowserSaveSession CreateFreshDefault(string profileName = "Player")
    {
        profileName = profileName.Trim();
        if (!IsValidProfileName(profileName))
            throw new InvalidOperationException($"Name must be 1–{Layout.NameLength} characters (no special path characters).");

        byte[] jane = LoadJaneCareer();
        string work = Path.Combine(Path.GetTempPath(), "NFSU2CustomCarSaves", "fresh_" + Guid.NewGuid().ToString("N"));
        string profileDir = Path.Combine(work, "NFS Underground 2", profileName);
        Directory.CreateDirectory(profileDir);
        try
        {
            string careerPath = Path.Combine(profileDir, profileName);
            File.WriteAllBytes(careerPath, jane);
            byte[] garage = GarageDatService.CreateEmptyGarage(jane);
            File.WriteAllBytes(Path.Combine(profileDir, "GARAGE.DAT"), garage);
            File.WriteAllText(Path.Combine(work, "registry.txt"), "NotFirstTime=1\r\n", Encoding.ASCII);
            File.WriteAllText(Path.Combine(work, "settings.ini"), BuildDefaultSettingsIni(profileName),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            var session = new BrowserSaveSession(work, isZip: false, work);
            session.LocateAndLoad();
            session.EnsureSlots10();
            // Wipe Jane’s starter / menu cars — user picks cars via Add car afterwards.
            GarageDatService.ClearAllSlots(session.Working);
            MyCarsService.ClearAll(session.Working);
            session.WriteInGameName(profileName);
            if (!string.Equals(session.ProfileName, profileName, StringComparison.Ordinal))
                session.Rename(profileName);

            PreparePostRachelEmptyBase(session.Working);

            // Clear again after unlock helpers (never leave a residual car).
            GarageDatService.ClearAllSlots(session.Working);
            MyCarsService.ClearAll(session.Working);
            CareerStatsService.EnsureCareerChecksums(session.Working);
            session.Dirty = true;
            return session;
        }
        catch
        {
            TryDelete(work);
            throw;
        }
    }

    /// <summary>
    /// Post-Rachel freeroam sandbox: career started, completion off, starter money,
    /// editable Stats cleared to 0, empty slots unlocked.
    /// Keeps Jane career blobs (safe freeroam; no Rachel re-trigger).
    /// </summary>
    public static void PreparePostRachelEmptyBase(byte[] working)
    {
        if (working.Length < Layout.ProfileSize)
            throw new InvalidOperationException("Career buffer too small for fresh Start base.");

        CareerStatsService.SetCareerStarted(working, true);
        CareerStatsService.SetCareerComplete(working, false);
        PlayerOptionsService.SetMoney(working, Layout.FreshStartMoney);

        // Proven/probable Stats editors → 0 (clean mod-tool baseline).
        CareerStatsService.SetRacesEntered(working, 0, extended: false);
        for (int slot = 0; slot < Layout.TotalSpentEditableSlotCount; slot++)
            CareerStatsService.SetTotalSpentOnCar(working, 0, slot);
        if (CareerStatsService.BufferHasPackedStages(working))
        {
            for (int stage = Layout.PackedStageMinIndex; stage <= Layout.PackedStageMaxIndex; stage++)
            {
                CareerStatsService.SetStageTotalReputation(working, stage, 0f);
                CareerStatsService.SetStageRepRaceCount(working, stage, 0);
            }
        }

        int slotCount = working.Length >= Layout.ExtraSlotRecordBase + Layout.CareerSlotCount * Layout.SlotStride
            ? Layout.MaxSlotCount
            : Layout.CareerSlotCount;
        for (int i = 0; i < slotCount; i++)
            GarageDatService.UnlockSlot(working, i);
        GarageDatService.UpdateGarageCarCount(working);
        CareerStatsService.EnsureCareerChecksums(working);
    }

    private static byte[] LoadJaneCareer()
    {
        foreach (string path in JaneCareerCandidates())
        {
            if (!File.Exists(path)) continue;
            byte[] data = File.ReadAllBytes(path);
            if (data.Length == Layout.ProfileSize && GarageDatService.LooksLikeCareer(data))
                return data;
        }
        throw new FileNotFoundException(
            "Jane template missing (Stock/Templates/JANE). Reinstall or copy Jane into Stock/Templates.");
    }

    private static IEnumerable<string> JaneCareerCandidates() => AppPaths.JaneCareerCandidates();

    private static bool IsArchive(string path)
    {
        string ext = Path.GetExtension(path);
        return ext.Equals(".zip", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".rar", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".7z", StringComparison.OrdinalIgnoreCase);
    }

    private void LocateAndLoad()
    {
        var hit = FindCareer(WorkRoot)
                  ?? throw new InvalidDataException("No 20CM career save found in this folder/zip.");
        CareerPath = hit.Path;
        ProfileName = hit.Name;
        string? gBeside = Path.Combine(Path.GetDirectoryName(CareerPath)!, "GARAGE.DAT");
        GaragePath = File.Exists(gBeside) ? gBeside : null;

        Career = File.ReadAllBytes(CareerPath);
        GarageOriginal = GaragePath != null ? File.ReadAllBytes(GaragePath) : null;
        var check = GarageDatService.Check(GarageOriginal, Career);
        GarageState = check.State;
        GarageNote = check.Note;

        // Stale fingerprint: browser negeert 6–10, maar in de editor wél laden zodat paste/export kan herstellen
        if (GarageOriginal != null &&
            (check.State == GarageState.Loaded || check.State == GarageState.Stale))
        {
            Working = GarageDatService.Merge(Career, GarageOriginal, GarageState.Loaded);
            GarageDatService.UnlockAllSponsorSlots(Working);
            if (check.State == GarageState.Stale)
                GarageNote = check.Note + " — editor still loads 6–10; Save zip refreshes fingerprint.";
        }
        else
        {
            Working = GarageDatService.Merge(Career, GarageOriginal, check.State);
        }
    }

    private static (string Name, string Path)? FindCareer(string root)
    {
        // Flat: root/BARKIE (+ GARAGE.DAT)
        foreach (var file in Directory.GetFiles(root))
        {
            if (Path.GetFileName(file).Equals("GARAGE.DAT", StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                var data = File.ReadAllBytes(file);
                if (GarageDatService.LooksLikeCareer(data))
                    return (Path.GetFileName(file), file);
            }
            catch { /* skip */ }
        }

        // Nested: .../NFS Underground 2/Name/Name
        foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
        {
            string name = Path.GetFileName(dir);
            string file = Path.Combine(dir, name);
            if (!File.Exists(file)) continue;
            try
            {
                var data = File.ReadAllBytes(file);
                if (GarageDatService.LooksLikeCareer(data))
                    return (name, file);
            }
            catch { /* skip */ }
        }
        return null;
    }

    /// <summary>
    /// Plakt een losse GARAGE.DAT op de huidige sessie (slots 6–10).
    /// Fingerprint wordt bij Save opnieuw gezet.
    /// </summary>
    public void AttachGarage(string garageFilePath)
    {
        if (!File.Exists(garageFilePath))
            throw new FileNotFoundException("GARAGE.DAT not found.", garageFilePath);

        byte[] g = File.ReadAllBytes(garageFilePath);
        if (!GarageDatService.LooksLikeGarage(g))
            throw new InvalidDataException("This is not a valid nfsu2 GARAGE.DAT (N2GA, 10202 bytes).");

        // Career-bytes uit werkbuffer (eventuele edits behouden)
        var careerNow = new byte[Layout.ProfileSize];
        Buffer.BlockCopy(Working, 0, careerNow, 0, Layout.ProfileSize);
        Career = careerNow;
        File.WriteAllBytes(CareerPath, Career);

        string dest = Path.Combine(Path.GetDirectoryName(CareerPath)!, "GARAGE.DAT");
        File.Copy(garageFilePath, dest, overwrite: true);
        GaragePath = dest;
        GarageOriginal = File.ReadAllBytes(dest);

        var check = GarageDatService.Check(GarageOriginal, Career);
        GarageState = check.State == GarageState.Missing ? GarageState.Loaded : check.State;
        GarageNote = check.State == GarageState.Stale
            ? "GARAGE.DAT attached (fingerprint stale — OK after Save zip)"
            : "GARAGE.DAT attached — slots 1–10 available.";

        // In de editor altijd mergen zodat slots 6–10 zichtbaar zijn
        Working = GarageDatService.Merge(Career, GarageOriginal, GarageState.Loaded);
        Dirty = true;
    }

    public List<SlotInfo> Slots() => GarageDatService.ReadSlots(Working);

    public string ReadInGameName()
    {
        if (Working.Length < Layout.NameOffset + Layout.NameLength)
            return ProfileName;
        int end = Layout.NameOffset;
        int lim = Layout.NameOffset + Layout.NameLength;
        while (end < lim && Working[end] != 0) end++;
        string n = Encoding.ASCII.GetString(Working, Layout.NameOffset, end - Layout.NameOffset).Trim();
        return string.IsNullOrEmpty(n) ? ProfileName : n;
    }

    public static bool IsValidProfileName(string name) =>
        name.Length is >= 1 and <= Layout.NameLength
        && name.All(c => c is >= ' ' and <= '~')
        && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    /// <summary>Hernoemt in-game naam + career-bestand (+ map als die dezelfde naam heeft).</summary>
    public void Rename(string newName)
    {
        newName = newName.Trim();
        if (!IsValidProfileName(newName))
            throw new InvalidOperationException($"Name must be 1–{Layout.NameLength} characters (no special path characters).");

        string oldName = ProfileName;
        WriteInGameName(newName);

        string oldCareer = CareerPath;
        string oldDir = Path.GetDirectoryName(oldCareer)!;
        string newCareer = Path.Combine(oldDir, newName);
        if (!string.Equals(oldCareer, newCareer, StringComparison.OrdinalIgnoreCase))
        {
            if (File.Exists(newCareer))
                throw new InvalidOperationException($"File '{newName}' already exists.");
            File.Move(oldCareer, newCareer);
            CareerPath = newCareer;
        }

        // Standaard game-map: .../Name/Name → ook map hernoemen
        if (string.Equals(Path.GetFileName(oldDir), oldName, StringComparison.OrdinalIgnoreCase))
        {
            string parent = Path.GetDirectoryName(oldDir)!;
            string newDir = Path.Combine(parent, newName);
            if (!string.Equals(oldDir, newDir, StringComparison.OrdinalIgnoreCase))
            {
                if (Directory.Exists(newDir))
                    throw new InvalidOperationException($"Folder '{newName}' already exists.");
                Directory.Move(oldDir, newDir);
                CareerPath = Path.Combine(newDir, newName);
                if (GaragePath != null)
                    GaragePath = Path.Combine(newDir, "GARAGE.DAT");
            }
        }

        ProfileName = newName;
        Dirty = true;
    }

    private void WriteInGameName(string name)
    {
        var bytes = new byte[Layout.NameLength];
        Encoding.ASCII.GetBytes(name.AsSpan(0, Math.Min(name.Length, Layout.NameLength)), bytes);
        Buffer.BlockCopy(bytes, 0, Working, Layout.NameOffset, Layout.NameLength);
        if (Career.Length >= Layout.NameOffset + Layout.NameLength)
            Buffer.BlockCopy(bytes, 0, Career, Layout.NameOffset, Layout.NameLength);
    }

    /// <summary>
    /// Zip-naam à la nfsu2-saves-2026-11-05_113306.zip.
    /// Laatste cijfers komen uit de geïmporteerde zip-naam als die er zijn.
    /// </summary>
    public string SuggestZipFileName()
    {
        string digits = ExtractSourceStampDigits() ?? DateTime.Now.ToString("HHmmss");
        string day = DateTime.Now.ToString("yyyy-MM-dd");
        return $"nfsu2-saves-{day}_{digits}.zip";
    }

    /// <summary>Haalt trailing digits uit bronzip (bv. …_113306 → 113306).</summary>
    private string? ExtractSourceStampDigits()
    {
        if (!SourceIsZip || string.IsNullOrWhiteSpace(SourcePath)) return null;
        string stem = Path.GetFileNameWithoutExtension(SourcePath);
        var m = Regex.Match(stem, @"_(\d{4,})$");
        return m.Success ? m.Groups[1].Value : null;
    }

    private static string SanitizeFile(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        name = name.Trim();
        return string.IsNullOrEmpty(name) ? "" : name;
    }

    public void ImportPackage(CarSlotPackage pkg, int slotIndex)
    {
        EnsureSlots10();
        var (tuned, inv) = CarSlotIo.Decode(pkg);
        GarageDatService.WriteRecord(Working, slotIndex, tuned, inv);
        Dirty = true;
    }

    /// <summary>Add or replace slot with a stock in-game donor (hackch car-donors.json).</summary>
    public void PlaceDonorCar(int slotIndex, CarDonor donor)
    {
        EnsureSlots10IfNeeded(slotIndex);
        if (slotIndex >= Layout.CareerSlotCount) EnsureSlots10();
        CarDonorService.PlaceInSlot(Working, slotIndex, donor);
        Dirty = true;
    }

    public void CopyFromCareerFile(string otherCareerPath, int srcSlot, int dstSlot)
    {
        byte[] other = File.ReadAllBytes(otherCareerPath);
        if (!GarageDatService.LooksLikeCareer(other) && other.Length < Layout.CareerTunedBase + Layout.SlotStride)
            throw new InvalidDataException("Source file is not a valid career save.");

        // Allow reading first 5 from normal career; if extended buffer file, use ExtendedRecordOffset
        byte[] srcBuf = other;
        string? otherGarage = Path.Combine(Path.GetDirectoryName(otherCareerPath)!, "GARAGE.DAT");
        if (File.Exists(otherGarage))
        {
            var g = File.ReadAllBytes(otherGarage);
            var st = GarageDatService.Check(g, other);
            if (st.State == GarageState.Loaded)
                srcBuf = GarageDatService.Merge(other, g, GarageState.Loaded);
        }

        if (srcSlot < 0 || srcSlot >= (srcBuf.Length > Layout.ProfileSize ? Layout.MaxSlotCount : Layout.CareerSlotCount))
            throw new ArgumentOutOfRangeException(nameof(srcSlot));

        GarageDatService.ReadRecord(srcBuf, srcSlot, out var tuned, out var inv);
        uint hash = BitConverter.ToUInt32(tuned, Layout.CarHashInTuned);
        if (tuned[Layout.BoughtInTuned] != 1 || hash == 0)
            throw new InvalidOperationException("Source slot is empty.");

        EnsureSlots10();
        GarageDatService.WriteRecord(Working, dstSlot, tuned, inv);
        Dirty = true;
    }

    public CarSlotPackage ExportPackage(int slotIndex, string profileName)
    {
        GarageDatService.ReadRecord(Working, slotIndex, out var tuned, out var inv);
        var info = GarageDatService.ReadSlot(Working, slotIndex);
        if (!info.IsOccupied) throw new InvalidOperationException("Slot is empty.");
        string save = string.IsNullOrWhiteSpace(profileName) ? "save" : profileName.Trim();
        string car = info.CarHash != 0 ? CarNames.Display(info.CarHash) : info.DisplayName;
        return new CarSlotPackage
        {
            CarName = car,
            XName = info.XName,
            CarHash = info.CarHash,
            SourceProfile = $"{save} · Career {slotIndex + 1}",
            SourceArea = "Career",
            TunedHex = Convert.ToHexString(tuned),
            InventoryHex = Convert.ToHexString(inv),
        };
    }

    public void Unlock(int slotIndex)
    {
        EnsureSlots10IfNeeded(slotIndex);
        GarageDatService.UnlockSlot(Working, slotIndex);
        Dirty = true;
    }

    /// <summary>100% free-play like Jane: shop/career unlocks only — garage cars untouched.</summary>
    public string MaxOut()
    {
        EnsureSlots10();
        string report = SaveMaxOut.Apply(Working);
        Dirty = true;
        return report;
    }

    public CarTuneStats ReadTune(int slotIndex) => CarTuneService.Read(Working, slotIndex);

    public void OpenTuneEditor(int slotIndex, IWin32Window owner, Action onChanged, bool menuMyCars = false)
    {
        if (!menuMyCars) EnsureSlots10IfNeeded(slotIndex);
        using var f = new CarTuneForm(Working, slotIndex, () =>
        {
            Dirty = true;
            onChanged();
        }, ProfileName, menuMyCars);
        f.ShowDialog(owner);
    }

    public CarSlotPackage ExportMyCarPackage(int index) =>
        MyCarsService.ExportPackage(Working, index, ProfileName);

    public void ImportMyCarPackage(CarSlotPackage pkg, int index)
    {
        MyCarsService.ImportPackage(Working, index, pkg);
        Dirty = true;
    }

    public void PlaceMyCarDonor(int index, CarDonor donor)
    {
        MyCarsService.PlaceDonor(Working, index, donor);
        Dirty = true;
    }

    /// <summary>Ensure Career slots 6–10 (GARAGE.DAT body) exist in the working buffer.</summary>
    public void EnsureMyCarsGarage() => EnsureSlots10();

    private void EnsureSlots10()
    {
        if (Working.Length >= Layout.ExtraSlotRecordBase + Layout.CareerSlotCount * Layout.SlotStride)
        {
            GarageDatService.UnlockAllSponsorSlots(Working);
            return;
        }

        // Career-edits uit Working behouden (niet terugvallen op oude Career[])
        var careerNow = new byte[Layout.ProfileSize];
        Buffer.BlockCopy(Working, 0, careerNow, 0, Layout.ProfileSize);
        Career = careerNow;

        var g = GarageOriginal ?? GarageDatService.CreateEmptyGarage(Career);
        if (!GarageDatService.LooksLikeGarage(g))
            g = GarageDatService.CreateEmptyGarage(Career);

        Working = GarageDatService.Merge(Career, g, GarageState.Loaded);
        GarageDatService.UnlockAllSponsorSlots(Working);
        GarageOriginal = g;
        GaragePath ??= Path.Combine(Path.GetDirectoryName(CareerPath)!, "GARAGE.DAT");
        GarageState = GarageState.Loaded;
        GarageNote = "GARAGE.DAT ready for slots 6–10.";
    }

    private void EnsureSlots10IfNeeded(int slotIndex)
    {
        if (slotIndex >= Layout.CareerSlotCount) EnsureSlots10();
    }

    public string Save(bool backup)
    {
        FlushToWorkFiles(backup);
        if (SourceIsZip)
        {
            return Path.Combine(
                Path.GetDirectoryName(SourcePath) ?? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                SuggestZipFileName());
        }
        return CareerPath;
    }

    /// <summary>
    /// Zip voor nfsu2.xyz Restore: <c>NFS Underground 2/Name/Name</c> + <c>GARAGE.DAT</c>
    /// plus <c>registry.txt</c> + default <c>settings.ini</c>.
    /// Zonder registry/settings kiest de site “Add profile” en laat GARAGE.DAT weg → slots 6–10 verdwijnen.
    /// Met registry/settings → “Restore backup”, dan blijft GARAGE.DAT behouden.
    /// </summary>
    public void ExportZip(string destZip)
    {
        // Altijd extended garage met verse fingerprint na career-CRC
        EnsureSlots10();
        FlushToWorkFiles(backup: false);

        var check = GarageDatService.Check(GarageOriginal, Career);
        if (check.State != GarageState.Loaded)
            throw new InvalidOperationException("GARAGE.DAT fingerprint mismatch after save — slots 6–10 would not load on nfsu2.xyz. " + check.Note);

        if (File.Exists(destZip)) File.Delete(destZip);
        string? dir = Path.GetDirectoryName(destZip);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        string stage = Path.Combine(Path.GetTempPath(), "NFSU2CustomCarSaves", "zip_" + Guid.NewGuid().ToString("N"));
        string profileDir = Path.Combine(stage, "NFS Underground 2", ProfileName);
        Directory.CreateDirectory(profileDir);
        try
        {
            File.Copy(CareerPath, Path.Combine(profileDir, ProfileName), true);
            string gSrc = GaragePath ?? throw new InvalidOperationException("No GARAGE.DAT to export.");
            File.Copy(gSrc, Path.Combine(profileDir, "GARAGE.DAT"), true);
            WriteBackupMarkers(stage);
            ZipFile.CreateFromDirectory(stage, destZip, CompressionLevel.Optimal, includeBaseDirectory: false);
        }
        finally
        {
            TryDelete(stage);
        }
    }

    /// <summary>
    /// nfsu2.xyz readBackup: registry.txt of settings.ini → backupIn (houdt GARAGE.DAT).
    /// Zonder die markers → profilesIn (slaat GARAGE.DAT over als “other file”).
    /// Altijd onze default settings.ini (roam_name = huidig profiel).
    /// </summary>
    private void WriteBackupMarkers(string stageRoot)
    {
        string? srcReg = FindBesideWork("registry.txt");
        string destReg = Path.Combine(stageRoot, "registry.txt");
        string destIni = Path.Combine(stageRoot, "settings.ini");

        if (srcReg != null)
            File.Copy(srcReg, destReg, true);
        else
            File.WriteAllText(destReg, "NotFirstTime=1\r\n", Encoding.ASCII);

        File.WriteAllText(destIni, BuildDefaultSettingsIni(ProfileName), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    /// <summary>Default browser settings; {{ROAM_NAME}} / {{ROAM_FLAG}} filled from profile + Flag picker.</summary>
    public static string BuildDefaultSettingsIni(string roamName, string? roamFlag = null)
    {
        string safe = SanitizeRoamName(roamName);
        string flag = RoamFlagPrefs.SanitizeForIni(roamFlag ?? RoamFlagPrefs.Load());
        string template = LoadSettingsTemplate();
        return template
            .Replace("{{ROAM_NAME}}", safe, StringComparison.Ordinal)
            .Replace("{{ROAM_FLAG}}", flag, StringComparison.Ordinal);
    }

    private static string SanitizeRoamName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "PLAYER";
        var sb = new StringBuilder(24);
        foreach (char c in name.Trim())
        {
            if (sb.Length >= 24) break;
            if (c >= 32 && c <= 126 && c != ';' && c != '#')
                sb.Append(c);
        }
        return sb.Length > 0 ? sb.ToString() : "PLAYER";
    }

    private static string LoadSettingsTemplate()
    {
        foreach (string path in SettingsTemplateCandidates())
        {
            if (File.Exists(path))
                return File.ReadAllText(path);
        }
        // Fallback if Templates/settings.ini missing next to the exe
        return
            "# NFSU2 settings\n" +
            "movies = off\n" +
            "intro = off\n" +
            "language = english\n" +
            "subtitles = off\n" +
            "autosave = on\n" +
            "fov_scale = correct\n" +
            "other_headlights = players\n" +
            "fps = 60\n" +
            "roam_name = {{ROAM_NAME}}\n" +
            "roam_flag = {{ROAM_FLAG}}\n" +
            "horn = cucaracha\n" +
            "roam = on\n" +
            "roam_max_cars = 48\n" +
            "roam_collide = none\n";
    }

    private static IEnumerable<string> SettingsTemplateCandidates() => AppPaths.SettingsIniCandidates();

    private string? FindBesideWork(string fileName)
    {
        string direct = Path.Combine(WorkRoot, fileName);
        if (File.Exists(direct)) return direct;
        try
        {
            foreach (var hit in Directory.EnumerateFiles(WorkRoot, fileName, SearchOption.AllDirectories))
                return hit;
        }
        catch
        {
            /* ignore */
        }
        return null;
    }

    private void FlushToWorkFiles(bool backup)
    {
        if (Working.Length >= Layout.ExtraSlotRecordBase + Layout.CareerSlotCount * Layout.SlotStride)
            GarageDatService.UnlockAllSponsorSlots(Working);
        GarageDatService.UpdateGarageCarCount(Working);

        var career = new byte[Layout.ProfileSize];
        Buffer.BlockCopy(Working, 0, career, 0, Layout.ProfileSize);
        Checksum.RecalculateCareerChecksums(career);

        byte[]? garageOut = null;
        if (Working.Length >= Layout.ExtraSlotRecordBase + Layout.CareerSlotCount * Layout.SlotStride)
        {
            var original = GarageOriginal ?? GarageDatService.CreateEmptyGarage(career);
            garageOut = GarageDatService.BuildGarage(original, Working, career);
        }

        if (backup)
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string desk = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string bakDir = Path.Combine(desk, "NFSU2CustomCarSaves_Backup_" + stamp);
            Directory.CreateDirectory(bakDir);
            File.Copy(CareerPath, Path.Combine(bakDir, Path.GetFileName(CareerPath)), true);
            if (GaragePath != null && File.Exists(GaragePath))
                File.Copy(GaragePath, Path.Combine(bakDir, "GARAGE.DAT"), true);
        }

        File.WriteAllBytes(CareerPath, career);
        Career = career;

        // Browser hernoemt mismatch naar GARAGE.OLD — oude .OLD verwijdert verwarring
        string careerDir = Path.GetDirectoryName(CareerPath)!;
        foreach (var old in Directory.GetFiles(careerDir, "GARAGE.OLD"))
        {
            try { File.Delete(old); } catch { /* ignore */ }
        }

        if (garageOut != null)
        {
            string gPath = GaragePath ?? Path.Combine(careerDir, "GARAGE.DAT");
            File.WriteAllBytes(gPath, garageOut);
            GaragePath = gPath;
            GarageOriginal = garageOut;
            GarageState = GarageState.Loaded;
            GarageNote = "GARAGE.DAT updated.";
        }

        Working = GarageDatService.Merge(Career, GarageOriginal, GarageState);
        Dirty = false;
    }

    public void Dispose() => TryDelete(WorkRoot);

    private static void CopyDir(string src, string dst)
    {
        foreach (var dir in Directory.GetDirectories(src, "*", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(src, dir);
            Directory.CreateDirectory(Path.Combine(dst, rel));
        }
        foreach (var file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(src, file);
            string target = Path.Combine(dst, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }
        // also copy files in root
        Directory.CreateDirectory(dst);
    }

    private static void TryDelete(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { /* ignore */ }
    }
}
