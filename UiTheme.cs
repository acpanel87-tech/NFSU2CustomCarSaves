using System.Drawing.Drawing2D;

namespace NFSU2CustomCarSaves;

/// <summary>Soft blue/purple galaxy tokens + controls.</summary>
internal static class UiTheme
{
    public static readonly Color BgDeep = Color.FromArgb(0x07, 0x08, 0x1C);
    public static readonly Color Bg = Color.FromArgb(0x0B, 0x0C, 0x28);
    public static readonly Color Surface = Color.FromArgb(0x13, 0x13, 0x38);
    public static readonly Color Elevated = Color.FromArgb(0x22, 0x20, 0x50);
    public static readonly Color Border = Color.FromArgb(0x3C, 0x38, 0x7A);
    public static readonly Color Primary = Color.FromArgb(0x7F, 0xE3, 0xFF);       // galaxy cyan
    public static readonly Color PrimaryHover = Color.FromArgb(0xA6, 0xEE, 0xFF);
    public static readonly Color Lilac = Color.FromArgb(0xB8, 0x9B, 0xFF);
    public static readonly Color Text = Color.FromArgb(0xC9, 0xCC, 0xEE);
    public static readonly Color TextStrong = Color.FromArgb(0xF2, 0xF0, 0xFF);
    public static readonly Color TextMuted = Color.FromArgb(0x8E, 0x92, 0xC4);
    public static readonly Color Warning = Color.FromArgb(0xFF, 0xA8, 0x9A);
    public static readonly Color Amber = Color.FromArgb(0xF5, 0xC0, 0x6A);
    public static readonly Color Success = Color.FromArgb(0x8E, 0xF0, 0xB4);
    public static readonly Color Danger = Color.FromArgb(0x55, 0x18, 0x36);
    public static readonly Color DangerHover = Color.FromArgb(0x7A, 0x24, 0x4A);
    public static readonly Color DangerText = Color.FromArgb(0xFF, 0xC4, 0xCC);
    public static readonly Color RacingPurple = Color.FromArgb(0x4B, 0x33, 0xA8);
    public static readonly Color RacingPurpleHover = Color.FromArgb(0x6A, 0x4B, 0xD6);
    public static readonly Color RacingPurpleText = Color.FromArgb(0xEA, 0xDD, 0xFF);
    public static readonly Color InfoBlue = Color.FromArgb(0x1E, 0x34, 0x78);
    public static readonly Color InfoBlueHover = Color.FromArgb(0x2A, 0x4A, 0x9A);
    public static readonly Color InfoBlueText = Color.FromArgb(0xCF, 0xE0, 0xFF);
    public static readonly Color StartYellow = Color.FromArgb(0xF7, 0xC9, 0x3E);
    public static readonly Color StartYellowHover = Color.FromArgb(0xFF, 0xDC, 0x72);
    public static readonly Color StartYellowText = Color.FromArgb(0x2A, 0x1C, 0x00);
    public static readonly Color HudDim = Color.FromArgb(90, 127, 227, 255);

    public static Font UiFont(float size = 9f) => new("Segoe UI", size);
    public static Font UiBold(float size = 9f) => new("Segoe UI Semibold", size);
    public static Font Mono(float size = 9.5f) => new("Consolas", size);

    public static void ApplyForm(Form f)
    {
        f.BackColor = Bg;
        f.ForeColor = Text;
        f.Font = UiFont();
    }

