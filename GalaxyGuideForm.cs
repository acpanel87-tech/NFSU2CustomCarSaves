using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace NFSU2CustomCarSaves;

/// <summary>Compact starry intro guide + credits (BARKIE / Discord @extrabarkie).</summary>
public sealed class GalaxyGuideForm : Form
{
    private readonly StarfieldControl _sky = new(80, 36) { Dock = DockStyle.Fill };
    private readonly GlowButton _btnGuide;
    private readonly GlowButton _btnCredits;
    private int _tab; // 0 guide, 1 credits

    private readonly Font _fH = new("Segoe UI Semibold", 13f, FontStyle.Bold | FontStyle.Italic);
    private readonly Font _fBody = new("Segoe UI", 9.5f);
    private readonly Font _fMute = new("Segoe UI", 8.5f);
    private readonly Font _fBadge = new("Segoe UI Semibold", 9f, FontStyle.Bold);
    private readonly Font _fName = new("Segoe UI Black", 28f, FontStyle.Bold | FontStyle.Italic);
    private readonly Font _fMono = new("Consolas", 14f, FontStyle.Bold);

    private static readonly string[] Steps =
    [
        "Set Name + Flag (your nfsu2.xyz player name / free-roam flag), then Start or Open.",
        "Edit slots 1–10: Add car, Tune, Copy / Paste, rack.",
        "Optional: 100% Save unlocks shop/career without touching your cars.",
        "Save zip… — name, flag, GARAGE.DAT + settings for nfsu2.xyz.",
        "On nfsu2.xyz → Restore a backup (not “Add profile”).",
    ];

