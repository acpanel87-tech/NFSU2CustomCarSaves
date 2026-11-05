using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace NFSU2CustomCarSaves;

internal static class GalaxyAssets
{
    private static Image? _logo;
    private static bool _logoTried;
    private static readonly Dictionary<(int, int), Bitmap> _fit = new();

    private static readonly string[] LogoCandidates =
    {
        "ccs-logo.png",
    };

    public static string LogoPath
    {
        get
        {
            foreach (string name in LogoCandidates)
            {
                string beside = Path.Combine(AppContext.BaseDirectory, "Assets", name);
                if (File.Exists(beside)) return beside;
                string dev = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Assets", name));
                if (File.Exists(dev)) return dev;
            }
            return Path.Combine(AppContext.BaseDirectory, "Assets", LogoCandidates[0]);
        }
    }

    /// <summary>Shared logo (loaded once, no file lock). Never dispose.</summary>
    public static Image? Logo
    {
        get
        {
            if (_logoTried) return _logo;
            _logoTried = true;
            try
            {
                string path = LogoPath;
                if (File.Exists(path))
                {
                    using var fs = File.OpenRead(path);
                    using var img = Image.FromStream(fs);
                    _logo = new Bitmap(img);
                }
            }
            catch { /* painted fallback */ }
            return _logo;
        }
    }

    /// <summary>Logo pre-scaled to fit inside maxW x maxH (cached, so animation frames stay cheap).</summary>
    public static Bitmap? LogoFit(int maxW, int maxH)
    {
        var src = Logo;
        if (src == null) return null;
        if (_fit.TryGetValue((maxW, maxH), out var cached)) return cached;

        float s = Math.Min((float)maxW / src.Width, (float)maxH / src.Height);
        int w = Math.Max(1, (int)(src.Width * s));
        int h = Math.Max(1, (int)(src.Height * s));
        var bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.CompositingMode = CompositingMode.SourceOver;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);
            g.DrawImage(src, 0, 0, w, h);
        }
        _fit[(maxW, maxH)] = bmp;
        return bmp;
    }
}
