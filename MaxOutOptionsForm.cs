namespace NFSU2CustomCarSaves;

internal sealed class MaxOutOptionsForm : Form
{
    private readonly RadioButton _perfInstall;
    private readonly RadioButton _perfOwn;
    private readonly RadioButton _uniqNone;
    private readonly RadioButton _uniqLegit;
    private readonly RadioButton _uniqAll;

    public MaxOutPerfMode PerfMode { get; private set; } = MaxOutPerfMode.InstallExtreme;
    public MaxOutUniqueMode UniqueMode { get; private set; } = MaxOutUniqueMode.Legit3;

    public MaxOutOptionsForm()
    {
        Text = "MAX OUT";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(420, 340);
        UiTheme.ApplyForm(this);

        var host = new StarfieldControl(28, 70) { Animate = false, Dock = DockStyle.Fill };
        Controls.Add(host);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(18, 16, 18, 14),
            BackColor = Color.Transparent,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        host.Controls.Add(root);

        root.Controls.Add(new Label
        {
            Text = "MAX OUT options",
            AutoSize = true,
            Font = UiTheme.UiBold(12f),
            ForeColor = UiTheme.TextStrong,
            Margin = new Padding(0, 0, 0, 6),
        });
        root.Controls.Add(UiTheme.Muted("Cosmetics stay on the car. Shop + all parts get owned/unlocked either way."));

        // Performance
        root.Controls.Add(UiTheme.SectionHead("Performance parts"));
        var perfBox = MakeGroup();
        _perfInstall = MakeRadio("Install Extreme (best packages on the car)", true);
        _perfOwn = MakeRadio("Own all only (don't change what's installed)");
        perfBox.Controls.Add(_perfInstall);
        perfBox.Controls.Add(_perfOwn);
        root.Controls.Add(perfBox);

        // Uniques
        root.Controls.Add(UiTheme.SectionHead("Performance uniques"));
        var uniqBox = MakeGroup();
        _uniqNone = MakeRadio("No uniques");
        _uniqLegit = MakeRadio("Legit uniques (3 in order: Weight → Trans → NOS)", true);
        _uniqAll = MakeRadio("All uniques");
        uniqBox.Controls.Add(_uniqNone);
        uniqBox.Controls.Add(_uniqLegit);
        uniqBox.Controls.Add(_uniqAll);
        root.Controls.Add(uniqBox);

        var btns = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 48,
            Padding = new Padding(12, 8, 12, 8),
            BackColor = Color.Transparent,
        };
        var apply = UiTheme.Btn("Apply", () =>
        {
            PerfMode = _perfOwn.Checked ? MaxOutPerfMode.OwnOnly : MaxOutPerfMode.InstallExtreme;
            if (_uniqAll.Checked) UniqueMode = MaxOutUniqueMode.All;
            else if (_uniqNone.Checked) UniqueMode = MaxOutUniqueMode.None;
            else UniqueMode = MaxOutUniqueMode.Legit3;
            DialogResult = DialogResult.OK;
            Close();
        }, primary: true);
        var cancel = UiTheme.Btn("Cancel", () =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        });
        btns.Controls.Add(UiTheme.Compact(apply));
        btns.Controls.Add(UiTheme.Compact(cancel));
        Controls.Add(btns);
        AcceptButton = apply;
        CancelButton = cancel;
    }

    private static Panel MakeGroup()
    {
        return new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0, 2, 0, 10),
            BackColor = Color.Transparent,
        };
    }

    private static RadioButton MakeRadio(string text, bool check = false)
    {
        return new RadioButton
        {
            Text = text,
            AutoSize = true,
            Checked = check,
            ForeColor = UiTheme.Text,
            Font = UiTheme.UiFont(9f),
            Margin = new Padding(4, 2, 4, 2),
            BackColor = Color.Transparent,
        };
    }
}
