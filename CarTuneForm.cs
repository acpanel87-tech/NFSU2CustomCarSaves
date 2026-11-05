namespace NFSU2CustomCarSaves;

/// <summary>Tuning UI — Performance tab (category list + part checkboxes); Dyno + Livery tabs. Galaxy visual shell only.</summary>
public sealed class CarTuneForm : Form
{
    private readonly byte[] _working;
    private readonly int _slot;
    private readonly string _saveName;
    private readonly Action _onChanged;

    private readonly Label _carName = new();
    private readonly Label _stateBadge = new();
    private readonly ListBox _catList = new();
    private readonly Panel _partsHost = new();
    private readonly FlowLayoutPanel _partsPanel = new();
    private readonly Label _perfSummary = new();

    private readonly ListBox _presets = new();
    private readonly Label _presetNote = new();
    private Button _deletePresetBtn = null!;
    private CheckBox _includeUpgrades = null!;
    private CheckBox _copyBody = null!;
    private readonly ComboBox _eventPick = new();
    private readonly Dictionary<int, TrackBar> _fieldBars = new();
    private readonly Dictionary<int, Label> _fieldVals = new();
    private readonly TrackBar[] _ecuBars = new TrackBar[9];
    private readonly TrackBar[] _turboBars = new TrackBar[9];
    private readonly Panel _perfPage = new();
    private readonly Panel _dynoPage = new();
    private readonly Panel _liveryPage = new();
    private readonly GlowButton _tabPerf = new();
    private readonly GlowButton _tabDyno = new();
    private readonly GlowButton _tabLivery = new();
    private readonly ListBox _liveries = new();
    private readonly Label _liveryNote = new();
    private readonly Label _liverySlotStatus = new();
    private Button _deleteLiveryBtn = null!;
    private CheckBox _liverySameCarOnly = null!;
    private ComboBox _liveryMode = null!;
    private uint _carHash;
    private bool _silent;

    public CarTuneForm(byte[] working, int slot, Action onChanged, string? saveName = null)
    {
        _working = working;
        _slot = slot;
        _saveName = string.IsNullOrWhiteSpace(saveName) ? "save" : saveName.Trim();
        _onChanged = onChanged;
        _carHash = GarageDatService.ReadSlot(working, slot).CarHash;

        UiTheme.ApplyForm(this);
        Text = $"Car slot {slot + 1} — Tuning";
        Width = 920;
        Height = 720;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        MinimumSize = new Size(820, 600);

        var host = new StarfieldControl(55, 61) { Animate = false, Dock = DockStyle.Fill };
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

        // Header: name + badge + MAX OUT
        var head = new TableLayoutPanel
        {
            ColumnCount = 3,
            AutoSize = true,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 8),
            BackColor = Color.Transparent,
        };
        head.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        head.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        head.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _carName.AutoSize = true;
        _carName.Font = new Font("Segoe UI Semibold", 16f, FontStyle.Bold | FontStyle.Italic);
        _carName.ForeColor = UiTheme.TextStrong;
        _stateBadge.AutoSize = true;
        _stateBadge.Font = UiTheme.UiBold(9f);
        _stateBadge.Padding = new Padding(10, 5, 10, 5);
        _stateBadge.BackColor = UiTheme.Elevated;
        _stateBadge.ForeColor = UiTheme.Primary;
        _stateBadge.Margin = new Padding(8, 4, 8, 0);
        var maxOut = UiTheme.Btn("MAX OUT", () =>
        {
            CarTuneService.FullyTune(_working, _slot);
            RefreshPerformance();
            _onChanged();
        }, primary: true);
        maxOut.Margin = new Padding(0, 0, 0, 0);
        head.Controls.Add(_carName, 0, 0);
        head.Controls.Add(_stateBadge, 1, 0);
        head.Controls.Add(maxOut, 2, 0);
        outer.Controls.Add(head, 0, 0);

