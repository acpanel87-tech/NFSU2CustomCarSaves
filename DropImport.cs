using System.IO.Compression;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace NFSU2CustomCarSaves;

public enum DropFileKind
{
    Unknown,
    Archive,
    Folder,
    Career,
    Garage,
}

/// <summary>Resultaat van een file-drop: openen als sessie, of garage plakken op huidige sessie.</summary>
public sealed class DropResolveResult
{
    public string? OpenPath { get; init; }
    /// <summary>Alleen GARAGE.DAT gedropt terwijl er geen sibling career is — plak op open sessie.</summary>
    public string? AttachGaragePath { get; init; }
}

/// <summary>Drag-drop van zip/rar/map of losse career + GARAGE.DAT → pad voor session.</summary>
public static class DropImport
{
    public static bool LooksLikeDrop(string[] paths)
    {
        if (paths == null || paths.Length == 0) return false;
        foreach (var p in paths)
        {
            if (string.IsNullOrWhiteSpace(p)) continue;
            try
            {
                if (Directory.Exists(p)) return true;
                if (!File.Exists(p)) continue;
                var kind = Classify(p);
                if (kind is DropFileKind.Archive or DropFileKind.Career or DropFileKind.Garage)
                    return true;
            }
            catch
            {
                /* skip locked/inaccessible */
            }
        }
        return false;
    }

    /// <summary>
    /// Zet dropped paden om naar een map/archive om te openen, of een garage om te attach'en.
    /// </summary>
    public static DropResolveResult Resolve(string[] paths)
    {
        if (paths == null || paths.Length == 0)
            throw new InvalidOperationException("No files in the drop.");

        // Flatten: als iemand 1 map + files mixt, map wint als enige directory
        var files = new List<string>();
        var dirs = new List<string>();
        foreach (var raw in paths)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            string p = raw.Trim().Trim('"');
            if (Directory.Exists(p)) dirs.Add(p);
            else if (File.Exists(p)) files.Add(p);
        }

        if (dirs.Count == 1 && files.Count == 0)
            return new DropResolveResult { OpenPath = dirs[0] };
        if (dirs.Count > 0 && files.Count == 0)
            return new DropResolveResult { OpenPath = dirs[0] };

        if (files.Count == 1 && dirs.Count == 0)
            return ResolveSingleFile(files[0]);

        // Meerdere files (± dirs negeren): career + garage zoeken
        string? career = null;
        string? garage = null;
        foreach (var p in files)
        {
            switch (Classify(p))
            {
                case DropFileKind.Career:
                    career ??= p;
                    break;
                case DropFileKind.Garage:
                    garage ??= p;
                    break;
                case DropFileKind.Archive when files.Count == 1:
                    return new DropResolveResult { OpenPath = p };
            }
        }

        if (career == null && garage != null)
        {
            career = FindSiblingCareer(Path.GetDirectoryName(garage)!);
            if (career == null)
                return new DropResolveResult { AttachGaragePath = garage };
        }

        if (career == null)
            throw new InvalidOperationException(
                "No career save found. Drop the save (no extension, ~54966 bytes), GARAGE.DAT, or a zip/folder.");

