using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace NFSU2CustomCarSaves;

/// <summary>Drop-hint overlay in galaxy style.</summary>
public sealed class DropOverlay : Panel
{
    public DropOverlay()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
        Dock = DockStyle.Fill;
        Visible = false;
        AllowDrop = true;
        BackColor = Color.FromArgb(205, 6, 7, 24);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        int w = Math.Min(520, Width - 40);
        int h = Math.Min(210, Height - 40);
        if (w <= 40 || h <= 40) return;
        var card = new Rectangle((Width - w) / 2, (Height - h) / 2, w, h);

        for (int i = 4; i >= 1; i--)
        {
            using var p = GalaxyCanvas.RoundRect(Rectangle.Inflate(card, i * 6, i * 6), 22 + i * 3);
            using var b = new SolidBrush(Color.FromArgb(10, 127, 227, 255));
            g.FillPath(b, p);
        }
        using (var path = GalaxyCanvas.RoundRect(card, 20))
        {
            using var fill = new LinearGradientBrush(card, Color.FromArgb(245, 0x24, 0x20, 0x62), Color.FromArgb(245, 0x10, 0x10, 0x36), 90f);
            g.FillPath(fill, path);
            using var edge = new Pen(Color.FromArgb(160, 150, 140, 255), 1.4f);
            g.DrawPath(edge, path);
        }
        using (var dash = GalaxyCanvas.RoundRect(Rectangle.Inflate(card, -12, -12), 14))
        using (var dp = new Pen(Color.FromArgb(150, UiTheme.Primary), 1.6f) { DashStyle = DashStyle.Dash })
            g.DrawPath(dp, dash);

        using var titleFont = UiTheme.UiBold(20f);
        using var subFont = UiTheme.UiFont(9.5f);
        string title = "Drop your save";
        var ts = g.MeasureString(title, titleFont);
        GalaxyCanvas.DrawGradientText(g, title, titleFont, card.X + (card.Width - ts.Width) / 2f, card.Y + 52, UiTheme.Primary, UiTheme.Lilac);

        using var sb = new SolidBrush(UiTheme.TextMuted);
        using var fmt = new StringFormat { Alignment = StringAlignment.Center };
        g.DrawString("zip / rar / folder  ·  career-save  ·  GARAGE.DAT  ·  or both\nFile stays local — matching fingerprints on export.",
            subFont, sb, new RectangleF(card.X + 20, card.Y + 106, card.Width - 40, 60), fmt);
    }

    public void ShowDrop()
    {
        if (Visible) return;
        Visible = true;
        BringToFront();
    }

    public void HideDrop()
    {
        Visible = false;
    }
}
