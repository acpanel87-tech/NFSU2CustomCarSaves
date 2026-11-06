namespace NFSU2CustomCarSaves;

/// <summary>
/// Overall Career Statistics editor — Tune-style tabs (Bank / Career / Unlocks / Quick).
/// </summary>
public sealed partial class CareerStatsForm : Form
{
    private const int MoneyTicks = 1000;

    private readonly byte[] _working;
    private readonly Action _onChanged;
    private readonly ToolTip _tip = new() { AutoPopDelay = 14000, InitialDelay = 400, ReshowDelay = 200 };
    private readonly StatsEditHistory _history = new();

    private readonly NumericUpDown _money = new();
    private readonly TrackBar _moneyBar = new();
    private readonly CheckBox _complete = new();
    private readonly CheckBox _careerStarted = new();
    private readonly ComboBox _txPick = new();
    private readonly Label _shopsLbl = new();
    private readonly TrackBar _shopsBar = new();
    private readonly Label _extremeLbl = new();
    private readonly Label _garageLbl = new();
    private readonly Label _moddedLbl = new();
    private readonly Label _diffLbl = new();
    private readonly Label _status = new();

    private readonly NumericUpDown[] _repTotal = new NumericUpDown[5];
    private readonly NumericUpDown[] _repRaces = new NumericUpDown[5];
    private readonly Label[] _repAvg = new Label[5];
    private readonly NumericUpDown _spent = new();
    private readonly ComboBox _spentSlot = new();
    private readonly Label _spentOffLbl = new();
    private readonly NumericUpDown _entered = new();
    private readonly CheckBox _enteredExtended = new();
    private readonly Label _winPctNote = new();

    private readonly Panel _bankPage = new();
    private readonly Panel _careerPage = new();
    private readonly Panel _unlocksPage = new();
    private readonly Panel _quickPage = new();
    private readonly Panel _rawPage = new();
    private readonly Panel _wizardPage = new();
    private readonly Panel _historyPage = new();
    private readonly GlowButton _tabBank = new();
    private readonly GlowButton _tabCareer = new();
    private readonly GlowButton _tabUnlocks = new();
    private readonly GlowButton _tabQuick = new();
    private readonly GlowButton _tabRaw = new();
    private readonly GlowButton _tabWizard = new();
    private readonly GlowButton _tabHistory = new();

    private bool _silent;
    private byte[] _backup = [];