        // Galaxy tabs + bordered content
        var tabHost = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
            BackColor = Color.Transparent,
        };
        tabHost.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
        tabHost.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var tabBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = Color.Transparent,
        };
        StyleTabButton(_tabPerf, "PERFORMANCE", true);
        StyleTabButton(_tabDyno, "DYNO", false);
        StyleTabButton(_tabLivery, "LIVERY", false);
        _tabPerf.Click += (_, _) => ShowTab(0);
        _tabDyno.Click += (_, _) => ShowTab(1);
        _tabLivery.Click += (_, _) => ShowTab(2);
        tabBar.Controls.Add(_tabPerf);
        tabBar.Controls.Add(_tabDyno);
        tabBar.Controls.Add(_tabLivery);
        tabHost.Controls.Add(tabBar, 0, 0);

        var contentFrame = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(1),
            BackColor = Color.FromArgb(0x4A, 0x44, 0x9A),
            Margin = new Padding(0),
        };
        var contentInner = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Bg };
        foreach (var page in new[] { _perfPage, _dynoPage, _liveryPage })
        {
            page.Dock = DockStyle.Fill;
            page.BackColor = UiTheme.Bg;
            page.Padding = new Padding(10);
            page.Visible = false;
        }
        _perfPage.Visible = true;
        BuildPerformanceInto(_perfPage);
        BuildDynoInto(_dynoPage);
        BuildLiveryInto(_liveryPage);
        contentInner.Controls.Add(_liveryPage);
        contentInner.Controls.Add(_dynoPage);
        contentInner.Controls.Add(_perfPage);
        contentFrame.Controls.Add(contentInner);
        tabHost.Controls.Add(contentFrame, 0, 1);
        outer.Controls.Add(tabHost, 0, 1);

        var bottom = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 10, 0, 0),
            BackColor = Color.Transparent,
        };
        bottom.Controls.Add(UiTheme.Btn("DONE", () => { DialogResult = DialogResult.OK; Close(); }, primary: true));
        outer.Controls.Add(bottom, 0, 2);

        if (!CarTuneService.Read(working, slot).IsOccupied)
        {
            MessageBox.Show(this,
                "This slot has no car. Unlock still works; tuning once a car is in the slot.",
                Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        RefreshPerformance();
        LoadDynoEditorFromSlot();
        ReloadPresets();
        ReloadLiveries();
    }

    private static void StyleTabButton(GlowButton b, string text, bool active)
    {
        b.Text = text;
        b.AutoSize = false;
        b.Size = new Size(140, 38);
        b.Margin = new Padding(0, 0, 6, 0);
        ApplyTabVisual(b, active);
    }

    private static void ApplyTabVisual(GlowButton b, bool active)
    {
        b.Kind = active ? GlowKind.Primary : GlowKind.Default;
    }

    private void ShowTab(int index)
    {
        ApplyTabVisual(_tabPerf, index == 0);
        ApplyTabVisual(_tabDyno, index == 1);
        ApplyTabVisual(_tabLivery, index == 2);
        _perfPage.Visible = index == 0;
        _dynoPage.Visible = index == 1;
        _liveryPage.Visible = index == 2;
        if (index == 0) _perfPage.BringToFront();
        else if (index == 1) _dynoPage.BringToFront();
        else _liveryPage.BringToFront();
    }

    private void BuildPerformanceInto(Panel page)
    {

        var split = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            BackColor = UiTheme.Bg,
        };
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240f));
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        split.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        split.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        _perfSummary.AutoSize = true;
        _perfSummary.ForeColor = UiTheme.TextMuted;
        _perfSummary.Font = UiTheme.UiFont(8.5f);
        _perfSummary.Margin = new Padding(0, 0, 0, 6);
        split.SetColumnSpan(_perfSummary, 2);
        split.Controls.Add(_perfSummary, 0, 0);

        UiTheme.StyleList(_catList);
        _catList.Dock = DockStyle.Fill;
        _catList.IntegralHeight = false;
        _catList.SelectedIndexChanged += (_, _) => RebuildPartChecks();
        var catCard = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(1),
            BackColor = UiTheme.Border,
            Margin = new Padding(0, 0, 10, 0),
        };
        var catInner = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Surface, Padding = new Padding(4) };
        catInner.Controls.Add(_catList);
        catCard.Controls.Add(catInner);
        split.Controls.Add(catCard, 0, 1);

        _partsHost.Dock = DockStyle.Fill;
        _partsHost.Padding = new Padding(1);
        _partsHost.BackColor = UiTheme.Border;
        var partsInner = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Surface, Padding = new Padding(10), AutoScroll = true };
        _partsPanel.Dock = DockStyle.Top;
        _partsPanel.AutoSize = true;
        _partsPanel.FlowDirection = FlowDirection.TopDown;
        _partsPanel.WrapContents = false;
        _partsPanel.Width = 600;
        partsInner.Controls.Add(_partsPanel);
        _partsHost.Controls.Add(partsInner);
        split.Controls.Add(_partsHost, 1, 1);

        page.Controls.Add(split);
    }

    private void RefreshPerformance()
    {
        _silent = true;
        var s = CarTuneService.Read(_working, _slot);
        _carHash = GarageDatService.ReadSlot(_working, _slot).CarHash;
        _carName.Text = string.IsNullOrWhiteSpace(s.CarName) ? $"Slot {_slot + 1}" : s.CarName;
        _stateBadge.Text = s.IsLocked ? "Locked" : s.IsOccupied ? "Filled" : "Open (free)";
        _stateBadge.ForeColor = s.IsLocked ? UiTheme.Warning : UiTheme.Primary;

        int keep = _catList.SelectedIndex;
        _catList.Items.Clear();
        for (int i = 0; i < CarTuneService.Categories.Length; i++)
        {
            var c = CarTuneService.Categories[i];
            int on = i < s.CategoryInstalled.Length ? s.CategoryInstalled[i] : 0;
            int tot = c.Indices.Length;
            _catList.Items.Add($"{c.LabelEn}:  {on}/{tot}");
        }
        if (_catList.Items.Count > 0)
            _catList.SelectedIndex = keep >= 0 && keep < _catList.Items.Count ? keep : 0;

        _perfSummary.Text =
            $"Performance flags  {s.PerfInstalled}/{s.PerfTotal}   ·   Unique  {s.UniqueOn}/{s.UniqueTotal}   ·   Shop unlocks  {s.UnlocksOn}/{s.UnlocksTotal}   ·   {CarTuneService.LevelLabel(s.EstimatedLevel)}";

        RebuildPartChecks();
        _silent = false;
    }

    private void RebuildPartChecks()
    {
        _partsPanel.Controls.Clear();
        int cat = _catList.SelectedIndex;
        if (cat < 0 || cat >= CarTuneService.Categories.Length) return;

        var info = CarTuneService.Categories[cat];
        _partsPanel.Controls.Add(new Label
        {
            Text = info.LabelEn,
            AutoSize = true,
            Font = UiTheme.UiBold(12f),
            ForeColor = UiTheme.Primary,
            Margin = new Padding(0, 0, 0, 8),
        });

        foreach (var part in CarTuneService.PartsInCategory(cat))
        {
            var cb = new CheckBox
            {
                Text = $"{part.Label}  ({part.LevelName})",
                AutoSize = true,
                ForeColor = UiTheme.Text,
                Font = UiTheme.UiFont(9.5f),
                Checked = CarTuneService.IsPartOn(_working, _slot, part.Index),
                Margin = new Padding(0, 2, 0, 2),
                Cursor = Cursors.Hand,
            };
            int idx = part.Index;
            cb.CheckedChanged += (_, _) =>
            {
                if (_silent) return;
                // Raw toggles: no conflict wipe when checking individually
                CarTuneService.SetPartOn(_working, _slot, idx, cb.Checked, respectConflicts: false);
                _onChanged();
                RefreshCategoryCountsOnly();
            };
            _partsPanel.Controls.Add(cb);
        }

        _partsPanel.Controls.Add(new Label
        {
            Text = $"Unique {info.LabelEn}",
            AutoSize = true,
            Font = UiTheme.UiBold(11f),
            ForeColor = UiTheme.Lilac,
            Margin = new Padding(0, 16, 0, 8),
        });

        if (info.UniqueIndex >= 0)
        {
            var u = new CheckBox
            {
                Text = "Unique flag",
                AutoSize = true,
                ForeColor = UiTheme.Text,
                Font = UiTheme.UiFont(9.5f),
                Checked = CarTuneService.IsUniqueOn(_working, _slot, info.UniqueIndex),
                Margin = new Padding(0, 2, 0, 2),
                Cursor = Cursors.Hand,
            };
            int ui = info.UniqueIndex;
            u.CheckedChanged += (_, _) =>
            {
                if (_silent) return;
                CarTuneService.SetUniqueOn(_working, _slot, ui, u.Checked);
                _onChanged();
                RefreshCategoryCountsOnly();
            };
            _partsPanel.Controls.Add(u);
        }
    }

    private void RefreshCategoryCountsOnly()
    {
        var s = CarTuneService.Read(_working, _slot);
        int keep = _catList.SelectedIndex;
        _silent = true;
        for (int i = 0; i < CarTuneService.Categories.Length && i < _catList.Items.Count; i++)
        {
            var c = CarTuneService.Categories[i];
            int on = i < s.CategoryInstalled.Length ? s.CategoryInstalled[i] : 0;
            _catList.Items[i] = $"{c.LabelEn}:  {on}/{c.Indices.Length}";
        }
        if (keep >= 0 && keep < _catList.Items.Count)
            _catList.SelectedIndex = keep;
        _perfSummary.Text =
            $"Performance flags  {s.PerfInstalled}/{s.PerfTotal}   ·   Unique  {s.UniqueOn}/{s.UniqueTotal}   ·   Shop unlocks  {s.UnlocksOn}/{s.UnlocksTotal}   ·   {CarTuneService.LevelLabel(s.EstimatedLevel)}";
        _silent = false;
    }

    private void BuildDynoInto(Panel page)
    {
        var root = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = UiTheme.Bg,
        };
        page.Controls.Add(root);

        root.Controls.Add(UiTheme.SectionHead("Dyno presets"));
        root.Controls.Add(UiTheme.Muted(
            "Only presets for this car. user created = saved by you. Vinyl/livery is never overwritten."));

        UiTheme.StyleList(_presets);
        _presets.Width = 700;
        _presets.Height = 150;
        _presets.IntegralHeight = false;
        _presets.SelectedIndexChanged += (_, _) =>
        {
            if (_presets.SelectedItem is TunePreset p)
                _presetNote.Text =
                    $"{p.CarName} · from {p.SaveName}  ·  {p.CarHash}" +
                    (p.IsUserCreated ? "  ·  user created" : "") +
                    $"\n{p.AeroNote}\nApply to any slot — vinyl/livery stays.";
            else
                _presetNote.Text = "";
            SyncPartsChecks();
            SyncDeleteButton();
        };
        _presets.DoubleClick += (_, _) =>
        {
            try { ApplySelectedPreset(); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Dyno", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        };
        var presetBorder = new Panel
        {
            Width = 710,
            Height = 158,
            Padding = new Padding(1),
            BackColor = UiTheme.Border,
            Margin = new Padding(0, 0, 0, 6),
        };
        var presetInner = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Surface, Padding = new Padding(4) };
        _presets.Dock = DockStyle.Fill;
        presetInner.Controls.Add(_presets);
        presetBorder.Controls.Add(presetInner);
        root.Controls.Add(presetBorder);

        _presetNote.AutoSize = true;
        _presetNote.MaximumSize = new Size(700, 0);
        _presetNote.ForeColor = UiTheme.TextMuted;
        _presetNote.Font = UiTheme.UiFont(8.5f);
        root.Controls.Add(_presetNote);

        _includeUpgrades = new CheckBox
        {
            Text = "Apply required parts (only for presets marked +parts)",
            Checked = true,
            AutoSize = true,
            ForeColor = UiTheme.Text,
            Margin = new Padding(0, 4, 0, 2),
        };
        _copyBody = new CheckBox
        {
            Text = "Also copy bumper/spoiler/widebody (same car only — not vinyl)",
            Checked = false,
            AutoSize = true,
            ForeColor = UiTheme.TextMuted,
            Margin = new Padding(0, 2, 0, 6),
        };
        root.Controls.Add(_includeUpgrades);
        root.Controls.Add(_copyBody);

        var presetBtns = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = new Padding(0, 4, 0, 12) };
        presetBtns.Controls.Add(UiTheme.Btn("Apply dyno", ApplySelectedPreset, primary: true));
        presetBtns.Controls.Add(UiTheme.Btn("Save dyno to library", () =>
        {
            var p = TunePresetService.HarvestFromSlot(_working, _slot, _saveName);
            ReloadPresets();
            SelectPreset(p);
            MessageBox.Show(this, $"Saved to library (user created):\n{p.DisplayLabel}", "Dyno library",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }));
        _deletePresetBtn = UiTheme.Btn("Delete from library", DeleteSelectedPreset, danger: true);
        _deletePresetBtn.Enabled = false;
        presetBtns.Controls.Add(_deletePresetBtn);
        presetBtns.Controls.Add(UiTheme.Btn("Reload", () => ReloadPresets()));
        root.Controls.Add(presetBtns);

        root.Controls.Add(UiTheme.SectionHead("Edit setup"));
        root.Controls.Add(UiTheme.Muted("Circuit · Drag · Drift · Street X · URL. Bytes 0–200 (100 = stock midpoint for ECU/turbo/gears)."));

        _eventPick.DropDownStyle = ComboBoxStyle.DropDownList;
        _eventPick.Width = 200;
        UiTheme.StyleCombo(_eventPick);
        foreach (string n in DynoService.EventNames) _eventPick.Items.Add(n);
        _eventPick.SelectedIndex = 0;
        _eventPick.SelectedIndexChanged += (_, _) =>
        {
            if (_silent) return;
            LoadDynoEditorFromSlot();
        };
        root.Controls.Add(_eventPick);

        var editBtns = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = new Padding(0, 6, 0, 8) };
        editBtns.Controls.Add(UiTheme.Btn("Reset event → 100", () =>
        {
            var z = new byte[NFSU2CustomCarSaves.Layout.DynoSetupSize];
            Array.Fill(z, (byte)100);
            DynoService.WriteEvent(_working, _slot, _eventPick.SelectedIndex, z);
            LoadDynoEditorFromSlot();
            _onChanged();
        }));
        editBtns.Controls.Add(UiTheme.Btn("Copy all events", () =>
            DynoService.CopyToClipboard(DynoService.Export(_working, _slot, _saveName))));
        editBtns.Controls.Add(UiTheme.Btn("Paste all events", () =>
        {
            var pkg = DynoService.PasteFromClipboard()
                      ?? throw new InvalidOperationException("No dyno in clipboard.");
            DynoService.Apply(_working, _slot, pkg);
            LoadDynoEditorFromSlot();
            _onChanged();
        }));
        root.Controls.Add(editBtns);

        var fieldsHost = new FlowLayoutPanel
        {
            Width = 720,
            AutoSize = true,
            WrapContents = true,
            Margin = new Padding(0, 4, 0, 8),
        };
        foreach (var (label, idx) in DynoService.SetupFields)
            fieldsHost.Controls.Add(MakeFieldEditor(label, idx));
        root.Controls.Add(fieldsHost);

        root.Controls.Add(UiTheme.SectionHead("ECU curve (9 RPM points)"));
        root.Controls.Add(BuildCurveRow(_ecuBars, 23));
        root.Controls.Add(UiTheme.SectionHead("Turbo curve (9 RPM points)"));
        root.Controls.Add(BuildCurveRow(_turboBars, 32));
    }

    private Control MakeFieldEditor(string label, int byteIndex)
    {
        var panel = new Panel { Width = 340, Height = 48, Margin = new Padding(0, 0, 12, 4) };
        var caption = new Label
        {
            Text = label,
            AutoSize = true,
            ForeColor = UiTheme.TextMuted,
            Font = UiTheme.UiFont(8f),
            Location = new Point(0, 0),
        };
        var val = new Label
        {
            AutoSize = true,
            ForeColor = UiTheme.Primary,
            Font = UiTheme.UiBold(8.5f),
            Location = new Point(130, 0),
            Text = "100",
        };
        var bar = new TrackBar
        {
            Minimum = 0,
            Maximum = 200,
            TickFrequency = 25,
            Width = 320,
            Height = 28,
            Location = new Point(0, 16),
            BackColor = UiTheme.Bg,
            Value = 100,
        };
        int idx = byteIndex;
        bar.ValueChanged += (_, _) =>
        {
            val.Text = bar.Value.ToString();
            if (_silent) return;
            WriteField(idx, (byte)bar.Value);
        };
        _fieldBars[idx] = bar;
        _fieldVals[idx] = val;
        panel.Controls.Add(caption);
        panel.Controls.Add(val);
        panel.Controls.Add(bar);
        return panel;
    }

    private FlowLayoutPanel BuildCurveRow(TrackBar[] bars, int startIndex)
    {
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = new Padding(0, 0, 0, 10) };
        for (int i = 0; i < 9; i++)
        {
            int bi = startIndex + i;
            var col = new Panel { Width = 70, Height = 90, Margin = new Padding(2) };
            var lbl = new Label
            {
                Text = $"P{i + 1}",
                AutoSize = true,
                ForeColor = UiTheme.TextMuted,
                Font = UiTheme.UiFont(7.5f),
            };
            var bar = new TrackBar
            {
                Orientation = Orientation.Vertical,
                Minimum = 0,
                Maximum = 200,
                TickStyle = TickStyle.None,
                Height = 70,
                Width = 40,
                Location = new Point(12, 16),
                BackColor = UiTheme.Bg,
                Value = 100,
            };
            bar.ValueChanged += (_, _) =>
            {
                if (_silent) return;
                WriteField(bi, (byte)bar.Value);
            };
            bars[i] = bar;
            col.Controls.Add(lbl);
            col.Controls.Add(bar);
            row.Controls.Add(col);
        }
        return row;
    }

    private void WriteField(int byteIndex, byte value)
    {
        int ev = Math.Max(0, _eventPick.SelectedIndex);
        var setup = DynoService.ReadEvent(_working, _slot, ev);
        setup[byteIndex] = value;
        DynoService.WriteEvent(_working, _slot, ev, setup);
        _onChanged();
    }

    private void LoadDynoEditorFromSlot()
    {
        try
        {
            _silent = true;
            int ev = Math.Max(0, _eventPick.SelectedIndex);
            var setup = DynoService.ReadEvent(_working, _slot, ev);
            foreach (var kv in _fieldBars)
            {
                int v = setup[kv.Key];
                kv.Value.Value = Math.Clamp(v, 0, 200);
                if (_fieldVals.TryGetValue(kv.Key, out var lbl))
                    lbl.Text = v.ToString();
            }
            for (int i = 0; i < 9; i++)
            {
                if (_ecuBars[i] != null)
                    _ecuBars[i].Value = Math.Clamp((int)setup[23 + i], 0, 200);
                if (_turboBars[i] != null)
                    _turboBars[i].Value = Math.Clamp((int)setup[32 + i], 0, 200);
            }
        }
        catch { /* empty */ }
        finally { _silent = false; }
    }

    private void ReloadPresets()
    {
        _presets.Items.Clear();
        string want = _carHash == 0 ? "" : _carHash.ToString("X8");
        foreach (var p in TunePresetService.LoadDynoPresets(refresh: true))
        {
            if (_carHash == 0) continue;
            if (string.IsNullOrWhiteSpace(p.CarHash)) continue;
            if (!p.CarHash.Equals(want, StringComparison.OrdinalIgnoreCase)) continue;
            _presets.Items.Add(p);
        }
        if (_presets.Items.Count > 0)
            _presets.SelectedIndex = 0;
        else
            _presetNote.Text = _carHash == 0
                ? "No car in this slot — open a filled slot to see dyno presets."
                : $"No dyno presets for this car yet ({_carName.Text}). Open a save with this model, or use Save dyno to library.";
        SyncPartsChecks();
        SyncDeleteButton();
    }

    private void SelectPreset(TunePreset want)
    {
        for (int i = 0; i < _presets.Items.Count; i++)
        {
            if (_presets.Items[i] is TunePreset p &&
                (p.Id == want.Id ||
                 string.Equals(p.DynoHex, want.DynoHex, StringComparison.OrdinalIgnoreCase)))
            {
                _presets.SelectedIndex = i;
                return;
            }
        }
    }

    private void SyncPartsChecks()
    {
        bool needs = _presets.SelectedItem is TunePreset p && p.NeedsParts && p.HasUpgrades;
        if (_includeUpgrades != null)
        {
            _includeUpgrades.Enabled = needs;
            _includeUpgrades.Checked = needs;
        }
        if (_copyBody != null)
        {
            _copyBody.Enabled = needs;
            if (!needs) _copyBody.Checked = false;
        }
    }

    private void SyncDeleteButton()
    {
        if (_deletePresetBtn == null) return;
        _deletePresetBtn.Enabled = _presets.SelectedItem is TunePreset p && p.CanDeleteFromLibrary;
    }

    private void DeleteSelectedPreset()
    {
        if (_presets.SelectedItem is not TunePreset p)
            throw new InvalidOperationException("Select a dyno preset first.");
        if (!p.CanDeleteFromLibrary)
            throw new InvalidOperationException("Included presets ship with the program and cannot be deleted.");

        var ask = MessageBox.Show(this,
            $"Delete this user/library preset?\n\n{p.DisplayLabel}",
            "Delete from library",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (ask != DialogResult.Yes) return;

        TunePresetService.DeleteFromLibrary(p);
        ReloadPresets();
        MessageBox.Show(this, "Removed from your library.", "Dyno library",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ApplySelectedPreset()
    {
        if (_presets.SelectedItem is not TunePreset p)
            throw new InvalidOperationException("Select a dyno preset in the list.");
        bool upgrades = _includeUpgrades?.Checked ?? true;
        bool body = _copyBody?.Checked ?? false;
        string what = TunePresetService.ApplyDyno(_working, _slot, p, upgrades, body);
        LoadDynoEditorFromSlot();
        RefreshPerformance();
        _onChanged();
        MessageBox.Show(this, $"Applied: {p.DisplayLabel}\n({what})", "Dyno",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void BuildLiveryInto(Panel page)
    {
        var root = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = UiTheme.Bg,
        };
        page.Controls.Add(root);

        root.Controls.Add(UiTheme.SectionHead("Livery library"));
        root.Controls.Add(UiTheme.Muted(
            "From DYNO SAVES + your library. Uncheck the filter to browse every car’s liveries."));

        _liverySlotStatus.AutoSize = true;
        _liverySlotStatus.ForeColor = UiTheme.Primary;
        _liverySlotStatus.Font = UiTheme.UiBold(9f);
        _liverySlotStatus.Margin = new Padding(0, 0, 0, 6);
        root.Controls.Add(_liverySlotStatus);

        _liverySameCarOnly = new CheckBox
        {
            Text = "Show only liveries for this car",
            Checked = true,
            AutoSize = true,
            ForeColor = UiTheme.Text,
            Margin = new Padding(0, 0, 0, 6),
        };
        _liverySameCarOnly.CheckedChanged += (_, _) => ReloadLiveries();
        root.Controls.Add(_liverySameCarOnly);

        UiTheme.StyleList(_liveries);
        _liveries.Width = 700;
        _liveries.Height = 280;
        _liveries.IntegralHeight = false;
        _liveries.SelectedIndexChanged += (_, _) =>
        {
            if (_liveries.SelectedItem is LiveryPreset p)
            {
                bool same = p.CarHash.Equals(_carHash.ToString("X8"), StringComparison.OrdinalIgnoreCase);
                string car = string.IsNullOrWhiteSpace(p.CarName) ? p.CarHash : p.CarName;
                string origin = p.IsUserCreated ? "user created  ·  " : "";
                _liveryNote.Text = same
                    ? $"{origin}{p.Note}"
                    : $"{car}  ·  {origin}{p.Note}  ·  different car";
            }
            else
                _liveryNote.Text = "";
            SyncLiveryDelete();
        };
        _liveries.DoubleClick += (_, _) =>
        {
            try { ApplySelectedLivery(); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Livery", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        };

        var listBorder = new Panel
        {
            Width = 710,
            Height = 288,
            Padding = new Padding(1),
            BackColor = UiTheme.Border,
            Margin = new Padding(0, 0, 0, 6),
        };
        var listInner = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Surface, Padding = new Padding(4) };
        _liveries.Dock = DockStyle.Fill;
        listInner.Controls.Add(_liveries);
        listBorder.Controls.Add(listInner);
        root.Controls.Add(listBorder);

        _liveryNote.AutoSize = true;
        _liveryNote.MaximumSize = new Size(700, 0);
        _liveryNote.ForeColor = UiTheme.TextMuted;
        _liveryNote.Font = UiTheme.UiFont(8.5f);
        _liveryNote.Margin = new Padding(0, 0, 0, 8);
        root.Controls.Add(_liveryNote);

        var modeRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 2, 0, 6) };
        modeRow.Controls.Add(new Label
        {
            Text = "Apply mode:",
            AutoSize = true,
            ForeColor = UiTheme.TextMuted,
            Margin = new Padding(0, 6, 8, 0),
        });
        _liveryMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 380 };
        UiTheme.StyleCombo(_liveryMode);
        _liveryMode.Items.Add("Replace — full graphics");
        _liveryMode.Items.Add("Merge empty — fill empty slots only");
        _liveryMode.Items.Add("Merge overlay — library overwrites set layers");
        _liveryMode.SelectedIndex = 0;
        modeRow.Controls.Add(_liveryMode);
        root.Controls.Add(modeRow);

        var btns = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = new Padding(0, 4, 0, 8) };
        btns.Controls.Add(UiTheme.Btn("Apply livery", ApplySelectedLivery, primary: true));
        btns.Controls.Add(UiTheme.Btn("Save livery to library", () =>
        {
            var p = LiveryService.SaveToLibrary(_working, _slot, _saveName);
            ReloadLiveries();
            SelectLivery(p);
            MessageBox.Show(this, $"Saved (user created):\n{p.DisplayLabel}", "Livery library",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }));
        _deleteLiveryBtn = UiTheme.Btn("Delete from library", DeleteSelectedLivery, danger: true);
        _deleteLiveryBtn.Enabled = false;
        btns.Controls.Add(_deleteLiveryBtn);
        btns.Controls.Add(UiTheme.Btn("Reload", () => ReloadLiveries()));
        root.Controls.Add(btns);
    }

    private void ReloadLiveries()
    {
        _liverySlotStatus.Text = "This car: " + LiveryService.SummarizeSlot(_working, _slot);
        string want = _carHash == 0 ? "" : _carHash.ToString("X8");
        bool sameOnly = _liverySameCarOnly?.Checked != false;
        int keep = _liveries.SelectedIndex;
        _liveries.Items.Clear();
        foreach (var p in LiveryService.LoadAll(refresh: true))
        {
            if (sameOnly)
            {
                if (_carHash == 0) continue;
                if (string.IsNullOrWhiteSpace(p.CarHash)) continue;
                if (!p.CarHash.Equals(want, StringComparison.OrdinalIgnoreCase)) continue;
            }
            _liveries.Items.Add(p);
        }
        if (_liveries.Items.Count > 0)
            _liveries.SelectedIndex = Math.Clamp(keep, 0, _liveries.Items.Count - 1);
        else
            _liveryNote.Text = sameOnly
                ? (_carHash == 0
                    ? "No car in this slot — open a filled slot to see liveries."
                    : $"No liveries for {_carName.Text} yet. Drop a save with this car in DYNO SAVES, or Save livery to library.")
                : "No liveries in the library yet.";
        SyncLiveryDelete();
    }

    private void SelectLivery(LiveryPreset want)
    {
        for (int i = 0; i < _liveries.Items.Count; i++)
        {
            if (_liveries.Items[i] is LiveryPreset p &&
                (p.Id == want.Id ||
                 string.Equals(p.LayersHex, want.LayersHex, StringComparison.OrdinalIgnoreCase)))
            {
                _liveries.SelectedIndex = i;
                return;
            }
        }
    }

    private void SyncLiveryDelete()
    {
        if (_deleteLiveryBtn == null) return;
        _deleteLiveryBtn.Enabled = _liveries.SelectedItem is LiveryPreset p && p.CanDelete;
    }

    private LiveryApplyMode SelectedLiveryMode() => _liveryMode?.SelectedIndex switch
    {
        0 => LiveryApplyMode.Replace,
        2 => LiveryApplyMode.MergeOverlay,
        _ => LiveryApplyMode.MergeEmpty,
    };

    private void ApplySelectedLivery()
    {
        if (_liveries.SelectedItem is not LiveryPreset p)
            throw new InvalidOperationException("Select a livery in the list.");
        string what = LiveryService.Apply(_working, _slot, p, SelectedLiveryMode());
        _onChanged();
        ReloadLiveries();
        MessageBox.Show(this, $"Applied:\n{p.DisplayLabel}\n({what})", "Livery",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void DeleteSelectedLivery()
    {
        if (_liveries.SelectedItem is not LiveryPreset p)
            throw new InvalidOperationException("Select a livery first.");
        if (!p.CanDelete)
            throw new InvalidOperationException("That livery cannot be deleted.");
        var ask = MessageBox.Show(this,
            $"Delete this livery from your library?\n\n{p.DisplayLabel}",
            "Delete livery", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (ask != DialogResult.Yes) return;
        LiveryService.DeleteFromLibrary(p);
        ReloadLiveries();
    }
}
