using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace NFSU2CustomCarSaves;

/// <summary>Galaxy splash with CCS logo — fake-loads 5–8 s so it feels busy.</summary>
public sealed class SplashForm : Form
{
    private readonly float _durationSec;
    private readonly float _closeAtSec;

    private readonly StarfieldControl _sky = new(130, 5) { Dock = DockStyle.Fill };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 30 };
    private readonly System.Diagnostics.Stopwatch _clock = new();
    private readonly Font _fSub = new("Segoe UI", 9.5f);
    private readonly Font _fTiny = new("Segoe UI", 8f);
    private float _progress;
    private bool _done;
    private readonly string[] _beats =
    [
        "Warming thrusters…",
        "Booting garage tools…",
        "Tuning neon…",
        "Indexing car slots…",
        "Aligning Bayview stars…",
        "Loading dyno presets…",
        "Polishing vinyl layers…",
        "Ready to race",
    ];

    public SplashForm()
    {
        // Random “loading” length so each launch feels a bit different.
        _durationSec = 5f + (float)(Random.Shared.NextDouble() * 3.0); // 5.0 … 8.0
        _closeAtSec = _durationSec + 0.25f;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Custom Car Saves";
        Width = 560;
        Height = 420;
        BackColor = UiTheme.Bg;
        ShowInTaskbar = false;
        TopMost = true;
        KeyPreview = true;
        Opacity = 0;

        _sky.Overlay = PaintSplash;
        _sky.Click += (_, _) => Finish();
        KeyDown += (_, _) => Finish();
        Controls.Add(_sky);

        Load += (_, _) =>
        {
            using var path = GalaxyCanvas.RoundRect(new RectangleF(0, 0, Width, Height), 26);
            Region = new Region(path);
            _clock.Start();
            _timer.Start();
        };
        _timer.Tick += (_, _) =>
        {
            float t = (float)_clock.Elapsed.TotalSeconds;
            float x = Math.Clamp(t / _durationSec, 0f, 1f);
            // Ease-in-out so the bar doesn’t look linear/robotic.
            _progress = x * x * (3f - 2f * x);
            Opacity = Math.Min(1.0, t / 0.25);
            if (t >= _closeAtSec) Finish();
        };
        FormClosed += (_, _) =>
        {
            _timer.Stop();
            _timer.Dispose();
            _fSub.Dispose();
            _fTiny.Dispose();
        };
    }

    private void Finish()
    {
        if (_done) return;
        _done = true;
        _timer.Stop();
        DialogResult = DialogResult.OK;
        Close();
    }

    private void PaintSplash(Graphics g, Rectangle r)
    {
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        float t = _sky.Time;
        float pulse = 0.5f + 0.5f * MathF.Sin(t * 3f);

        // Logo already includes “CUSTOM CAR SAVES - CCS” — no extra title under it.
        var logo = GalaxyAssets.LogoFit(420, 230);
        GalaxyCanvas.DrawLogo(g, logo, new Rectangle((r.Width - 420) / 2, 36, 420, 230), pulse);

        int beat = Math.Min(_beats.Length - 1, (int)(_progress * _beats.Length));
        string tip = _beats[beat];
        var tps = g.MeasureString(tip, _fSub);
        using (var mb = new SolidBrush(Color.FromArgb(210, 180, 190, 235)))
            g.DrawString(tip, _fSub, mb, (r.Width - tps.Width) / 2f, 280);

        // progress bar
        int barW = 300, barH = 8;
        int barX = (r.Width - barW) / 2;
        int barY = r.Height - 76;
        using (var tp = GalaxyCanvas.RoundRect(new RectangleF(barX, barY, barW, barH), 4))
        using (var tb = new SolidBrush(Color.FromArgb(46, 255, 255, 255)))
            g.FillPath(tb, tp);
        int fw = Math.Max(2, (int)(barW * _progress));
        var fr = new RectangleF(barX, barY, fw, barH);
        for (int i = 3; i >= 1; i--)
        {
            using var gp = GalaxyCanvas.RoundRect(RectangleF.Inflate(fr, i * 1.5f, i * 1.5f), 4 + i);
            using var gb = new SolidBrush(Color.FromArgb(14, 140, 200, 255));
            g.FillPath(gb, gp);
        }
        using (var fp = GalaxyCanvas.RoundRect(fr, 4))
        using (var fb = new LinearGradientBrush(new RectangleF(barX, barY, Math.Max(2, barW), barH),
                   Color.FromArgb(255, 120, 220, 255), Color.FromArgb(255, 190, 140, 255), 0f))
            g.FillPath(fb, fp);

        using (var cb = new SolidBrush(Color.FromArgb(150, 200, 210, 255)))
        {
            g.DrawString("by BARKIE", _fTiny, cb, 22, r.Height - 32);
            var d = g.MeasureString("@extrabarkie", _fTiny);
            g.DrawString("@extrabarkie", _fTiny, cb, r.Width - d.Width - 22, r.Height - 32);
        }

        using var border = new Pen(Color.FromArgb(120, 150, 140, 255), 1.5f);
        using var bp = GalaxyCanvas.RoundRect(new RectangleF(1, 1, r.Width - 3, r.Height - 3), 25);
        g.DrawPath(border, bp);
    }
}