    public CareerStatsForm(byte[] working, Action onChanged, string? profileName = null)
    {
        _working = working ?? throw new ArgumentNullException(nameof(working));
        _onChanged = onChanged ?? (() => { });

        string titleName = string.IsNullOrWhiteSpace(profileName) ? "save" : profileName.Trim();
        UiTheme.ApplyForm(this);
        Text = $"Career Stats — {titleName}";
        Width = 780;
        Height = 700;
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

        // Header: title + FULLY MODDED
        var head = new TableLayoutPanel
        {
            ColumnCount = 2,
            AutoSize = true,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 6),
            BackColor = Color.Transparent,
        };
        head.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        head.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var title = new Label
        {
            Text = "Overall Career Statistics",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 15f, FontStyle.Bold | FontStyle.Italic),
            ForeColor = UiTheme.TextStrong,
            Margin = new Padding(0, 4, 0, 0),
        };
        head.Controls.Add(title, 0, 0);

        var fullyHead = UiTheme.Compact(UiTheme.Btn("FULLY MODDED", () => ShowTab(5), primary: true));
        fullyHead.Margin = new Padding(8, 0, 0, 0);
        _tip.SetToolTip(fullyHead, "Open Fully Modded wizard tab (proven/probable actions only).");
        head.Controls.Add(fullyHead, 1, 0);
        outer.Controls.Add(head, 0, 0);

        // Tabs + bordered content
        var tabHost = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = new Padding(0),
            BackColor = Color.Transparent,
        };
        tabHost.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
        tabHost.RowStyles.Add(new RowStyle(SizeType.AutoSize));
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
        StyleTabButton(_tabBank, "BANK", true);
        StyleTabButton(_tabCareer, "CAREER", false);
        StyleTabButton(_tabUnlocks, "UNLOCKS", false);
        StyleTabButton(_tabQuick, "QUICK", false);
        StyleTabButton(_tabRaw, "RAW", false);
        StyleTabButton(_tabWizard, "WIZARD", false);
        StyleTabButton(_tabHistory, "HISTORY", false);
        foreach (var b in new[] { _tabBank, _tabCareer, _tabUnlocks, _tabQuick, _tabRaw, _tabWizard, _tabHistory })
            b.Size = new Size(88, 36);
        _tabBank.Click += (_, _) => ShowTab(0);
        _tabCareer.Click += (_, _) => ShowTab(1);
        _tabUnlocks.Click += (_, _) => ShowTab(2);
        _tabQuick.Click += (_, _) => ShowTab(3);
        _tabRaw.Click += (_, _) => ShowTab(4);
        _tabWizard.Click += (_, _) => ShowTab(5);
        _tabHistory.Click += (_, _) => ShowTab(6);
        tabBar.Controls.Add(_tabBank);
        tabBar.Controls.Add(_tabCareer);
        tabBar.Controls.Add(_tabUnlocks);
        tabBar.Controls.Add(_tabQuick);
        tabBar.Controls.Add(_tabRaw);
        tabBar.Controls.Add(_tabWizard);
        tabBar.Controls.Add(_tabHistory);
        tabHost.Controls.Add(tabBar, 0, 0);

        _moddedLbl.AutoSize = true;
        _moddedLbl.Dock = DockStyle.Fill;
        _moddedLbl.ForeColor = UiTheme.Lilac;
        _moddedLbl.Font = UiTheme.UiBold(8.5f);
        _moddedLbl.Margin = new Padding(0, 2, 0, 4);
        _moddedLbl.Padding = new Padding(0, 2, 0, 2);
        tabHost.Controls.Add(_moddedLbl, 0, 1);

        var contentFrame = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(1),
            BackColor = Color.FromArgb(0x4A, 0x44, 0x9A),
            Margin = new Padding(0),
        };
        var contentInner = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Bg };
        foreach (var page in new[] { _bankPage, _careerPage, _unlocksPage, _quickPage, _rawPage, _wizardPage, _historyPage })
        {
            page.Dock = DockStyle.Fill;
            page.BackColor = UiTheme.Bg;
            page.Padding = new Padding(0);
            page.Visible = false;
        }
        _bankPage.Visible = true;
        BuildBankInto(_bankPage);
        BuildCareerInto(_careerPage);
        BuildUnlocksInto(_unlocksPage);
        BuildQuickInto(_quickPage);
        BuildRawInto(_rawPage);
        BuildWizardInto(_wizardPage);
        BuildHistoryInto(_historyPage);
        contentInner.Controls.Add(_historyPage);
        contentInner.Controls.Add(_wizardPage);
        contentInner.Controls.Add(_rawPage);
        contentInner.Controls.Add(_quickPage);
        contentInner.Controls.Add(_unlocksPage);
        contentInner.Controls.Add(_careerPage);
        contentInner.Controls.Add(_bankPage);
        contentFrame.Controls.Add(contentInner);
        tabHost.Controls.Add(contentFrame, 0, 2);
        outer.Controls.Add(tabHost, 0, 1);

        var bottom = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 10, 0, 0),
            BackColor = Color.Transparent,
        };
        var done = UiTheme.Btn("DONE", () =>
        {
            DialogResult = DialogResult.OK;
            Close();
        }, primary: true);
        bottom.Controls.Add(done);
        outer.Controls.Add(bottom, 0, 2);
        AcceptButton = done;

        // Keep Summarize updates for Notify/LoadFromBuffer without cluttering the UI.
        _status.Visible = false;
        Controls.Add(_status);

        _backup = CareerStatsService.Snapshot(_working);
        LoadFromBuffer();
    }

    private static void StyleTabButton(GlowButton b, string text, bool active)
    {
        b.Text = text;
        b.AutoSize = false;
        b.Size = new Size(120, 36);
        b.Margin = new Padding(0, 0, 6, 0);
        ApplyTabVisual(b, active);
    }

    private static void ApplyTabVisual(GlowButton b, bool active)
    {
        b.Kind = active ? GlowKind.Primary : GlowKind.Default;
    }

    private void ShowTab(int index)
    {
        ApplyTabVisual(_tabBank, index == 0);
        ApplyTabVisual(_tabCareer, index == 1);
        ApplyTabVisual(_tabUnlocks, index == 2);
        ApplyTabVisual(_tabQuick, index == 3);
        ApplyTabVisual(_tabRaw, index == 4);
        ApplyTabVisual(_tabWizard, index == 5);
        ApplyTabVisual(_tabHistory, index == 6);
        _bankPage.Visible = index == 0;
        _careerPage.Visible = index == 1;
        _unlocksPage.Visible = index == 2;
        _quickPage.Visible = index == 3;
        _rawPage.Visible = index == 4;
        _wizardPage.Visible = index == 5;
        _historyPage.Visible = index == 6;
        Panel[] pages = [_bankPage, _careerPage, _unlocksPage, _quickPage, _rawPage, _wizardPage, _historyPage];
        if ((uint)index < pages.Length) pages[index].BringToFront();
        if (index == 6) RefreshHistoryList();
    }

    private static FlowLayoutPanel MakePageRoot(Panel page)
    {
        var root = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(12),
            BackColor = UiTheme.Bg,
        };
        page.Controls.Add(root);
        return root;
    }

    // ── BANK ────────────────────────────────────────────────────────────────

    private void BuildBankInto(Panel page)
    {
        var root = MakePageRoot(page);
        root.Controls.Add(UiTheme.SectionHead("Bank"));

        var moneyRow = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 2),
            BackColor = UiTheme.Bg,
        };
        _money.Minimum = 0;
        _money.Maximum = CareerStatsService.UiMoneyMax;
        _money.Increment = 1000;
        _money.ThousandsSeparator = true;
        _money.Width = 160;
        _money.Font = UiTheme.UiFont(10f);
        _money.BackColor = UiTheme.Elevated;
        _money.ForeColor = UiTheme.TextStrong;
        _money.BorderStyle = BorderStyle.FixedSingle;
        _money.ValueChanged += (_, _) =>
        {
            if (_silent) return;
            int amount = (int)_money.Value;
            SyncMoneyBar(amount);
            RecordEdit($"bank → {amount}", () => CareerStatsService.SetMoney(_working, amount));
            Notify();
        };
        _tip.SetToolTip(_money, $"Writes Layout.MoneyOffset (int32). Clamped 0…{CareerStatsService.UiMoneyMax:N0}.");
        moneyRow.Controls.Add(_money);
        root.Controls.Add(moneyRow);

        _moneyBar.Minimum = 0;
        _moneyBar.Maximum = MoneyTicks;
        _moneyBar.TickFrequency = 100;
        _moneyBar.TickStyle = TickStyle.BottomRight;
        _moneyBar.Width = 640;
        _moneyBar.Height = 36;
        _moneyBar.BackColor = UiTheme.Bg;
        _moneyBar.Margin = new Padding(0, 0, 0, 2);
        _moneyBar.ValueChanged += (_, _) =>
        {
            if (_silent) return;
            int amount = TickToMoney(_moneyBar.Value);
            SyncMoneyNud(amount);
            RecordEdit($"bank → {amount}", () => CareerStatsService.SetMoney(_working, amount));
            Notify();
        };
        _tip.SetToolTip(_moneyBar,
            $"0…1000 ticks → 0…{CareerStatsService.UiMoneyMax:N0} linearly (tick × max / 1000).");
        root.Controls.Add(_moneyBar);

        var presets = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true,
            Margin = new Padding(0, 0, 0, 10),
            BackColor = UiTheme.Bg,
        };
        AddMoneyPreset(presets, "0", 0);
        AddMoneyPreset(presets, "1M", 1_000_000);
        AddMoneyPreset(presets, "10M", 10_000_000);
        AddMoneyPreset(presets, "100M", 100_000_000);
        AddMoneyPreset(presets, "Max", CareerStatsService.UiMoneyMax);
        root.Controls.Add(presets);

        _complete.Text = "Game Completion 100%";
        _complete.AutoSize = true;
        _complete.ForeColor = UiTheme.Text;
        _complete.Font = UiTheme.UiFont(9.5f);
        _complete.Margin = new Padding(0, 0, 0, 10);
        _complete.Cursor = Cursors.Hand;
        _complete.CheckedChanged += (_, _) =>
        {
            if (_silent) return;
            bool on = _complete.Checked;
            RecordEdit($"complete → {(on ? 1 : 0)}", () => CareerStatsService.SetCareerComplete(_working, on));
            Notify();
        };
        _tip.SetToolTip(_complete,
            "Writes SaveMaxOut.CareerCompleteFlag (0xA17A) as 0 or 1.");
        root.Controls.Add(_complete);

        var winBtn = UiTheme.Compact(UiTheme.Btn("Force 100% Win%", () =>
        {
            string msg = "";
            RecordEdit("force win%", () => msg = CareerStatsService.ApplyPerfectWinRecord(_working));
            LoadFromBuffer();
            Notify();
            MessageBox.Show(this, msg, "Win %", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }, primary: true));
        _tip.SetToolTip(winBtn,
            "SaveMaxOut.ForcePerfectWinRecord — flips 7148 loss place 5→1 and 7B00 field_5.");
        root.Controls.Add(winBtn);
    }

    // ── CAREER ──────────────────────────────────────────────────────────────

    private void BuildCareerInto(Panel page)
    {
        var root = MakePageRoot(page);

        root.Controls.Add(UiTheme.SectionHead("Total Reputation (proven)"));
        root.Controls.Add(new Label
        {
            Text = "Packed stage table @ 0x9962 · stride 0x101 · Avg = Total / races (read-only).",
            AutoSize = true,
            ForeColor = UiTheme.Lilac,
            Font = UiTheme.UiFont(8.5f),
            Margin = new Padding(0, 0, 0, 6),
        });

        for (int i = 0; i < 5; i++)
        {
            int stage = i + 1;
            var row = new FlowLayoutPanel
            {
                AutoSize = true,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, 4),
                BackColor = UiTheme.Bg,
            };
            row.Controls.Add(new Label
            {
                Text = $"Stage {stage}",
                AutoSize = true,
                Width = 58,
                ForeColor = UiTheme.TextStrong,
                Font = UiTheme.UiBold(9f),
                Margin = new Padding(0, 6, 8, 0),
            });

            var total = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 10_000_000,
                DecimalPlaces = 1,
                Increment = 50,
                Width = 110,
                Font = UiTheme.UiFont(9.5f),
                BackColor = UiTheme.Elevated,
                ForeColor = UiTheme.TextStrong,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 2, 6, 0),
            };
            int stageCapture = stage;
            total.ValueChanged += (_, _) =>
            {
                if (_silent) return;
                float v = (float)total.Value;
                var result = RecordEdit($"stage {stageCapture} rep → {v}", () =>
                    CareerStatsService.SetStageTotalReputation(_working, stageCapture, v));
                ShowDiff(result);
                RefreshAvgLabels();
                Notify();
            };
            _tip.SetToolTip(total,
                $"float32 @ 0x{CareerStatsService.PackedStageRepOffset(stage):X} (stage {stage}).");
            _repTotal[i] = total;
            row.Controls.Add(total);
            row.Controls.Add(new Label
            {
                Text = "races",
                AutoSize = true,
                ForeColor = UiTheme.Text,
                Font = UiTheme.UiFont(8.5f),
                Margin = new Padding(4, 6, 4, 0),
            });

            var races = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 255,
                DecimalPlaces = 0,
                Increment = 1,
                Width = 60,
                Font = UiTheme.UiFont(9.5f),
                BackColor = UiTheme.Elevated,
                ForeColor = UiTheme.TextStrong,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 2, 6, 0),
            };
            races.ValueChanged += (_, _) =>
            {
                if (_silent) return;
                int n = (int)races.Value;
                var result = RecordEdit($"stage {stageCapture} races → {n}", () =>
                    CareerStatsService.SetStageRepRaceCount(_working, stageCapture, n));
                ShowDiff(result);
                RefreshAvgLabels();
                Notify();
            };
            _tip.SetToolTip(races,
                $"uint8 @ 0x{CareerStatsService.PackedStageRepRacesOffset(stage):X} (rep+4).");
            _repRaces[i] = races;
            row.Controls.Add(races);

            var avg = new Label
            {
                Text = "avg —",
                AutoSize = true,
                ForeColor = UiTheme.Lilac,
                Font = UiTheme.UiBold(9f),
                Margin = new Padding(8, 6, 0, 0),
            };
            _repAvg[i] = avg;
            row.Controls.Add(avg);
            root.Controls.Add(row);
        }

        root.Controls.Add(UiTheme.SectionHead("Total Spent (probable, slots 0–3)"));
        var spentSlotRow = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 4),
            BackColor = UiTheme.Bg,
        };
        _spentSlot.DropDownStyle = ComboBoxStyle.DropDownList;
        _spentSlot.Width = 100;
        _spentSlot.Font = UiTheme.UiFont(9.5f);
        _spentSlot.BackColor = UiTheme.Elevated;
        _spentSlot.ForeColor = UiTheme.TextStrong;
        for (int s = 0; s < global::NFSU2CustomCarSaves.Layout.TotalSpentEditableSlotCount; s++)
            _spentSlot.Items.Add($"Slot {s}");
        _spentSlot.SelectedIndex = 0;
        _spentSlot.SelectedIndexChanged += (_, _) =>
        {
            RefreshSpentOffsetLabel();
            if (_silent) return;
            LoadSpentFromBuffer();
        };
        _tip.SetToolTip(_spentSlot,
            "Editable garage slots 0…3. Slot 4 is unverified (constant junk). UI Spent = current car (index unmapped).");
        spentSlotRow.Controls.Add(_spentSlot);
        _spentOffLbl.AutoSize = true;
        _spentOffLbl.ForeColor = UiTheme.Lilac;
        _spentOffLbl.Font = UiTheme.UiFont(8.5f);
        _spentOffLbl.Margin = new Padding(8, 6, 0, 0);
        spentSlotRow.Controls.Add(_spentOffLbl);
        root.Controls.Add(spentSlotRow);
        RefreshSpentOffsetLabel();

        var spentRow = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 4),
            BackColor = UiTheme.Bg,
        };
        _spent.Minimum = 0;
        _spent.Maximum = int.MaxValue;
        _spent.Increment = 1000;
        _spent.ThousandsSeparator = true;
        _spent.Width = 140;
        _spent.Font = UiTheme.UiFont(9.5f);
        _spent.BackColor = UiTheme.Elevated;
        _spent.ForeColor = UiTheme.TextStrong;
        _spent.BorderStyle = BorderStyle.FixedSingle;
        _spent.ValueChanged += (_, _) =>
        {
            if (_silent) return;
            int slot = Math.Clamp(_spentSlot.SelectedIndex, 0, global::NFSU2CustomCarSaves.Layout.TotalSpentEditableSlotCount - 1);
            int amount = (int)_spent.Value;
            var result = RecordEdit($"spent slot{slot} → {amount}", () =>
                CareerStatsService.SetTotalSpentOnCar(_working, amount, slot));
            ShowDiff(result);
            Notify();
        };
        _tip.SetToolTip(_spent,
            "Probable int32 at SAVE_REC+0xEB8 (= 0x6720 + slot×0x7F2), slots 0…3.");
        spentRow.Controls.Add(_spent);
        spentRow.Controls.Add(new Label
        {
            Text = "per-slot · UI = current car",
            AutoSize = true,
            ForeColor = UiTheme.Lilac,
            Font = UiTheme.UiFont(8.5f),
            Margin = new Padding(8, 6, 0, 0),
        });
        root.Controls.Add(spentRow);
        root.Controls.Add(new Label
        {
            Text = "Slot 4 greyed (unverified) · current_car_slot unmapped — no write.",
            AutoSize = true,
            ForeColor = UiTheme.Lilac,
            Font = UiTheme.UiFont(8.5f),
            Margin = new Padding(0, 0, 0, 10),
        });

        root.Controls.Add(UiTheme.SectionHead("Races Entered (proven)"));
        var enteredRow = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 4),
            BackColor = UiTheme.Bg,
        };
        _entered.Minimum = 0;
        _entered.Maximum = global::NFSU2CustomCarSaves.Layout.RacesEnteredSafeMax;
        _entered.Increment = 1;
        _entered.ThousandsSeparator = true;
        _entered.Width = 140;
        _entered.Font = UiTheme.UiFont(9.5f);
        _entered.BackColor = UiTheme.Elevated;
        _entered.ForeColor = UiTheme.TextStrong;
        _entered.BorderStyle = BorderStyle.FixedSingle;
        _entered.ValueChanged += (_, _) =>
        {
            if (_silent) return;
            uint value = (uint)_entered.Value;
            bool ext = _enteredExtended.Checked;
            var result = RecordEdit($"entered → {value}", () =>
                CareerStatsService.SetRacesEntered(_working, value, ext));
            ShowDiff(result);
            Notify();
        };
        _tip.SetToolTip(_entered,
            "Probable int32 @ Layout.RacesEnteredOffset (0xA16F). Jane/Mosta sample = 246. Safe max 999 (PS2 cheat, unverified).");
        enteredRow.Controls.Add(_entered);
        enteredRow.Controls.Add(new Label
        {
            Text = "@ 0xA16F · after Bank — confirm in-game",
            AutoSize = true,
            ForeColor = UiTheme.Lilac,
            Font = UiTheme.UiFont(8.5f),
            Margin = new Padding(8, 6, 0, 0),
        });
        root.Controls.Add(enteredRow);

        _enteredExtended.Text = "Extended (uint32)";
        _enteredExtended.AutoSize = true;
        _enteredExtended.ForeColor = UiTheme.Text;
        _enteredExtended.Font = UiTheme.UiFont(9f);
        _enteredExtended.Margin = new Padding(0, 0, 0, 4);
        _enteredExtended.Cursor = Cursors.Hand;
        _enteredExtended.CheckedChanged += (_, _) =>
        {
            bool ext = _enteredExtended.Checked;
            decimal max = ext ? uint.MaxValue : global::NFSU2CustomCarSaves.Layout.RacesEnteredSafeMax;
            bool wasSilent = _silent;
            _silent = true;
            try
            {
                if (_entered.Value > max) _entered.Value = max;
                _entered.Maximum = max;
            }
            finally { _silent = wasSilent; }
            if (wasSilent) return;
            // Re-clamp stored value when leaving Extended.
            if (!CareerStatsService.BufferHasRacesEntered(_working)) return;
            uint cur = CareerStatsService.GetRacesEntered(_working);
            uint clamped = ext ? cur : Math.Min(cur, (uint)global::NFSU2CustomCarSaves.Layout.RacesEnteredSafeMax);
            if (clamped == cur) return;
            var result = RecordEdit($"entered → {clamped}", () =>
                CareerStatsService.SetRacesEntered(_working, clamped, ext));
            ShowDiff(result);
            Notify();
        };
        _tip.SetToolTip(_enteredExtended,
            "Off = Safe 0…999. On = write full uint32 range at the same offset.");
        root.Controls.Add(_enteredExtended);

        _winPctNote.Text = "Win% = Overall Wins / Races Entered (derived). Wins & type counters remain runtime/unverified.";
        _winPctNote.AutoSize = true;
        _winPctNote.MaximumSize = new Size(640, 0);
        _winPctNote.ForeColor = UiTheme.Lilac;
        _winPctNote.Font = UiTheme.UiFont(8.5f);
        _winPctNote.Margin = new Padding(0, 0, 0, 10);
        root.Controls.Add(_winPctNote);

        var backupRow = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true,
            Margin = new Padding(0, 0, 0, 10),
            BackColor = UiTheme.Bg,
        };
        var undoBtn = UiTheme.Compact(UiTheme.Btn("Undo stats backup", () =>
        {
            if (_backup.Length != _working.Length)
            {
                MessageBox.Show(this, "No backup snapshot available.", Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show(this,
                    "Restore the Stats buffer to the snapshot taken when this window opened?",
                    "Undo stats backup",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            var result = CareerStatsService.RestoreSnapshot(_working, _backup);
            ShowDiff(result);
            LoadFromBuffer();
            Notify();
        }));
        _tip.SetToolTip(undoBtn, "Restores full working-buffer snapshot from dialog open.");
        backupRow.Controls.Add(undoBtn);
        var refreshBackup = UiTheme.Compact(UiTheme.Btn("Refresh backup", () =>
        {
            _backup = CareerStatsService.Snapshot(_working);
            ShowDiff(new StatsEditResult("backup refreshed", 0, "(snapshot taken)", CareerStatsService.CareerChecksumsOk(_working)));
        }));
        _tip.SetToolTip(refreshBackup, "Replace undo snapshot with the current buffer.");
        backupRow.Controls.Add(refreshBackup);
        root.Controls.Add(backupRow);

        _diffLbl.AutoSize = true;
        _diffLbl.MaximumSize = new Size(640, 0);
        _diffLbl.ForeColor = UiTheme.Lilac;
        _diffLbl.Font = UiTheme.UiFont(8f);
        _diffLbl.Margin = new Padding(0, 0, 0, 12);
        _diffLbl.Text = "Diff: (no edits yet)";
        root.Controls.Add(_diffLbl);

        root.Controls.Add(UiTheme.SectionHead("Races Won"));
        var racesBtn = UiTheme.Compact(UiTheme.Btn("Apply Jane race/career progress", () =>
        {
            if (MessageBox.Show(this,
                    "Apply Jane race/career progress?\n\n" +
                    "Copies Jane 7148 + stages + 7B00 + markers + 887C, sets complete=1,\n" +
                    "then ForcePerfectWinRecord. Money is preserved.",
                    "Races Won",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            string report = CareerStatsService.ApplyJaneRaceAndCareerProgress(_working, preserveMoney: true);
            LoadFromBuffer();
            Notify();
            MessageBox.Show(this, report, "Races Won", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }, info: true));
        racesBtn.Margin = new Padding(0, 0, 0, 10);
        _tip.SetToolTip(racesBtn, "ApplyCareerProgress + ForcePerfectWinRecord (money preserved).");
        root.Controls.Add(racesBtn);

        root.Controls.Add(UiTheme.SectionHead("Rewards"));
        var rewardsBtn = UiTheme.Compact(UiTheme.Btn("Apply Jane rewards & sponsors", () =>
        {
            if (MessageBox.Show(this,
                    "Apply Jane rewards & sponsors?\n\nCopies Career7148 + Career887C only (magazines / DVD / sponsors / visual path).",
                    "Rewards",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            string report = CareerStatsService.ApplyRewardsAndSponsors(_working);
            LoadFromBuffer();
            Notify();
            MessageBox.Show(this, report, "Rewards", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }, info: true));
        rewardsBtn.Margin = new Padding(0, 0, 0, 10);
        _tip.SetToolTip(rewardsBtn, "SaveMaxOut.ApplyRewardsAndSponsors — 7148 + 887C from Jane.");
        root.Controls.Add(rewardsBtn);

        root.Controls.Add(UiTheme.SectionHead("Stages"));
        var stagesBtn = UiTheme.Compact(UiTheme.Btn("Apply Jane stages & events", () =>
        {
            if (MessageBox.Show(this,
                    "Apply Jane stages & events?\n\n" +
                    "Full career progress block (7148 + stages + 7B00 + markers + 887C, complete=1).\n" +
                    "Money is preserved.",
                    "Stage Status",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            string report = CareerStatsService.ApplyJaneStagesAndEvents(_working, preserveMoney: true);
            LoadFromBuffer();
            Notify();
            MessageBox.Show(this, report, "Stage Status", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }, info: true));
        stagesBtn.Margin = new Padding(0, 0, 0, 10);
        _tip.SetToolTip(stagesBtn, "SaveMaxOut.ApplyCareerProgress (money preserved).");
        root.Controls.Add(stagesBtn);

        root.Controls.Add(UiTheme.SectionHead("Hidden Shops"));
        _shopsLbl.AutoSize = true;
        _shopsLbl.ForeColor = UiTheme.Lilac;
        _shopsLbl.Font = UiTheme.UiBold(9f);
        _shopsLbl.Margin = new Padding(0, 0, 0, 4);
        _tip.SetToolTip(_shopsLbl,
            $"SaveMaxOut.HiddenShopsOffset 0xAD9A · {SaveMaxOut.HiddenShopsLen} bytes (0/1).");
        root.Controls.Add(_shopsLbl);

        _shopsBar.Minimum = 0;
        _shopsBar.Maximum = SaveMaxOut.HiddenShopsLen;
        _shopsBar.TickFrequency = 1;
        _shopsBar.TickStyle = TickStyle.BottomRight;
        _shopsBar.Width = 640;
        _shopsBar.Height = 36;
        _shopsBar.BackColor = UiTheme.Bg;
        _shopsBar.Margin = new Padding(0, 0, 0, 6);
        _shopsBar.ValueChanged += (_, _) =>
        {
            if (_silent) return;
            int n = _shopsBar.Value;
            RecordEdit($"shops → {n}", () => CareerStatsService.SetHiddenShopsUnlockedCount(_working, n));
            Notify();
        };
        _tip.SetToolTip(_shopsBar,
            $"0…{SaveMaxOut.HiddenShopsLen}: first N discovery bytes → 1, rest → 0.");
        root.Controls.Add(_shopsBar);

        var shopsBtn = UiTheme.Compact(UiTheme.Btn("Unlock all + apply Jane map", () =>
        {
            if (MessageBox.Show(this,
                    "Unlock all hidden shops and apply Jane world/map state?\n\n" +
                    "Copies Jane world state, then sets all 58 discovery bytes to 1. Money preserved.",
                    "Hidden Shops",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            string report = CareerStatsService.UnlockHiddenShopsAndWorld(_working, preserveMoney: true);
            LoadFromBuffer();
            Notify();
            MessageBox.Show(this, report, "Hidden Shops", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }, primary: true));
        _tip.SetToolTip(shopsBtn, "ApplyWorldState + UnlockAllHiddenShops (post-7B00 order).");
        root.Controls.Add(shopsBtn);
    }

    // ── UNLOCKS ─────────────────────────────────────────────────────────────

    private void BuildUnlocksInto(Panel page)
    {
        var root = MakePageRoot(page);

        _extremeLbl.AutoSize = true;
        _extremeLbl.ForeColor = UiTheme.Lilac;
        _extremeLbl.Font = UiTheme.UiBold(9f);
        _extremeLbl.Margin = new Padding(0, 0, 0, 8);
        _tip.SetToolTip(_extremeLbl, "SaveMaxOut.AreShopCatalogsExtreme — global shop-tier bytes ≥ 3.");
        root.Controls.Add(_extremeLbl);

        var row = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true,
            Margin = new Padding(0, 0, 0, 10),
            BackColor = UiTheme.Bg,
        };

        var extremeBtn = UiTheme.Compact(UiTheme.Btn("Parts catalogs → Extreme", () =>
        {
            string report = CareerStatsService.SetShopCatalogsExtreme(_working);
            LoadFromBuffer();
            Notify();
            MessageBox.Show(this, report, "Shop catalogs", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }, info: true));
        _tip.SetToolTip(extremeBtn, "SaveMaxOut.SetShopCatalogsExtreme — global tier bytes → 3.");
        row.Controls.Add(extremeBtn);

        var lotBtn = UiTheme.Compact(UiTheme.Btn("Car lot stock unlock", () =>
        {
            string report = CareerStatsService.ApplyCarLot(_working);
            LoadFromBuffer();
            Notify();
            MessageBox.Show(this, report, "Car lot", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }, info: true));
        _tip.SetToolTip(lotBtn, "SaveMaxOut.ApplyCarLot — Jane floats @ 0xBC.");
        row.Controls.Add(lotBtn);
        root.Controls.Add(row);

        _careerStarted.Text = "Career started";
        _careerStarted.AutoSize = true;
        _careerStarted.ForeColor = UiTheme.Text;
        _careerStarted.Font = UiTheme.UiFont(9.5f);
        _careerStarted.Margin = new Padding(0, 2, 0, 10);
        _careerStarted.Cursor = Cursors.Hand;
        _careerStarted.CheckedChanged += (_, _) =>
        {
            if (_silent) return;
            CareerStatsService.SetCareerStarted(_working, _careerStarted.Checked);
            Notify();
        };
        _tip.SetToolTip(_careerStarted,
            "Layout.CareerSaveBase + CareerIsStartedRel (0/1).");
        root.Controls.Add(_careerStarted);

        root.Controls.Add(new Label
        {
            Text = "Transmission",
            AutoSize = true,
            ForeColor = UiTheme.Text,
            Font = UiTheme.UiFont(9.5f),
            Margin = new Padding(0, 0, 0, 2),
        });
        _txPick.DropDownStyle = ComboBoxStyle.DropDownList;
        _txPick.Width = 160;
        UiTheme.StyleCombo(_txPick);
        _txPick.Items.Add("Automatic");
        _txPick.Items.Add("Manual");
        _txPick.Margin = new Padding(0, 0, 0, 10);
        _txPick.SelectedIndexChanged += (_, _) =>
        {
            if (_silent) return;
            var mode = _txPick.SelectedIndex == 1
                ? PlayerOptionsService.TransmissionMode.Manual
                : PlayerOptionsService.TransmissionMode.Automatic;
            CareerStatsService.SetTransmission(_working, mode);
            Notify();
        };
        _tip.SetToolTip(_txPick, "PlayerOptionsService — Manual + Auto companion flags.");
        root.Controls.Add(_txPick);

        _garageLbl.AutoSize = true;
        _garageLbl.ForeColor = UiTheme.Lilac;
        _garageLbl.Font = UiTheme.UiBold(9f);
        _garageLbl.Margin = new Padding(0, 0, 0, 6);
        _tip.SetToolTip(_garageLbl,
            "Read-only Layout.GarageCarCountOffset. Sync recomputes from occupied slots.");
        root.Controls.Add(_garageLbl);

        var garageRow = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true,
            Margin = new Padding(0, 0, 0, 2),
            BackColor = UiTheme.Bg,
        };

        var syncGarage = UiTheme.Compact(UiTheme.Btn("Sync garage counter", () =>
        {
            string msg = CareerStatsService.SyncGarageCarCount(_working);
            LoadFromBuffer();
            Notify();
            MessageBox.Show(this, msg, "Garage counter", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }));
        _tip.SetToolTip(syncGarage,
            "GarageDatService.UpdateGarageCarCount (minUnlocked 5) — same floor as 100% Save.");
        garageRow.Controls.Add(syncGarage);

        var unlockSlots = UiTheme.Compact(UiTheme.Btn("Unlock all garage slots", () =>
        {
            string msg = CareerStatsService.UnlockAllGarageSlots(_working);
            LoadFromBuffer();
            Notify();
            MessageBox.Show(this, msg, "Garage slots", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }));
        _tip.SetToolTip(unlockSlots,
            "UnlockSlot on CareerSlotCount / MaxSlotCount + UpdateGarageCarCount. Cars unchanged.");
        garageRow.Controls.Add(unlockSlots);
        root.Controls.Add(garageRow);
    }

    // ── QUICK ───────────────────────────────────────────────────────────────

    private void BuildQuickInto(Panel page)
    {
        var root = MakePageRoot(page);
        root.Controls.Add(UiTheme.SectionHead("Quick"));

        var quick = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true,
            Margin = new Padding(0, 4, 0, 4),
            BackColor = UiTheme.Bg,
        };

        var janeBtn = UiTheme.Compact(UiTheme.Btn("Apply Jane free-play unlocks", () =>
        {
            if (MessageBox.Show(this,
                    "Apply Jane free-play unlocks?\n\n" +
                    "Car lot, shops, career stages, complete flag, win record, slot unlocks.\n" +
                    "Money is preserved at the Bank value.",
                    "Jane unlocks",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            string report = CareerStatsService.ApplyJaneProgress(_working, preserveMoney: true);
            LoadFromBuffer();
            Notify();
            MessageBox.Show(this, report, "Jane unlocks", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }, info: true));
        _tip.SetToolTip(janeBtn, "SaveMaxOut.Apply with money restored afterward.");
        quick.Controls.Add(janeBtn);

        var fullBtn = UiTheme.Compact(UiTheme.Btn("Sync from 100% Save defaults", () =>
        {
            if (MessageBox.Show(this,
                    $"Sync full 100% Save defaults?\n\nSame as main-window 100% Save, including money → {CareerStatsService.UiMoneyMax:N0}.",
                    "100% defaults",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
                return;
            string report = CareerStatsService.ApplyFullMaxOut(_working);
            LoadFromBuffer();
            Notify();
            MessageBox.Show(this, report, "100% defaults", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }));
        _tip.SetToolTip(fullBtn, "Full SaveMaxOut.Apply — overwrites money.");
        quick.Controls.Add(fullBtn);

        root.Controls.Add(quick);
    }

    private void ApplyFullyModded()
    {
        if (MessageBox.Show(this,
                "Apply fully modded save?\n\n" +
                "• Full Jane free-play unlocks + win%\n" +
                $"• Money → {CareerStatsService.UiMoneyMax:N0}\n" +
                "• All hidden shops + Extreme catalogs\n" +
                "• All garage slots unlocked (cars unchanged)\n" +
                "• Transmission is preserved\n\n" +
                "This overwrites career progress BLOBs like 100% Save.",
                "Fully modded save",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
            return;
        string report = CareerStatsService.ApplyFullyModded(
            _working, preserveMoney: false, preserveTransmission: true);
        LoadFromBuffer();
        Notify();
        MessageBox.Show(this, report, "Fully modded save", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private void AddMoneyPreset(FlowLayoutPanel host, string label, int amount)
    {
        var btn = UiTheme.Compact(UiTheme.Btn(label, () => ApplyMoneyUi(amount)));
        btn.Margin = new Padding(0, 0, 6, 2);
        _tip.SetToolTip(btn, $"Set money → {amount:N0}");
        host.Controls.Add(btn);
    }

    private void ApplyMoneyUi(int amount)
    {
        amount = Math.Clamp(amount, 0, CareerStatsService.UiMoneyMax);
        _silent = true;
        try
        {
            SyncMoneyNud(amount);
            SyncMoneyBar(amount);
        }
        finally { _silent = false; }
        RecordEdit($"bank → {amount}", () => CareerStatsService.SetMoney(_working, amount));
        Notify();
    }

    private static int TickToMoney(int tick) =>
        (int)((long)Math.Clamp(tick, 0, MoneyTicks) * CareerStatsService.UiMoneyMax / MoneyTicks);

    private static int MoneyToTick(int amount)
    {
        amount = Math.Clamp(amount, 0, CareerStatsService.UiMoneyMax);
        return (int)((long)amount * MoneyTicks / CareerStatsService.UiMoneyMax);
    }

    private void SyncMoneyNud(int amount)
    {
        amount = Math.Clamp(amount, 0, CareerStatsService.UiMoneyMax);
        bool was = _silent;
        _silent = true;
        try { _money.Value = amount; }
        finally { _silent = was; }
    }

    private void SyncMoneyBar(int amount)
    {
        int tick = MoneyToTick(amount);
        bool was = _silent;
        _silent = true;
        try { _moneyBar.Value = tick; }
        finally { _silent = was; }
    }

    private void RefreshShopUi()
    {
        int n = Math.Clamp(
            CareerStatsService.CountHiddenShopsUnlocked(_working),
            0,
            SaveMaxOut.HiddenShopsLen);
        _shopsLbl.Text = $"Unlocked {n}/{SaveMaxOut.HiddenShopsLen}";
        bool was = _silent;
        _silent = true;
        try { _shopsBar.Value = n; }
        finally { _silent = was; }
    }

    private void RefreshExtremeLabel()
    {
        bool extreme = CareerStatsService.AreShopCatalogsExtreme(_working);
        _extremeLbl.Text = $"Shop catalogs Extreme: {(extreme ? "yes" : "no")}";
    }

    private void RefreshGarageLabel()
    {
        int n = CareerStatsService.GetGarageCarCount(_working);
        _garageLbl.Text = $"Garage car count: {n}  (read-only · sync below)";
    }

    private void LoadFromBuffer()
    {
        _silent = true;
        try
        {
            int money = Math.Clamp(CareerStatsService.ReadMoney(_working), 0, CareerStatsService.UiMoneyMax);
            _money.Value = money;
            _moneyBar.Value = MoneyToTick(money);
            _complete.Checked = CareerStatsService.IsCareerComplete(_working);
            _careerStarted.Checked = CareerStatsService.GetCareerStarted(_working);
            _txPick.SelectedIndex =
                CareerStatsService.GetTransmission(_working) == PlayerOptionsService.TransmissionMode.Manual
                    ? 1
                    : 0;
            if (CareerStatsService.BufferHasPackedStages(_working))
            {
                for (int i = 0; i < 5; i++)
                {
                    int stage = i + 1;
                    float rep = CareerStatsService.GetStageTotalReputation(_working, stage);
                    byte races = CareerStatsService.GetStageRepRaceCount(_working, stage);
                    _repTotal[i].Value = ClampDecimal(rep, _repTotal[i].Minimum, _repTotal[i].Maximum);
                    _repRaces[i].Value = races;
                }
            }
            LoadSpentFromBuffer();
            if (CareerStatsService.BufferHasRacesEntered(_working))
            {
                uint entered = CareerStatsService.GetRacesEntered(_working);
                bool needExt = entered > global::NFSU2CustomCarSaves.Layout.RacesEnteredSafeMax;
                if (needExt && !_enteredExtended.Checked)
                    _enteredExtended.Checked = true;
                decimal max = _enteredExtended.Checked ? uint.MaxValue : global::NFSU2CustomCarSaves.Layout.RacesEnteredSafeMax;
                _entered.Maximum = max;
                _entered.Value = ClampDecimal(entered, _entered.Minimum, _entered.Maximum);
            }
            RefreshAvgLabels();
            RefreshShopUi();
            RefreshExtremeLabel();
            RefreshGarageLabel();
            RefreshModdedLabel();
            _status.Text = CareerStatsService.Summarize(_working);
        }
        finally { _silent = false; }
    }

    private int SelectedSpentSlot() =>
        Math.Clamp(_spentSlot.SelectedIndex, 0, global::NFSU2CustomCarSaves.Layout.TotalSpentEditableSlotCount - 1);

    private void RefreshSpentOffsetLabel()
    {
        int slot = SelectedSpentSlot();
        int off = global::NFSU2CustomCarSaves.Layout.TotalSpentOnCarOffsetForSlot(slot);
        _spentOffLbl.Text = $"@ 0x{off:X} · SAVE_REC+0xEB8";
    }

    private void LoadSpentFromBuffer()
    {
        int slot = SelectedSpentSlot();
        if (!CareerStatsService.BufferHasTotalSpent(_working, slot)) return;
        bool was = _silent;
        _silent = true;
        try
        {
            int spent = CareerStatsService.GetTotalSpentOnCar(_working, slot);
            _spent.Value = ClampDecimal(spent, _spent.Minimum, _spent.Maximum);
            RefreshSpentOffsetLabel();
        }
        finally { _silent = was; }
    }

    private void RefreshAvgLabels()
    {
        if (!CareerStatsService.BufferHasPackedStages(_working)) return;
        for (int i = 0; i < 5; i++)
        {
            int stage = i + 1;
            float avg = CareerStatsService.GetStageAverageReputation(_working, stage);
            int races = CareerStatsService.GetStageRepRaceCount(_working, stage);
            _repAvg[i].Text = races <= 0 ? "avg —" : $"avg {avg:0.#}";
        }
    }

    private void ShowDiff(StatsEditResult result)
    {
        _diffLbl.Text = "Diff: " + result.Summary;
        _diffLbl.ForeColor = result.ChecksumsOk ? UiTheme.Lilac : Color.OrangeRed;
    }

    private static decimal ClampDecimal(double value, decimal min, decimal max)
    {
        decimal d = (decimal)value;
        if (d < min) return min;
        if (d > max) return max;
        return d;
    }

    private void RefreshModdedLabel()
    {
        _moddedLbl.Text = CareerStatsService.DescribeModdedStatus(_working);
    }

    private void Notify()
    {
        CareerStatsService.EnsureCareerChecksums(_working);
        RefreshShopUi();
        RefreshExtremeLabel();
        RefreshGarageLabel();
        RefreshModdedLabel();
        _status.Text = CareerStatsService.Summarize(_working);
        _onChanged();
    }
}