    public static Button Btn(string text, Action click, bool primary = false, bool danger = false, bool accent = false, bool info = false, bool start = false)
    {
        GlowKind kind = danger ? GlowKind.Danger
            : start ? GlowKind.Start
            : accent ? GlowKind.Accent
            : info ? GlowKind.Info
            : primary ? GlowKind.Primary
            : GlowKind.Default;
        var b = new GlowButton { Text = text, Kind = kind };
        b.Click += (_, _) =>
        {
            try { click(); }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Custom Car Saves", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };
        return b;
    }

    /// <summary>Smaller padding/font for compact dialogs (guide, splash bars, etc.).</summary>
    public static Button Compact(Button b)
    {
        if (b is GlowButton g)
            g.MakeCompact();
        else
        {
            b.Font = UiBold(8.5f);
            b.Padding = new Padding(10, 4, 10, 4);
            b.Margin = new Padding(0, 0, 5, 2);
        }
        return b;
    }

    public static Label Title(string text, float size = 18f) => new()
    {
        Text = text,
        AutoSize = true,
        Font = UiBold(size),
        ForeColor = TextStrong,
        Margin = new Padding(0, 0, 0, 2),
    };

    public static Label Subtitle(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = UiFont(9.5f),
        ForeColor = TextMuted,
        Margin = new Padding(0, 0, 0, 10),
        MaximumSize = new Size(920, 0),
    };

    public static Label SectionHead(string text) => new()
    {
        Text = "✦  " + text,
        AutoSize = true,
        Font = UiBold(11f),
        ForeColor = Primary,
        Margin = new Padding(0, 0, 0, 6),
    };

    public static Label Muted(string text, int maxWidth = 520) => new()
    {
        Text = text,
        AutoSize = true,
        Font = UiFont(8.5f),
        ForeColor = TextMuted,
        MaximumSize = new Size(maxWidth, 0),
        Margin = new Padding(0, 0, 0, 8),
    };

    public static Panel Card(Control content, Padding? pad = null)
    {
        var outer = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(1),
            BackColor = Border,
            Margin = new Padding(0, 0, 10, 0),
        };
        var inner = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Padding = pad ?? new Padding(12),
        };
        content.Dock = DockStyle.Fill;
        inner.Controls.Add(content);
        outer.Controls.Add(inner);
        return outer;
    }

    public static void StyleList(ListBox lb)
    {
        lb.Dock = DockStyle.Fill;
        lb.BackColor = Surface;
        lb.ForeColor = Text;
        lb.BorderStyle = BorderStyle.None;
        lb.IntegralHeight = false;
        lb.Font = Mono(9.5f);
        lb.ItemHeight = 22;
        lb.DrawMode = DrawMode.OwnerDrawFixed;
        lb.DrawItem += DrawListItem;
    }

    private static void DrawListItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ListBox lb) return;
        var g = e.Graphics;
        var r = e.Bounds;
        if (r.Width <= 0 || r.Height <= 0) return;

        using (var bb = new SolidBrush(lb.BackColor))
            g.FillRectangle(bb, r);
        if (e.Index < 0 || e.Index >= lb.Items.Count) return;

        bool sel = (e.State & DrawItemState.Selected) != 0;
        if (sel)
        {
            using var gb = new LinearGradientBrush(r, Color.FromArgb(0x3E, 0x32, 0x96), Color.FromArgb(0x22, 0x30, 0x72), 0f);
            g.FillRectangle(gb, r);
            using var bar = new SolidBrush(Primary);
            g.FillRectangle(bar, r.X, r.Y, 3, r.Height);
        }

        string text = lb.GetItemText(lb.Items[e.Index]);
        TextRenderer.DrawText(g, text, lb.Font,
            new Rectangle(r.X + 8, r.Y, r.Width - 10, r.Height),
            sel ? TextStrong : lb.ForeColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
            TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    public static void StyleCombo(ComboBox cb)
    {
        cb.FlatStyle = FlatStyle.Flat;
        cb.BackColor = Elevated;
        cb.ForeColor = TextStrong;
        cb.Font = UiFont();
    }

    /// <summary>Simple progress bar row (slot-stats look).</summary>
    public static TableLayoutPanel ProgressRow(string label, out Label valueLbl, out Panel fill)
    {
        var row = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 2,
            AutoSize = true,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 10),
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        row.RowStyles.Add(new RowStyle(SizeType.Absolute, 8f));

        var caption = new Label
        {
            Text = label,
            AutoSize = true,
            Font = UiFont(8.5f),
            ForeColor = TextMuted,
            Margin = new Padding(0, 0, 0, 2),
        };
        valueLbl = new Label
        {
            AutoSize = true,
            Font = UiBold(9.5f),
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = TextStrong,
            Margin = new Padding(8, 0, 0, 2),
        };
        var track = new Panel
        {
            Height = 6,
            Dock = DockStyle.Fill,
            BackColor = Elevated,
            Margin = new Padding(0, 2, 0, 0),
        };
        fill = new Panel { Height = 6, Width = 0, BackColor = Primary, Dock = DockStyle.Left };
        track.Controls.Add(fill);
        row.Controls.Add(caption, 0, 0);
        row.Controls.Add(valueLbl, 1, 0);
        row.Controls.Add(track, 0, 1);
        row.SetColumnSpan(track, 2);
        return row;
    }

    public static void SetProgress(Panel fill, Label valueLbl, int on, int total, string? suffix = null)
    {
        valueLbl.Text = suffix == null ? $"{on} / {total}" : $"{on} / {total}  {suffix}";
        var track = fill.Parent;
        if (track == null) return;
        void Apply()
        {
            int w = Math.Max(1, track.ClientSize.Width);
            double pct = total <= 0 ? 0 : Math.Clamp(on / (double)total, 0, 1);
            fill.Width = (int)Math.Round(w * pct);
        }
        Apply();
        track.Resize -= OnResize;
        track.Resize += OnResize;
        void OnResize(object? _, EventArgs __) => Apply();
    }
}

