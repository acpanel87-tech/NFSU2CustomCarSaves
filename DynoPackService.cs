namespace NFSU2CustomCarSaves;

/// <summary>
/// Dyno setups plus the upgrades that keep them stable in-game.
/// Visual bumpers/skirts/spoiler/widebody drive aero flags 56–67 (drag/drift).
/// Perf package flags 0–55 are the exact installed upgrades from the donor save.
/// </summary>
public static class DynoPackService
{
    /// <summary>Visual slots that drive group-9 aero (hackch technical.md).</summary>
    public static readonly int[] AeroVisualSlots = [1, 2, 6, 11, 12]; // F-bumper, R-bumper, widebody, skirts, spoiler

    public const int PerfBytes = Layout.PerfCount; // 68
    public const int UniqueBytes = Layout.UniquePerfCount * 4; // 36
    public const int AeroVisualBytes = 5 * 4; // 20

    public static void Capture(byte[] working, int slot, out byte[] dyno, out byte[] perf, out byte[] unique, out byte[] aeroVisual)
    {
        int editor = Layout.SlotEditorOffset(slot);
        Ensure(working, editor);

        dyno = new byte[Layout.DynoTotalSize];
        Buffer.BlockCopy(working, editor + Layout.DynoOff, dyno, 0, Layout.DynoTotalSize);

        perf = new byte[PerfBytes];
        Buffer.BlockCopy(working, editor + Layout.PerfOff, perf, 0, PerfBytes);

        unique = new byte[UniqueBytes];
        Buffer.BlockCopy(working, editor + Layout.UniquePerfOff, unique, 0, UniqueBytes);

        aeroVisual = new byte[AeroVisualBytes];
        for (int i = 0; i < AeroVisualSlots.Length; i++)
        {
            int off = editor + 0x18 + AeroVisualSlots[i] * 4;
            Buffer.BlockCopy(working, off, aeroVisual, i * 4, 4);
        }
    }

    /// <param name="includeUpgrades">Perf package + unique flags (not vinyl/livery).</param>
    /// <param name="copyBodyVisuals">
    /// Optional: overwrite bumper/spoiler/skirts/widebody hashes (same car only).
    /// Off by default — vinyl/livery layers are never touched either way.
    /// </param>
    public static string Apply(
        byte[] working,
        int slot,
        string dynoHex,
        string? perfHex,
        string? uniqueHex,
        string? aeroVisualHex,
        string? presetCarHash,
        bool includeUpgrades,
        bool copyBodyVisuals = false)
    {
        var info = GarageDatService.ReadSlot(working, slot);
        if (!info.IsOccupied)
            throw new InvalidOperationException("Target slot has no car.");

        string dHex = Norm(dynoHex);
        if (dHex.Length != Layout.DynoTotalSize * 2)
            throw new InvalidOperationException($"Invalid dyno ({dHex.Length / 2} bytes).");
        DynoService.WriteRaw(working, slot, Convert.FromHexString(dHex));

        if (!includeUpgrades && !copyBodyVisuals)
            return "dyno only (livery/body kept)";

        int editor = Layout.SlotEditorOffset(slot);
        Ensure(working, editor);
        var done = new List<string> { "dyno" };

        if (includeUpgrades)
        {
            if (!string.IsNullOrWhiteSpace(perfHex))
            {
                string p = Norm(perfHex);
                if (p.Length == PerfBytes * 2)
                {
                    Buffer.BlockCopy(Convert.FromHexString(p), 0, working, editor + Layout.PerfOff, PerfBytes);
                    done.Add("perf/aero flags");
                }
            }

            if (!string.IsNullOrWhiteSpace(uniqueHex))
            {
                string u = Norm(uniqueHex);
                if (u.Length == UniqueBytes * 2)
                {
                    Buffer.BlockCopy(Convert.FromHexString(u), 0, working, editor + Layout.UniquePerfOff, UniqueBytes);
                    done.Add("unique");
                }
            }
        }

        // Vinyl / livery / paint / neon are NEVER written here.
        // Body aero parts only if explicitly requested + same car model.
        bool sameCar = !string.IsNullOrWhiteSpace(presetCarHash) &&
                       uint.TryParse(presetCarHash, System.Globalization.NumberStyles.HexNumber, null, out uint ph) &&
                       ph == info.CarHash;

        if (copyBodyVisuals && sameCar && !string.IsNullOrWhiteSpace(aeroVisualHex))
        {
            string v = Norm(aeroVisualHex);
            if (v.Length == AeroVisualBytes * 2)
            {
                byte[] aero = Convert.FromHexString(v);
                for (int i = 0; i < AeroVisualSlots.Length; i++)
                {
                    int off = editor + 0x18 + AeroVisualSlots[i] * 4;
                    Buffer.BlockCopy(aero, i * 4, working, off, 4);
                }
                done.Add("bumper/spoiler/widebody (not vinyl)");
            }
        }
        else if (copyBodyVisuals && !sameCar)
        {
            done.Add("skipped body visuals (different car)");
        }

        done.Add("vinyl/livery untouched");
        return string.Join(" + ", done);
    }

