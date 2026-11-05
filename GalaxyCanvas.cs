using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace NFSU2CustomCarSaves;

/// <summary>UI-only GDI+ helpers for the galaxy look.</summary>
internal static class GalaxyCanvas
{
    public static readonly Color SkyTop = Color.FromArgb(0x08, 0x09, 0x24);
    public static readonly Color SkyMid = Color.FromArgb(0x12, 0x0F, 0x3A);
    public static readonly Color SkyBottom = Color.FromArgb(0x1C, 0x10, 0x4A);

    public static GraphicsPath RoundRect(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = Math.Min(radius * 2f, Math.Min(r.Width, r.Height));
        if (d <= 0.5f)
        {
            p.AddRectangle(r);
            return p;
        }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static Color Blend(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return Color.FromArgb(
            (int)(a.A + (b.A - a.A) * t),
            (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t),
            (int)(a.B + (b.B - a.B) * t));
    }

    /// <summary>Gradient + soft nebulae, rendered once per size.</summary>
    public static Bitmap RenderBackdrop(Size size)
    {
        var bmp = new Bitmap(Math.Max(1, size.Width), Math.Max(1, size.Height), PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new Rectangle(0, 0, bmp.Width, bmp.Height);

        using (var br = new LinearGradientBrush(r, SkyTop, SkyBottom, 90f))
        {
            br.InterpolationColors = new ColorBlend(3)
            {
                Colors = new[] { SkyTop, SkyMid, SkyBottom },
                Positions = new[] { 0f, 0.5f, 1f },
            };
            g.FillRectangle(br, r);
        }

        Nebula(g, r, 0.12f, 0.92f, 0.55f, 0.65f, Color.FromArgb(78, 120, 70, 232));
        Nebula(g, r, 0.90f, 0.08f, 0.50f, 0.70f, Color.FromArgb(60, 70, 120, 240));
        Nebula(g, r, 0.55f, 0.58f, 0.42f, 0.50f, Color.FromArgb(34, 60, 200, 232));
        Nebula(g, r, 0.28f, 0.18f, 0.30f, 0.45f, Color.FromArgb(36, 190, 100, 232));
        return bmp;
    }

    private static void Nebula(Graphics g, Rectangle r, float cx, float cy, float rw, float rh, Color c)
    {
        float w = r.Width * rw * 2f;
        float h = r.Height * rh * 2f;
        if (w < 4 || h < 4) return;
        using var path = new GraphicsPath();
        path.AddEllipse(r.Width * cx - w / 2f, r.Height * cy - h / 2f, w, h);
        using var pg = new PathGradientBrush(path)
        {
            CenterColor = c,
            SurroundColors = new[] { Color.FromArgb(0, c) },
        };
        g.FillPath(pg, path);
    }

    /// <summary>Draws the (pre-fitted) transparent logo centered in box — no backdrop panel (logo has its own glow).</summary>
    public static void DrawLogo(Graphics g, Bitmap? fitted, Rectangle box, float pulse)
    {
        _ = pulse;
        Rectangle rect;
        if (fitted != null)
            rect = new Rectangle(box.X + (box.Width - fitted.Width) / 2, box.Y + (box.Height - fitted.Height) / 2, fitted.Width, fitted.Height);
        else
            rect = box;

        if (fitted != null)
        {
            g.DrawImageUnscaled(fitted, rect.Location);
            return;
        }

        using var clip = RoundRect(rect, 16);
        using var fill = new LinearGradientBrush(rect, Color.FromArgb(0x2A, 0x24, 0x78), Color.FromArgb(0x14, 0x18, 0x50), 90f);
        g.FillPath(fill, clip);
        using var f = new Font("Segoe UI Black", Math.Max(10f, rect.Height / 3.2f), FontStyle.Bold | FontStyle.Italic);
        using var tb = new SolidBrush(Color.FromArgb(255, 160, 230, 255));
        var sz = g.MeasureString("CCS", f);
        g.DrawString("CCS", f, tb, rect.X + (rect.Width - sz.Width) / 2f, rect.Y + (rect.Height - sz.Height) / 2f);
    }

    public static SizeF DrawGradientText(Graphics g, string text, Font font, float x, float y, Color a, Color b)
    {
        var sz = g.MeasureString(text, font);
        using var br = new LinearGradientBrush(
            new RectangleF(x, y, Math.Max(1f, sz.Width), Math.Max(1f, sz.Height)), a, b, 0f);
        g.DrawString(text, font, br, x, y);
        return sz;
    }
}

/// <summary>Twinkling, slowly drifting stars in normalized 0..1 space.</summary>
internal sealed class Starfield
{
    private struct Star
    {
        public float X, Y, Size, Phase, Rate, Drift;
        public int Hue;
    }

    private readonly Star[] _s;
    private float _t;

    public float Time => _t;

    public Starfield(int count, int seed)
    {
        var rng = new Random(seed);
        _s = new Star[Math.Max(1, count)];
        for (int i = 0; i < _s.Length; i++)
        {
            _s[i] = new Star
            {
                X = rng.NextSingle(),
                Y = rng.NextSingle(),
                Size = 0.9f + rng.NextSingle() * 2.0f,
                Phase = rng.NextSingle() * MathF.PI * 2f,
                Rate = 1.2f + rng.NextSingle() * 2.2f,
                Drift = 0.002f + rng.NextSingle() * 0.008f,
                Hue = rng.Next(3),
            };
        }
    }

    public void Step(float dt)
    {
        _t += dt;
        for (int i = 0; i < _s.Length; i++)
        {
            ref var s = ref _s[i];
            s.X += s.Drift * dt;
            if (s.X > 1.02f) s.X -= 1.04f;
        }
    }

    public void Draw(Graphics g, Rectangle r, float intensity)
    {
        using var b = new SolidBrush(Color.White);
        foreach (var s in _s)
        {
            float tw = 0.35f + 0.65f * (0.5f + 0.5f * MathF.Sin(_t * s.Rate + s.Phase));
            int a = (int)Math.Clamp(235f * tw * intensity, 0f, 255f);
            Color c = s.Hue switch
            {
                1 => Color.FromArgb(a, 160, 225, 255),
                2 => Color.FromArgb(a, 205, 170, 255),
                _ => Color.FromArgb(a, 240, 240, 255),
            };
            float x = s.X * r.Width;
            float y = s.Y * r.Height;
            float sz = s.Size * (0.8f + 0.4f * tw);
            if (s.Size > 1.9f)
            {
                b.Color = Color.FromArgb(a / 6, c);
                g.FillEllipse(b, x - sz, y - sz, sz * 3f, sz * 3f);
            }
            b.Color = c;
            g.FillEllipse(b, x, y, sz, sz);
        }
    }
}