        garage ??= FindSiblingGarage(Path.GetDirectoryName(career)!);
        return new DropResolveResult { OpenPath = StagePair(career, garage) };
    }

    /// <summary>Legacy helper — geeft altijd een open-pad (attach wordt error).</summary>
    public static string ResolveToOpenPath(string[] paths)
    {
        var r = Resolve(paths);
        if (!string.IsNullOrEmpty(r.OpenPath)) return r.OpenPath!;
        throw new InvalidOperationException(
            "Only GARAGE.DAT dropped — open the career save first, or drop both together.");
    }

    private static DropResolveResult ResolveSingleFile(string p)
    {
        switch (Classify(p))
        {
            case DropFileKind.Folder:
                return new DropResolveResult { OpenPath = p };
            case DropFileKind.Archive:
                return new DropResolveResult { OpenPath = p };
            case DropFileKind.Career:
            {
                string? garage = FindSiblingGarage(Path.GetDirectoryName(p)!);
                return new DropResolveResult { OpenPath = StagePair(p, garage) };
            }
            case DropFileKind.Garage:
            {
                string? career = FindSiblingCareer(Path.GetDirectoryName(p)!);
                if (career != null)
                    return new DropResolveResult { OpenPath = StagePair(career, p) };
                return new DropResolveResult { AttachGaragePath = p };
            }
            default:
                throw new InvalidOperationException(
                    "Unknown file. Drop zip/rar, folder, career save (20CM), or GARAGE.DAT.");
        }
    }

    private static string StagePair(string careerPath, string? garagePath)
    {
        string dir = MakeStage();
        string careerName = Path.GetFileName(careerPath);
        if (string.IsNullOrEmpty(careerName)) careerName = "CAREER";
        File.Copy(careerPath, Path.Combine(dir, careerName), true);
        if (garagePath != null && File.Exists(garagePath))
            File.Copy(garagePath, Path.Combine(dir, "GARAGE.DAT"), true);
        return dir;
    }

    public static DropFileKind Classify(string path)
    {
        if (Directory.Exists(path)) return DropFileKind.Folder;
        if (!File.Exists(path)) return DropFileKind.Unknown;

        string ext = Path.GetExtension(path);
        if (ext.Equals(".zip", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".rar", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".7z", StringComparison.OrdinalIgnoreCase))
            return DropFileKind.Archive;

        long len;
        try { len = new FileInfo(path).Length; }
        catch { return DropFileKind.Unknown; }

        string name = Path.GetFileName(path);
        bool garageName = name.Equals("GARAGE.DAT", StringComparison.OrdinalIgnoreCase)
                          || name.Equals("GARAGE", StringComparison.OrdinalIgnoreCase);

        // Snelle size-hints + magic peek
        if (len == Layout.GarageSize || garageName)
        {
            if (PeekMagic(path, Layout.GarageMagic) || (garageName && len == Layout.GarageSize))
                return DropFileKind.Garage;
            if (PeekMagic(path, Layout.GarageMagic))
                return DropFileKind.Garage;
        }

        if (len == Layout.ProfileSize)
        {
            if (PeekMagic(path, Layout.CareerMagic))
                return DropFileKind.Career;
        }

        // Naam GARAGE.DAT met afwijkende size: toch als garage proberen via volledige check
        if (garageName)
        {
            try
            {
                var data = File.ReadAllBytes(path);
                if (GarageDatService.LooksLikeGarage(data)) return DropFileKind.Garage;
            }
            catch { /* ignore */ }
        }

        // Extensieloos / .dat: volledige magic als size klopt ongeveer
        if (string.IsNullOrEmpty(ext) || ext.Equals(".dat", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".save", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                if (len == Layout.ProfileSize && PeekMagic(path, Layout.CareerMagic))
                    return DropFileKind.Career;
                if (len == Layout.GarageSize && PeekMagic(path, Layout.GarageMagic))
                    return DropFileKind.Garage;
            }
            catch { /* ignore */ }
        }

        return DropFileKind.Unknown;
    }

    private static bool PeekMagic(string path, byte[] magic)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (fs.Length < magic.Length) return false;
            Span<byte> buf = stackalloc byte[magic.Length];
            int n = fs.Read(buf);
            if (n < magic.Length) return false;
            for (int i = 0; i < magic.Length; i++)
                if (buf[i] != magic[i]) return false;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static string? FindSiblingGarage(string directory)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return null;

        foreach (var candidate in new[]
                 {
                     Path.Combine(directory, "GARAGE.DAT"),
                     Path.Combine(directory, "garage.dat"),
                     Path.Combine(directory, "Garage.dat"),
                 })
        {
            if (File.Exists(candidate) && Classify(candidate) == DropFileKind.Garage)
                return candidate;
        }

        foreach (var file in Directory.GetFiles(directory))
        {
            if (Classify(file) == DropFileKind.Garage)
                return file;
        }
        return null;
    }

    public static string? FindSiblingCareer(string directory)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return null;

        foreach (var file in Directory.GetFiles(directory))
        {
            if (Classify(file) == DropFileKind.Career)
                return file;
        }
        return null;
    }

    public static void ExtractArchive(string archivePath, string destDir)
    {
        Directory.CreateDirectory(destDir);
        string ext = Path.GetExtension(archivePath);
        if (ext.Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            ZipFile.ExtractToDirectory(archivePath, destDir, overwriteFiles: true);
            return;
        }

        using var archive = ArchiveFactory.Open(archivePath);
        foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
        {
            entry.WriteToDirectory(destDir, new ExtractionOptions
            {
                ExtractFullPath = true,
                Overwrite = true,
            });
        }
    }

    private static string MakeStage()
    {
        string dir = Path.Combine(Path.GetTempPath(), "NFSU2CustomCarSaves", "drop_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
