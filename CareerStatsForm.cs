namespace NFSU2CustomCarSaves;

/// <summary>
/// Career Stats — single Fully Modded wizard (proven/probable actions only).
/// </summary>
public sealed partial class CareerStatsForm : Form
{
    private readonly byte[] _working;
    private readonly Action _onChanged;
    private readonly ToolTip _tip = new() { AutoPopDelay = 14000, InitialDelay = 400, ReshowDelay = 200 };
    private readonly StatsEditHistory _history = new();

    private readonly Label _diffLbl = new();
    private readonly Label _status = new();
    private readonly Panel _wizardPage = new();

    public CareerStatsForm(byte[] working, Action onChanged, string? profileName = null)
    {
        _working = working ?? throw new ArgumentNullException(nameof(working));
        _onChanged = onChanged ?? (() => { });

        string titleName = string.IsNullOrWhiteSpace(profileName) ? "save" : profileName.Trim();
        UiTheme.ApplyForm(this);
        Text = $"Career Stats — {titleName}";
        Width = 760;
        Height = 720;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        KeyPreview = true;
        KeyDown += CareerStatsForm_KeyDown;

        var host = new StarfieldControl(36, 55) { Animate = false, Dock = DockStyle.Fill };
        Controls.Add(host);

        var outer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(16),
            BackColor = Color.Transparent,
        };
        outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        host.Controls.Add(outer);

        var head = new TableLayoutPanel
        {
            ColumnCount = 1,
            AutoSize = true,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 6),
            BackColor = Color.Transparent,
        };
        head.Controls.Add(new Label
        {
            Text = "Career Stats Wizard",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 15f, FontStyle.Bold | FontStyle.Italic),
            ForeColor = UiTheme.TextStrong,
            Margin = new Padding(0, 0, 0, 6),
        }, 0, 0);

        var banner = new Panel
        {
            AutoSize = true,
            Padding = new Padding(10, 6, 10, 6),
            Margin = new Padding(0, 0, 0, 2),
            BackColor = Color.FromArgb(0x3A, 0x2E, 0x14),
        };
        banner.Controls.Add(new Label
        {
            Text = "⚠ Under construction — not everything works as it should yet. yk yk",
            AutoSize = true,
            MaximumSize = new Size(700, 0),
            ForeColor = Color.FromArgb(0xF0, 0xC0, 0x40),
            Font = UiTheme.UiBold(9f),
            Margin = Padding.Empty,
        });
        head.Controls.Add(banner, 0, 1);
        outer.Controls.Add(head, 0, 0);

        var contentFrame = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(1),
            BackColor = Color.FromArgb(0x4A, 0x44, 0x9A),
            Margin = new Padding(0),
        };
        var contentInner = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Bg };
        _wizardPage.Dock = DockStyle.Fill;
        _wizardPage.BackColor = UiTheme.Bg;
        BuildWizardInto(_wizardPage);
        contentInner.Controls.Add(_wizardPage);
        contentFrame.Controls.Add(contentInner);
        outer.Controls.Add(contentFrame, 0, 1);

        var bottom = new TableLayoutPanel
        {
            ColumnCount = 2,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 10, 0, 0),
            BackColor = Color.Transparent,
        };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _diffLbl.AutoSize = true;
        _diffLbl.MaximumSize = new Size(480, 0);
        _diffLbl.ForeColor = UiTheme.Lilac;
        _diffLbl.Font = UiTheme.UiFont(8f);
        _diffLbl.Text = "";
        bottom.Controls.Add(_diffLbl, 0, 0);

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            WrapContents = false,
            BackColor = Color.Transparent,
        };
        var done = UiTheme.Btn("DONE", () =>
        {
            DialogResult = DialogResult.OK;
            Close();
        }, primary: true);
        buttons.Controls.Add(done);
        var redo = UiTheme.Compact(UiTheme.Btn("Redo", DoRedo));
        _tip.SetToolTip(redo, "Ctrl+Y");
        buttons.Controls.Add(redo);
        var undo = UiTheme.Compact(UiTheme.Btn("Undo", DoUndo));
        _tip.SetToolTip(undo, "Ctrl+Z");
        buttons.Controls.Add(undo);
        bottom.Controls.Add(buttons, 1, 0);
        outer.Controls.Add(bottom, 0, 2);
        AcceptButton = done;

        _status.Visible = false;
        Controls.Add(_status);

        LoadFromBuffer();
    }

    private static FlowLayoutPanel MakePageRoot(Panel page)
    {
        var root = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(14, 12, 14, 12),
            BackColor = UiTheme.Bg,
        };
        page.Controls.Add(root);
        return root;
    }

    private void LoadFromBuffer()
    {
        _status.Text = CareerStatsService.Summarize(_working);
        SyncWizardTargetsFromBuffer();
    }

    private void ShowDiff(StatsEditResult result)
    {
        _diffLbl.Text = result.Summary;
        _diffLbl.ForeColor = result.ChecksumsOk ? UiTheme.Lilac : Color.OrangeRed;
    }

    private void Notify()
    {
        CareerStatsService.EnsureCareerChecksums(_working);
        _status.Text = CareerStatsService.Summarize(_working);
        _onChanged();
    }
}
