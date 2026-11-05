namespace NFSU2CustomCarSaves;

internal sealed class MaxOutOptionsForm : Form
{
    private readonly RadioButton _perfInstall;
    private readonly RadioButton _perfOwn;
    private readonly RadioButton _uniqNone;
    private readonly RadioButton _uniqS3;
    private readonly RadioButton _uniqS4;
    private readonly RadioButton _uniqS5;

    public MaxOutPerfMode PerfMode { get; private set; } = MaxOutPerfMode.InstallExtreme;
    public MaxOutUniqueMode UniqueMode { get; private set; } = MaxOutUniqueMode.Stage3;

    public MaxOutOptionsForm()
    {
        Text = "MAX OUT";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(460, 400);
        UiTheme.ApplyForm(this);

        var host = new StarfieldControl(28, 70) { Animate = false, Dock = DockStyle.Fill };
        Controls.Add(host);

        var root = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(18, 16, 18, 56),
            BackColor = Color.Transparent,
        };
        host.Controls.Add(root);

        root.Controls.Add(new Label
        {
            Text = "MAX OUT options",
            AutoSize = true,
            Font = UiTheme.UiBold(12f),
            ForeColor = UiTheme.TextStrong,
            Margin = new Padding(0, 0, 0, 6),
        });
        root.Controls.Add(UiTheme.Muted(
            "Cosmetics on the car stay as-is. Shop + all parts get owned/unlocked either way."));

        root.Controls.Add(UiTheme.SectionHead("Performance parts"));
        var perfBox = MakeGroup();
        _perfInstall = MakeRadio("Install Extreme (best packages on the car)", true);
        _perfOwn = MakeRadio("Own all only (don't change what's installed)");
        perfBox.Controls.Add(_perfInstall);
        perfBox.Controls.Add(_perfOwn);
        root.Controls.Add(perfBox);

        root.Controls.Add(UiTheme.SectionHead("Performance uniques (career order)"));
        var uniqBox = MakeGroup();
        _uniqNone = MakeRadio("No uniques");
        _uniqS3 = MakeRadio("Stage 3 — Engine, Tires, Transmission", true);
        _uniqS4 = MakeRadio("Stage 4 — + ECU, Turbo, Brakes");
        _uniqS5 = MakeRadio("Stage 5 — + NOS, Weight, Suspension (all 9)");
        uniqBox.Controls.Add(_uniqNone);
        uniqBox.Controls.Add(_uniqS3);
        uniqBox.Controls.Add(_uniqS4);
        uniqBox.Controls.Add(_uniqS5);
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
            if (_uniqS5.Checked) UniqueMode = MaxOutUniqueMode.Stage5;
            else if (_uniqS4.Checked) UniqueMode = MaxOutUniqueMode.Stage4;
            else if (_uniqNone.Checked) UniqueMode = MaxOutUniqueMode.None;
            else UniqueMode = MaxOutUniqueMode.Stage3;
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
