using System.Drawing.Text;

namespace NFSU2CustomCarSaves;

internal enum StartupLibraryChoice
{
    LoadOnce,
    SkipOnce,
    AlwaysLoad,
}

/// <summary>
/// Stock-library ask — same animated galaxy sky as Guide / main hero,
/// centered card text painted via Overlay (no label flicker).
/// </summary>
internal sealed class StartupLibraryAskForm : Form
{
    private readonly StarfieldControl _sky = new(80, 36) { Dock = DockStyle.Fill };
    private readonly Font _fTitle = UiTheme.UiBold(12.5f);
    private readonly Font _fBody = UiTheme.UiFont(9f);
    private readonly Font _fHint = UiTheme.UiFont(8.5f);

    public StartupLibraryChoice Choice { get; private set; } = StartupLibraryChoice.SkipOnce;

    public StartupLibraryAskForm()
    {
        Text = "Default libraries";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ShowIcon = false;
        ClientSize = new Size(400, 200);
        UiTheme.ApplyForm(this);
        BackColor = UiTheme.BgDeep;

        var tip = new ToolTip { AutoPopDelay = 10000, InitialDelay = 350, ReshowDelay = 200 };

        // Same animated sky as GalaxyGuideForm (seed 36, ~80 stars, Animate=true).
        _sky.Overlay = PaintCard;
        Controls.Add(_sky);

        var bottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            BackColor = UiTheme.BgDeep,
        };

        // 3-column table: empty | buttons | empty → true center
        var center = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 6, 0, 6),
        };
        center.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        center.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        center.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        center.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var btns = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Anchor = AnchorStyles.None,
        };

        var yes = UiTheme.Compact(UiTheme.Btn("Yes", () =>
        {
            Choice = StartupLibraryChoice.LoadOnce;
            DialogResult = DialogResult.Yes;
            Close();
        }, start: true));
        var no = UiTheme.Compact(UiTheme.Btn("No", () =>
        {
            Choice = StartupLibraryChoice.SkipOnce;
            DialogResult = DialogResult.No;
            Close();
        }));
        var always = UiTheme.Compact(UiTheme.Btn("Don't ask again", () =>
        {
            Choice = StartupLibraryChoice.AlwaysLoad;
            DialogResult = DialogResult.Ignore;
            Close();
        }, info: true));

        yes.Margin = new Padding(3, 0, 3, 0);
        no.Margin = new Padding(3, 0, 3, 0);
        always.Margin = new Padding(3, 0, 3, 0);

        tip.SetToolTip(yes, "Load once this launch.");
        tip.SetToolTip(no, "Skip once this launch.");
        tip.SetToolTip(always, "Always load + never ask. Change later in Settings.");

        btns.Controls.Add(yes);
        btns.Controls.Add(no);
        btns.Controls.Add(always);
        center.Controls.Add(btns, 1, 0);
        bottom.Controls.Add(center);
        Controls.Add(bottom);

        AcceptButton = yes;
        CancelButton = no;
        FormClosed += (_, _) =>
        {
            _fTitle.Dispose();
            _fBody.Dispose();
            _fHint.Dispose();
        };
    }

    private void PaintCard(Graphics g, Rectangle r)
    {
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        // Soft card over the twinkling sky (Guide-style)
        int cardH = Math.Max(90, r.Height - 12);
        var card = new Rectangle(14, 10, r.Width - 28, cardH);
        using (var path = GalaxyCanvas.RoundRect(card, 12))
        {
            using var fill = new SolidBrush(Color.FromArgb(175, 10, 10, 34));
            g.FillPath(fill, path);
            using var pen = new Pen(Color.FromArgb(110, 150, 140, 255), 1.1f);
            g.DrawPath(pen, path);
        }

        using var center = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Near,
            Trimming = StringTrimming.EllipsisWord,
        };

        float y = card.Y + 16;
        float w = card.Width - 24;
        float x = card.X + 12;

        using (var titleBrush = new SolidBrush(UiTheme.TextStrong))
            g.DrawString("Load stock libraries?", _fTitle, titleBrush, new RectangleF(x, y, w, 28), center);
        y += 28;

        using (var bodyBrush = new SolidBrush(UiTheme.TextMuted))
            g.DrawString("Cars, liveries & dynos from Presets + DYNO SAVES.",
                _fBody, bodyBrush, new RectangleF(x, y, w, 36), center);
        y += 34;

        // Centered settings icon + hint
        var icon = GalaxyAssets.SettingsFit(15, 15);
        const string hint = "Change later in Settings";
        SizeF hintSz = g.MeasureString(hint, _fHint);
        float clusterW = (icon?.Width ?? 0) + (icon != null ? 6 : 0) + hintSz.Width;
        float hx = card.X + (card.Width - clusterW) / 2f;
        float hy = y + 2;
        if (icon != null)
        {
            g.DrawImage(icon, hx, hy, icon.Width, icon.Height);
            hx += icon.Width + 6;
        }
        using (var hintBrush = new SolidBrush(UiTheme.TextMuted))
            g.DrawString(hint, _fHint, hintBrush, hx, hy + Math.Max(0, ((icon?.Height ?? 14) - hintSz.Height) / 2f));
    }
}
