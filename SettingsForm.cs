namespace NFSU2CustomCarSaves;

/// <summary>Tune / MaxOut-style settings dialog (Starfield + SectionHead + Compact).</summary>
internal sealed class SettingsForm : Form
{
    private readonly CheckBox _loadLibs;
    private readonly CheckBox _askLibs;
    private readonly CheckBox _showGuide;
    private readonly ComboBox _themePick;
    private readonly NumericUpDown _accentR;
    private readonly NumericUpDown _accentG;
    private readonly NumericUpDown _accentB;
    private readonly Panel _accentPreview;
    private readonly Action? _onLibrariesSeeded;
    private readonly Label _seedStatus;

    public SettingsForm(Action? onLibrariesSeeded = null)
    {
        _onLibrariesSeeded = onLibrariesSeeded;
        var s = AppSettings.Load();

        Text = "Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(460, 480);
        UiTheme.ApplyForm(this);

        var host = new StarfieldControl(32, 88) { Animate = false, Dock = DockStyle.Fill };
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
            Text = "App settings",
            AutoSize = true,
            Font = UiTheme.UiBold(12f),
            ForeColor = UiTheme.TextStrong,
            Margin = new Padding(0, 0, 0, 6),
            BackColor = Color.Transparent,
        });
        root.Controls.Add(UiTheme.SectionHead("Libraries"));
        _loadLibs = MakeCheck("Load default libraries (cars / liveries / dynos)", s.LoadDefaultLibraries);
        _askLibs = MakeCheck("Ask at startup whether to load default libraries", s.AskLoadDefaultLibraries);
        root.Controls.Add(_loadLibs);
        root.Controls.Add(_askLibs);

        _seedStatus = new Label
        {
            Text = "",
            AutoSize = true,
            Font = UiTheme.UiFont(8.5f),
            ForeColor = UiTheme.TextMuted,
            MaximumSize = new Size(420, 0),
            Margin = new Padding(0, 4, 0, 12),
            BackColor = Color.Transparent,
        };
        var seedNow = UiTheme.Compact(UiTheme.Btn("Load default libraries now", () =>
        {
            try
            {
                string status = AppPaths.SeedDefaultLibraries();
                _seedStatus.Text = status;
                _seedStatus.ForeColor = UiTheme.Success;
                _onLibrariesSeeded?.Invoke();
            }
            catch (Exception ex)
            {
                _seedStatus.Text = ex.Message;
                _seedStatus.ForeColor = UiTheme.Warning;
            }
        }, info: true));
        seedNow.Margin = new Padding(0, 4, 0, 2);
        root.Controls.Add(seedNow);
        root.Controls.Add(_seedStatus);

        root.Controls.Add(UiTheme.SectionHead("Startup"));
        _showGuide = MakeCheck("Show Guide · Credits popup at launch", s.ShowGuideOnStartup);
        root.Controls.Add(_showGuide);

        root.Controls.Add(UiTheme.SectionHead("Theme"));
        _themePick = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 220,
            Margin = new Padding(0, 2, 0, 6),
        };
        UiTheme.StyleCombo(_themePick);
        _themePick.Items.AddRange(["NFSU2Classic", "Dark", "Light"]);
        string theme = s.UiThemeName;
        if (theme.Equals("NFSU2 Classic", StringComparison.OrdinalIgnoreCase)) theme = "NFSU2Classic";
        int ti = _themePick.Items.IndexOf(theme);
        _themePick.SelectedIndex = ti >= 0 ? ti : 1;
        root.Controls.Add(_themePick);

        Color accent = Color.FromArgb(s.UiAccentArgb);
        var accentRow = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 8),
            BackColor = Color.Transparent,
        };
        accentRow.Controls.Add(new Label
        {
            Text = "Accent RGB",
            AutoSize = true,
            ForeColor = UiTheme.Text,
            Font = UiTheme.UiFont(9f),
            Margin = new Padding(0, 6, 8, 0),
            BackColor = Color.Transparent,
        });
        _accentR = MakeByteNud(accent.R);
        _accentG = MakeByteNud(accent.G);
        _accentB = MakeByteNud(accent.B);
        _accentR.ValueChanged += (_, _) => RefreshAccentPreview();
        _accentG.ValueChanged += (_, _) => RefreshAccentPreview();
        _accentB.ValueChanged += (_, _) => RefreshAccentPreview();
        accentRow.Controls.Add(_accentR);
        accentRow.Controls.Add(_accentG);
        accentRow.Controls.Add(_accentB);
        _accentPreview = new Panel
        {
            Width = 28,
            Height = 22,
            Margin = new Padding(8, 4, 0, 0),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = accent,
        };
        accentRow.Controls.Add(_accentPreview);
        root.Controls.Add(accentRow);

        var btns = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 48,
            Padding = new Padding(12, 8, 12, 8),
            BackColor = UiTheme.BgDeep,
        };
        var save = UiTheme.Compact(UiTheme.Btn("Save", SaveAndClose, primary: true));
        var cancel = UiTheme.Compact(UiTheme.Btn("Cancel", () =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }));
        btns.Controls.Add(save);
        btns.Controls.Add(cancel);
        Controls.Add(btns);
        AcceptButton = save;
        CancelButton = cancel;
    }

    private void RefreshAccentPreview()
    {
        _accentPreview.BackColor = Color.FromArgb((int)_accentR.Value, (int)_accentG.Value, (int)_accentB.Value);
    }

    private static NumericUpDown MakeByteNud(int v) => new()
    {
        Minimum = 0,
        Maximum = 255,
        Value = Math.Clamp(v, 0, 255),
        Width = 52,
        Font = UiTheme.UiFont(9f),
        BackColor = UiTheme.Elevated,
        ForeColor = UiTheme.TextStrong,
        BorderStyle = BorderStyle.FixedSingle,
        Margin = new Padding(0, 2, 4, 0),
    };

    private void SaveAndClose()
    {
        Color accent = Color.FromArgb((int)_accentR.Value, (int)_accentG.Value, (int)_accentB.Value);
        var s = new AppSettings
        {
            LoadDefaultLibraries = _loadLibs.Checked,
            AskLoadDefaultLibraries = _askLibs.Checked,
            ShowGuideOnStartup = _showGuide.Checked,
            UiThemeName = _themePick.SelectedItem?.ToString() ?? "Dark",
            UiAccentArgb = accent.ToArgb(),
        };
        s.Save();
        UiTheme.ApplyFromSettings(s);
        DialogResult = DialogResult.OK;
        Close();
    }

    private static CheckBox MakeCheck(string text, bool check)
    {
        return new CheckBox
        {
            Text = text,
            Checked = check,
            AutoSize = true,
            ForeColor = UiTheme.Text,
            Font = UiTheme.UiFont(9f),
            Margin = new Padding(4, 2, 4, 6),
            BackColor = Color.Transparent,
        };
    }
}