internal enum GlowKind { Default, Primary, Accent, Info, Danger, Start, Pink, PinkDark }

/// <summary>Rounded gradient button with a gentle hover/CTA glow.</summary>
internal sealed class GlowButton : Button
{
    private GlowKind _kind = GlowKind.Default;
    private bool _hover, _down;
    private Color _top, _bottom, _fore, _edge, _glow;

    public GlowKind Kind
    {
        get => _kind;
        set
        {
            _kind = value;
            ApplyKind();
        }
    }

    public GlowButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        FlatAppearance.BorderColor = UiTheme.Bg;
        FlatAppearance.MouseOverBackColor = UiTheme.Bg;
        FlatAppearance.MouseDownBackColor = UiTheme.Bg;
        FlatAppearance.CheckedBackColor = UiTheme.Bg;
        UseVisualStyleBackColor = false;
        BackColor = UiTheme.Bg;
        ForeColor = UiTheme.TextStrong;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Font = UiTheme.UiBold(9f);
        Margin = new Padding(0, 0, 6, 4);
        Padding = new Padding(14, 8, 14, 8);
        Cursor = Cursors.Hand;
        ApplyKind();
    }

    public void MakeCompact()
    {
        Font = UiTheme.UiBold(8.5f);
        Padding = new Padding(10, 4, 10, 4);
        Margin = new Padding(0, 0, 5, 2);
        Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        // Suppress default Button chrome — OnPaint draws the full face.
    }

    private static Color C(int r, int g, int b) => Color.FromArgb(r, g, b);

    private void ApplyKind()
    {
        switch (_kind)
        {
            case GlowKind.Primary:
                _top = C(0x9A, 0xEC, 0xFF); _bottom = C(0x7C, 0xA2, 0xFF); _fore = C(0x08, 0x0A, 0x26);
                _edge = C(0x6A, 0xB8, 0xD8); _glow = C(0x7F, 0xE3, 0xFF);
                break;
            case GlowKind.Accent:
                _top = C(0x6A, 0x4B, 0xD6); _bottom = C(0x4B, 0x33, 0xA8); _fore = UiTheme.RacingPurpleText;
                _edge = C(0x5A, 0x48, 0xB0); _glow = C(0xA0, 0x80, 0xFF);
                break;
            case GlowKind.Info:
                _top = C(0x2A, 0x4A, 0x9A); _bottom = C(0x1E, 0x34, 0x78); _fore = UiTheme.InfoBlueText;
                _edge = C(0x38, 0x52, 0x98); _glow = C(0x70, 0xA0, 0xFF);
                break;
            case GlowKind.Danger:
                // Classic red wipe / destructive
                _top = C(0x7A, 0x24, 0x4A); _bottom = C(0x55, 0x18, 0x36); _fore = UiTheme.DangerText;
                _edge = C(0x78, 0x30, 0x50); _glow = C(0xFF, 0x70, 0x98);
                break;
            case GlowKind.PinkDark:
                // Selected Career / Customize tab
                _top = C(0xC4, 0x3A, 0x7A); _bottom = C(0x8A, 0x1E, 0x52); _fore = C(0xFF, 0xE8, 0xF0);
                _edge = C(0xA0, 0x30, 0x68); _glow = C(0xFF, 0x6B, 0xA8);
                break;
            case GlowKind.Pink:
                // Unselected tab
                _top = C(0xF0, 0xA8, 0xC8); _bottom = C(0xE0, 0x7A, 0xA8); _fore = C(0x3A, 0x0C, 0x24);
                _edge = C(0xD0, 0x70, 0x98); _glow = C(0xFF, 0xB0, 0xD0);
                break;
            case GlowKind.Start:
                _top = C(0xFF, 0xDC, 0x6A); _bottom = C(0xF2, 0xB4, 0x2A); _fore = UiTheme.StartYellowText;
                _edge = C(0xC8, 0x96, 0x28); _glow = C(0xFF, 0xCC, 0x50);
                break;
            default:
                _top = C(0x2E, 0x2C, 0x66); _bottom = C(0x22, 0x20, 0x50); _fore = UiTheme.TextStrong;
                _edge = C(0x3A, 0x36, 0x70); _glow = C(0x9A, 0x8C, 0xFF);
                break;
        }
        ForeColor = _fore;
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateRegion();
    }

    private void UpdateRegion()
    {
        if (Width < 4 || Height < 4) return;
        using var path = GalaxyCanvas.RoundRect(new RectangleF(0, 0, Width, Height), 8f);
        Region?.Dispose();
        Region = new Region(path);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        // 0.5 inset so the 1px stroke sits inside the client — no outer halo frame.
        var rc = new RectangleF(0.5f, 0.5f, Math.Max(1, Width - 1f), Math.Max(1, Height - 1f));
        if (rc.Width < 4 || rc.Height < 4) return;
        if (_down) rc.Offset(0, 1);

        bool en = Enabled;
        Color top = en ? _top : GalaxyCanvas.Blend(_top, UiTheme.Bg, 0.65f);
        Color bottom = en ? _bottom : GalaxyCanvas.Blend(_bottom, UiTheme.Bg, 0.65f);
        Color fore = en ? _fore : GalaxyCanvas.Blend(_fore, UiTheme.Bg, 0.55f);
        Color edge = en ? _edge : GalaxyCanvas.Blend(_edge, UiTheme.Bg, 0.6f);
        if (en && _hover)
        {
            top = GalaxyCanvas.Blend(top, Color.White, 0.12f);
            bottom = GalaxyCanvas.Blend(bottom, Color.White, 0.08f);
            edge = GalaxyCanvas.Blend(edge, _glow, 0.35f);
        }

        const float radius = 8f;
        using (var path = GalaxyCanvas.RoundRect(rc, radius))
        {
            using (var br = new LinearGradientBrush(rc, top, bottom, 90f))
                g.FillPath(br, path);
            using var ep = new Pen(edge, 1f);
            g.DrawPath(ep, path);
        }

        if (Focused && ShowFocusCues)
        {
            using var p = GalaxyCanvas.RoundRect(RectangleF.Inflate(rc, -3, -3), 5);
            using var fp = new Pen(Color.FromArgb(120, UiTheme.Lilac), 1f) { DashStyle = DashStyle.Dot };
            g.DrawPath(fp, p);
        }

        TextRenderer.DrawText(g, Text, Font, Rectangle.Round(rc), fore,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
    }
}
