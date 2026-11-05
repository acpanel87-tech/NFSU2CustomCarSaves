namespace NFSU2CustomCarSaves;

public static class CarNames
{
    // Keys = in-save LE car-type hashes (as BitConverter.ToUInt32 reads them).
    // Also register byte-swapped aliases so older tables still resolve.
    private static readonly Dictionary<uint, string> XNames = BuildXNames();

    private static readonly Dictionary<string, string> Friendly = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PEUGOT"] = "Peugeot 206",
        ["FOCUS"] = "Ford Focus",
        ["COROLLA"] = "Toyota Corolla AE86",
        ["240SX"] = "Nissan 240SX",
        ["MIATA"] = "Mazda Miata MX-5",
        ["CIVIC"] = "Honda Civic Si",
        ["PEUGOT106"] = "Peugeot 106",
        ["CORSA"] = "Opel Corsa",
        ["HUMMER"] = "Hummer H2",
        ["NAVIGATOR"] = "Lincoln Navigator",
        ["ESCALADE"] = "Cadillac Escalade",
        ["TIBURON"] = "Hyundai Tiburon",
        ["SENTRA"] = "Nissan Sentra SE-R",
        ["CELICA"] = "Toyota Celica",
        ["IS300"] = "Lexus IS 300",
        ["SUPRA"] = "Toyota Supra",
        ["GOLF"] = "VW Golf GTI",
        ["A3"] = "Audi A3",
        ["RSX"] = "Acura RSX",
        ["ECLIPSE"] = "Mitsubishi Eclipse",
        ["TT"] = "Audi TT",
        ["RX8"] = "Mazda RX-8",
        ["350Z"] = "Nissan 350Z",
        ["G35"] = "Infiniti G35",
        ["3000GT"] = "Mitsubishi 3000GT",
        ["GTO"] = "Pontiac GTO",
        ["MUSTANGGT"] = "Ford Mustang GT",
        ["SKYLINE"] = "Nissan Skyline GT-R R34",
        ["LANCEREVO8"] = "Mitsubishi Lancer Evo VIII",
        ["RX7"] = "Mazda RX-7",
        ["IMPREZAWRX"] = "Subaru Impreza WRX",
    };

    private static Dictionary<uint, string> BuildXNames()
    {
        // Canonical LE values (game / save file order)
        var canon = new (uint Hash, string Name)[]
        {
            (0x72B2E513, "PEUGOT"),
            (0x02CA04DF, "FOCUS"),
            (0xF77C13EB, "COROLLA"),
            (0x0150FB80, "240SX"),
            (0x03455D6B, "MIATA"),
            (0x0290C04D, "CIVIC"),
            (0x4B10070A, "PEUGOT106"),
            (0x0293FAD7, "CORSA"),
            (0x6126DD4D, "HUMMER"),
            (0x0219C94A, "NAVIGATOR"),
            (0xCF704771, "ESCALADE"),
            (0x04FD0522, "TIBURON"),
            (0x79AEBFEC, "SENTRA"),
            (0x545AEC60, "CELICA"),
            (0x030236EE, "IS300"),
            (0x03B8C48A, "SUPRA"),
            (0x00163087, "GOLF"),
            (0x00000453, "A3"),
            (0x0000DB7C, "RSX"),
            (0x7502DA84, "ECLIPSE"),
            (0x000006E7, "TT"),
            (0x0000DC01, "RX8"),
            (0x000AC6D1, "350Z"),
            (0x0000A86E, "G35"),
            (0x2D7C9B5D, "3000GT"),
            (0x0000ACC9, "GTO"),
            (0x35165819, "MUSTANGGT"),
            (0xBE48375E, "SKYLINE"),
            (0x09D2EFF6, "LANCEREVO8"),
            (0x0000DC00, "RX7"),
            (0xCF4458F8, "IMPREZAWRX"),
        };

        var map = new Dictionary<uint, string>();
        void Add(uint h, string n)
        {
            map[h] = n;
            uint s = Swap(h);
            if (s != h) map[s] = n;
        }

        foreach (var (h, n) in canon) Add(h, n);

        // Legacy Profile Master / MemphisRider table (often byte-swapped vs file)
        foreach (var (h, n) in new (uint, string)[]
        {
            (0x13E5B272, "PEUGOT"), (0xDF04CA02, "FOCUS"), (0xEB137CF7, "COROLLA"),
            (0x80FB5001, "240SX"), (0x6B5D4503, "MIATA"), (0x4DC09002, "CIVIC"),
            (0x0A07104B, "PEUGOT106"), (0xD7FA9302, "CORSA"), (0x4DDD2661, "HUMMER"),
            (0x4AC91902, "NAVIGATOR"), (0x714770CF, "ESCALADE"), (0x2205FD04, "TIBURON"),
            (0xECBFAE79, "SENTRA"), (0x60EC5A54, "CELICA"), (0xEE360203, "IS300"),
            (0x8AC4B803, "SUPRA"), (0x87301600, "GOLF"), (0x53040000, "A3"),
            (0x7CDB0000, "RSX"), (0x84DA0275, "ECLIPSE"), (0xE7060000, "TT"),
            (0x01DC0000, "RX8"), (0xD1C60A00, "350Z"), (0x6EA80000, "G35"),
            (0x5D9B7C2D, "3000GT"), (0xC9AC0000, "GTO"), (0x19581635, "MUSTANGGT"),
            (0x5E3748BE, "SKYLINE"), (0xF6EFD209, "LANCEREVO8"), (0x00DC0000, "RX7"),
            (0xF85844CF, "IMPREZAWRX"),
        })
            Add(h, n);

        return map;
    }

    private static uint Swap(uint v) =>
        ((v & 0x000000FFu) << 24) | ((v & 0x0000FF00u) << 8) |
        ((v & 0x00FF0000u) >> 8) | ((v & 0xFF000000u) >> 24);

    public static bool TryResolve(uint hash, out string xname)
    {
        if (hash != 0 && XNames.TryGetValue(hash, out xname!)) return true;
        xname = "";
        return false;
    }

    public static string ResolveXName(uint hash) =>
        TryResolve(hash, out var n) ? n : $"UNK_{hash:X8}";

    /// <summary>Leesbare showroom-naam.</summary>
    public static string Display(uint hash)
    {
        if (hash == 0) return "(empty)";
        string x = ResolveXName(hash);
        if (Friendly.TryGetValue(x, out var f)) return f;
        if (x.StartsWith("UNK_", StringComparison.Ordinal)) return $"Unknown car ({hash:X8})";
        return x;
    }

    /// <summary>Korte regel voor lijsten.</summary>
    public static string ListLabel(uint hash, bool occupied, bool locked)
    {
        if (locked && !occupied) return "(locked)";
        if (!occupied) return "(empty)";
        return Display(hash);
    }

    /// <summary>Hover-details: interne id + hex.</summary>
    public static string Tooltip(uint hash, string? extra = null)
    {
        if (hash == 0) return extra ?? "(empty slot)";
        string x = ResolveXName(hash);
        string friendly = Display(hash);
        string tip = $"{friendly}\nInternal name: {x}\nCar hash: 0x{hash:X8}";
        if (!string.IsNullOrWhiteSpace(extra)) tip = extra + "\n" + tip;
        return tip;
    }
}