    public GalaxyGuideForm()
    {
        UiTheme.ApplyForm(this);
        Text = "CCS Guide";
        ClientSize = new Size(560, 420);
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        BackColor = UiTheme.BgDeep;

        _sky.Overlay = PaintSky;
        Controls.Add(_sky);

        var topBar = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = UiTheme.BgDeep };
        var tabs = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            Padding = new Padding(10, 6, 0, 0),
            BackColor = UiTheme.BgDeep,
        };
        _btnGuide = (GlowButton)UiTheme.Compact(UiTheme.Btn("✦  Quick guide", () => SetTab(0), primary: true));
        _btnCredits = (GlowButton)UiTheme.Compact(UiTheme.Btn("★  Credits", () => SetTab(1)));
        tabs.Controls.Add(_btnGuide);
        tabs.Controls.Add(_btnCredits);
        topBar.Controls.Add(tabs);
        Controls.Add(topBar);

        var bottomBar = new Panel { Dock = DockStyle.Bottom, Height = 42, BackColor = UiTheme.BgDeep };
        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(10, 6, 10, 4),
            BackColor = UiTheme.BgDeep,
        };
        var ride = UiTheme.Compact(UiTheme.Btn("Let's ride", () => { DialogResult = DialogResult.OK; Close(); }, primary: true));
        bottom.Controls.Add(ride);
        bottomBar.Controls.Add(bottom);
        Controls.Add(bottomBar);
        AcceptButton = ride;
        CancelButton = ride;

        SetTab(0);
        FormClosed += (_, _) =>
        {
            _fH.Dispose(); _fBody.Dispose(); _fMute.Dispose();
            _fBadge.Dispose(); _fName.Dispose(); _fMono.Dispose();
        };
    }

    private void SetTab(int id)
    {
        _tab = id;
        _btnGuide.Kind = id == 0 ? GlowKind.Primary : GlowKind.Default;
        _btnCredits.Kind = id == 1 ? GlowKind.Primary : GlowKind.Default;
        _sky.Invalidate();
    }

    private void PaintSky(Graphics g, Rectangle r)
    {
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        var card = new Rectangle(12, 8, r.Width - 24, r.Height - 16);
        using (var path = GalaxyCanvas.RoundRect(card, 14))
        {
            using var fill = new SolidBrush(Color.FromArgb(170, 10, 10, 34));
            g.FillPath(fill, path);
            using var pen = new Pen(Color.FromArgb(120, 150, 140, 255), 1.2f);
            g.DrawPath(pen, path);
        }
        if (_tab == 0) PaintGuide(g, card);
        else PaintCredits(g, card);
    }

    private void PaintGuide(Graphics g, Rectangle card)
    {
        float x = card.X + 18;
        float y = card.Y + 14;
        var hs = GalaxyCanvas.DrawGradientText(g, "Welcome to the underground", _fH, x, y, UiTheme.Primary, UiTheme.Lilac);
        y += hs.Height + 10;

        float textW = card.Width - 40 - 36;
        using var textBrush = new SolidBrush(Color.FromArgb(235, 220, 228, 248));
        using var badgeFill = new SolidBrush(Color.FromArgb(60, 127, 227, 255));
        using var badgePen = new Pen(Color.FromArgb(200, 127, 227, 255), 1.2f);
        using var badgeText = new SolidBrush(UiTheme.TextStrong);
        using var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        for (int i = 0; i < Steps.Length; i++)
        {
            var badge = new RectangleF(x, y, 24, 24);
            g.FillEllipse(badgeFill, badge);
            g.DrawEllipse(badgePen, badge);
            g.DrawString((i + 1).ToString(), _fBadge, badgeText, badge, center);

            SizeF m = g.MeasureString(Steps[i], _fBody, (int)textW);
            g.DrawString(Steps[i], _fBody, textBrush, new RectangleF(x + 34, y + 2, textW, m.Height + 2));
            y += Math.Max(26f, m.Height + 4f) + 8f;
        }

        y += 4;
        using var dim = new SolidBrush(Color.FromArgb(200, 160, 175, 215));
        g.DrawString("Tip: if the site says GARAGE.DAT is left out, cancel and use a fresh zip from this app.",
            _fMute, dim, new RectangleF(x, y, card.Width - 36, 36));
    }

    private void PaintCredits(Graphics g, Rectangle card)
    {
        var logo = GalaxyAssets.LogoFit(200, 112);
        int logoBottom = card.Y + 20;
        if (logo != null)
        {
            int lx = card.Right - logo.Width - 14;
            int ly = card.Y + 12;
            g.DrawImageUnscaled(logo, lx, ly);
            logoBottom = ly + logo.Height;
        }

        using var text = new SolidBrush(Color.FromArgb(242, 232, 240, 255));
        using var dim = new SolidBrush(Color.FromArgb(200, 150, 170, 210));
        using var bright = new SolidBrush(Color.FromArgb(255, 210, 238, 255));

        float x = card.X + 18;
        float y = card.Y + 16;
        float textRight = logo != null ? card.Right - logo.Width - 28 : card.Right - 18;
        float leftW = Math.Max(180, textRight - x);

        var ns = GalaxyCanvas.DrawGradientText(g, "BARKIE", _fName, x - 1, y, UiTheme.Lilac, UiTheme.Primary);
        y += ns.Height + 6;
        g.DrawString("Discord", _fMute, dim, x, y);
        y += 18;
        g.DrawString("@extrabarkie", _fMono, bright, x, y);
        y = Math.Max(y + 28, logoBottom + 12);

        float w = card.Width - 36;
        g.DrawString("Custom Car Saves is a local garage tool for nfsu2.xyz saves.", _fBody, text, new RectangleF(x, y, w, 28));
        y += _fBody.GetHeight(g) + 6;
        g.DrawString("Custom Slots · Unlocks · Livery · Tuning · Dynos · 100% Savegame · and more…", _fBody, text, new RectangleF(x, y, w, 28));
        y += _fBody.GetHeight(g) + 14;
        g.DrawString("Drive loud. Stay neon. See you in free roam.", _fMute, dim, x, y);
        _ = leftW;
    }
}
