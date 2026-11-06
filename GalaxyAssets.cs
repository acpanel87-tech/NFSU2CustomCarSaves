using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace NFSU2CustomCarSaves;

internal static class GalaxyAssets
{
    private static Image? _logo;
    private static bool _logoTried;
    private static Image? _text;
    private static bool _textTried;
    private static readonly Dictionary<(int, int, string), Bitmap> _fit = new();

    private static readonly string[] LogoCandidates = { "ccs-logo.png" };
    private static readonly string[] TextCandidates = { "ccs-text.png" };
    private static readonly string[] SettingsCandidates = { "settings-icon.png" };

    private static Image? _settings;
    private static bool _settingsTried;

    public static string LogoPath => ResolveAsset(LogoCandidates);
    public static string TextPath => ResolveAsset(TextCandidates);
    public static string SettingsPath => ResolveAsset(SettingsCandidates);

    /// <summary>Cached Discord glyph (blurple plate + white Clyde face). Never dispose.</summary>
    private static readonly Dictionary<int, Bitmap> DiscordIcons = new();

    /// <summary>
    /// Small Discord mark sized for UI labels. Prefer <paramref name="px"/> ≤ adjacent text height.
    /// </summary>
    public static Image DiscordIcon(int px)
    {
        px = Math.Clamp(px, 10, 28);
        if (DiscordIcons.TryGetValue(px, out var hit)) return hit;

        var bmp = new Bitmap(px, px, PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            // Discord blurple plate
            using (var plate = new SolidBrush(Color.FromArgb(255, 88, 101, 242)))
                g.FillEllipse(plate, 0.5f, 0.5f, px - 1f, px - 1f);

            // Simplified Clyde face (controller) — scaled to icon
            float s = px / 16f;
            using var face = new GraphicsPath();
            // body oval
            face.AddEllipse(3.2f * s, 4.4f * s, 9.6f * s, 7.2f * s);
            // left ear
            face.AddEllipse(2.4f * s, 3.2f * s, 3.2f * s, 3.6f * s);
            // right ear
            face.AddEllipse(10.4f * s, 3.2f * s, 3.2f * s, 3.6f * s);
            using (var white = new SolidBrush(Color.White))
                g.FillPath(white, face);

            // eyes (cut from face by painting blurple dots)
            using (var eye = new SolidBrush(Color.FromArgb(255, 88, 101, 242)))
            {
                g.FillEllipse(eye, 5.4f * s, 6.6f * s, 1.8f * s, 2.2f * s);
                g.FillEllipse(eye, 8.8f * s, 6.6f * s, 1.8f * s, 2.2f * s);
            }
        }

        DiscordIcons[px] = bmp;
        return bmp;
    }

    private static string ResolveAsset(string[] names)
    {
        foreach (string name in names)
        {
            string beside = Path.Combine(AppContext.BaseDirectory, "Assets", name);
            if (File.Exists(beside)) return beside;
            string dev = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Assets", name));
            if (File.Exists(dev)) return dev;
        }
        return Path.Combine(AppContext.BaseDirectory, "Assets", names[0]);
    }

    /// <summary>Shared logo (loaded once, no file lock). Never dispose.</summary>
    public static Image? Logo
    {
        get
        {
            if (_logoTried) return _logo;
            _logoTried = true;
            _logo = LoadBitmap(LogoPath);
            return _logo;
        }
    }

    /// <summary>Wordmark “CUSTOM CAR SAVES” PNG. Never dispose.</summary>
    public static Image? TextTitle
    {
        get
        {
            if (_textTried) return _text;
            _textTried = true;
            _text = LoadBitmap(TextPath);
            return _text;
        }
    }

    /// <summary>Galaxy wrench/screwdriver settings mark (Icons8 Nolan). Never dispose.</summary>
    public static Image? SettingsIcon
    {
        get
        {
            if (_settingsTried) return _settings;
            _settingsTried = true;
            _settings = LoadBitmap(SettingsPath);
            return _settings;
        }
    }

    /// <summary>Logo pre-scaled to fit inside maxW x maxH (cached).</summary>
    public static Bitmap? LogoFit(int maxW, int maxH) => Fit(Logo, maxW, maxH, "logo", keyBlack: false);

    /// <summary>Title wordmark fitted; near-black keyed to transparent for dark UI.</summary>
    public static Bitmap? TextFit(int maxW, int maxH) => Fit(TextTitle, maxW, maxH, "text", keyBlack: true);

    /// <summary>Settings icon fitted; black plate keyed transparent for the hero banner.</summary>
    public static Bitmap? SettingsFit(int maxW, int maxH) => Fit(SettingsIcon, maxW, maxH, "settings", keyBlack: true);

    private static Image? LoadBitmap(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var fs = File.OpenRead(path);
            using var img = Image.FromStream(fs);
            return new Bitmap(img);
        }
        catch
        {
            return null;
        }
    }

    private static Bitmap? Fit(Image? src, int maxW, int maxH, string kind, bool keyBlack)
    {
        if (src == null) return null;
        var key = (maxW, maxH, kind);
        if (_fit.TryGetValue(key, out var cached)) return cached;

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
        if (keyBlack)
            KeyNearBlackTransparent(bmp);
        _fit[key] = bmp;
        return bmp;
    }

    /// <summary>Drop solid black plate from Adobe/Express exports so wordmark sits on UI chrome.</summary>
    private static void KeyNearBlackTransparent(Bitmap bmp)
    {
        var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height),
            ImageLockMode.ReadWrite, PixelFormat.Format32bppPArgb);
        try
        {
            int bytes = Math.Abs(data.Stride) * bmp.Height;
            byte[] buf = new byte[bytes];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, buf, 0, bytes);
            for (int i = 0; i + 3 < buf.Length; i += 4)
            {
                byte b = buf[i], g = buf[i + 1], r = buf[i + 2];
                if (r < 22 && g < 22 && b < 22)
                    buf[i + 3] = 0;
            }
            System.Runtime.InteropServices.Marshal.Copy(buf, 0, data.Scan0, bytes);
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }
}