    public static string AeroSummary(byte[] perf68)
    {
        var on = new List<int>();
        for (int i = 56; i <= 67 && i < perf68.Length; i++)
            if (perf68[i] != 0) on.Add(i);
        if (on.Count == 0) return "aero: none";
        // Widebody L3 set from hackch = 58,61,64,67
        if (on.SequenceEqual(new[] { 58, 61, 64, 67 }))
            return "aero: widebody L3";
        return "aero: [" + string.Join(",", on) + "]";
    }

    /// <summary>
    /// Aggressive / event-specific setups need the donor part ranges (and often aero).
    /// Mild midpoint-ish dynos work without forcing parts onto the target car.
    /// </summary>
    public static bool DynoNeedsParts(byte[] dyno205, byte[]? perf68 = null)
    {
        if (dyno205.Length < Layout.DynoSetupSize) return false;

        int extreme = 0; // |b-100| >= 40
        int hard = 0;    // 0, >=200, or <=5 / >=195
        for (int i = 0; i < dyno205.Length; i++)
        {
            int b = dyno205[i];
            int d = Math.Abs(b - 100);
            if (d >= 40) extreme++;
            if (b <= 5 || b >= 195) hard++;
        }

        // Stock / mild midpoint — no parts required (ignore leftover aero flags on the car)
        if (extreme < 6 && hard == 0) return false;

        // Tuned setup + donor aero → parts (incl. bumper/spoiler aero) matter
        if (perf68 != null)
        {
            for (int i = 56; i <= 67 && i < perf68.Length; i++)
                if (perf68[i] != 0) return true;
        }

        // Tuned curves / gears / NOS outside mild band → needs matching packages
        if (hard >= 3 || extreme >= 12) return true;

        // Per-event: if any single event is heavily shaped, need parts
        for (int e = 0; e < Layout.DynoSetupCount; e++)
        {
            int off = e * Layout.DynoSetupSize;
            int eExt = 0, eHard = 0;
            for (int i = 0; i < Layout.DynoSetupSize; i++)
            {
                int b = dyno205[off + i];
                if (Math.Abs(b - 100) >= 40) eExt++;
                if (b <= 5 || b >= 195) eHard++;
            }
            if (eHard >= 2 || eExt >= 8) return true;
        }

        return extreme >= 8;
    }

    public static bool DynoNeedsPartsFromHex(string? dynoHex, string? perfHex = null)
    {
        try
        {
            string d = (dynoHex ?? "").Replace(" ", "").Replace("-", "");
            if (d.Length != Layout.DynoTotalSize * 2) return false;
            byte[] dyno = Convert.FromHexString(d);
            byte[]? perf = null;
            string p = (perfHex ?? "").Replace(" ", "").Replace("-", "");
            if (p.Length == PerfBytes * 2)
                perf = Convert.FromHexString(p);
            return DynoNeedsParts(dyno, perf);
        }
        catch { return false; }
    }

    private static string Norm(string hex) =>
        hex.Replace(" ", "").Replace("-", "").ToUpperInvariant();

    private static void Ensure(byte[] working, int editor)
    {
        int need = editor + Layout.DynoOff + Layout.DynoTotalSize;
        if (need > working.Length)
            throw new InvalidOperationException("Slot outside save range.");
    }
}
