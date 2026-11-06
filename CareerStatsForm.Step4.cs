namespace NFSU2CustomCarSaves;

/// <summary>Stats wizard UI: presets, undo/redo, Ctrl+K schema search.</summary>
public sealed partial class CareerStatsForm
{
    private TextBox _searchBox = null!;
    private ListBox _searchResults = null!;
    private Panel _searchOverlay = null!;

    private readonly List<(CheckBox On, TextBox Target, StatsWizardAction Action)> _wizardRows = new();
    private ComboBox _wizardPreset = null!;
    private TextBox _wizardPresetName = null!;
    private Label _wizardLog = null!;

    private void CareerStatsForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.Z)
        {
            e.SuppressKeyPress = true;
            DoUndo();
        }
        else if (e.Control && e.KeyCode == Keys.Y)
        {
            e.SuppressKeyPress = true;
            DoRedo();
        }
        else if (e.Control && e.KeyCode == Keys.K)
        {
            e.SuppressKeyPress = true;
            ShowSearchOverlay();
        }
        else if (e.KeyCode == Keys.Escape && _searchOverlay is { Visible: true })
        {
            e.SuppressKeyPress = true;
            HideSearchOverlay();
        }
    }

    private StatsEditResult RecordEdit(string label, Action mutate)
    {
        var result = _history.Apply(_working, label, mutate);
        ShowDiff(result);
        return result;
    }

    private void DoUndo()
    {
        var result = _history.Undo(_working);
        ShowDiff(result);
        LoadFromBuffer();
        Notify();
    }

    private void DoRedo()
    {
        var result = _history.Redo(_working);
        ShowDiff(result);
        LoadFromBuffer();
        Notify();
    }

    // ── SEARCH (Ctrl+K) ─────────────────────────────────────────────────────

    private void EnsureSearchOverlay()
    {
        if (_searchOverlay != null) return;
        _searchOverlay = new Panel
        {
            Visible = false,
            Width = 420,
            Height = 280,
            BackColor = UiTheme.Elevated,
            BorderStyle = BorderStyle.FixedSingle,
        };
        _searchBox = new TextBox
        {
            Dock = DockStyle.Top,
            Height = 28,
            Font = UiTheme.UiFont(10f),
            BackColor = UiTheme.Bg,
            ForeColor = UiTheme.TextStrong,
            BorderStyle = BorderStyle.FixedSingle,
        };
        _searchBox.TextChanged += (_, _) => RefreshSearchResults();
        _searchBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape) { HideSearchOverlay(); e.SuppressKeyPress = true; }
            else if (e.KeyCode is Keys.Enter or Keys.Down)
            {
                if (_searchResults.Items.Count > 0)
                {
                    _searchResults.SelectedIndex = 0;
                    if (e.KeyCode == Keys.Enter) ActivateSearchSelection();
                }
                e.SuppressKeyPress = true;
            }
        };
        _searchResults = new ListBox
        {
            Dock = DockStyle.Fill,
            Font = UiTheme.UiFont(9f),
            BackColor = UiTheme.Bg,
            ForeColor = UiTheme.Text,
            BorderStyle = BorderStyle.None,
        };
        _searchResults.DoubleClick += (_, _) => ActivateSearchSelection();
        _searchResults.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { ActivateSearchSelection(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Escape) { HideSearchOverlay(); e.SuppressKeyPress = true; }
        };
        _searchOverlay.Controls.Add(_searchResults);
        _searchOverlay.Controls.Add(_searchBox);
        Controls.Add(_searchOverlay);
        _searchOverlay.BringToFront();
    }

    private void ShowSearchOverlay()
    {
        EnsureSearchOverlay();
        _searchOverlay.Left = Math.Max(20, (ClientSize.Width - _searchOverlay.Width) / 2);
        _searchOverlay.Top = 80;
        _searchOverlay.Visible = true;
        _searchBox.Text = "";
        RefreshSearchResults();
        _searchBox.Focus();
    }

    private void HideSearchOverlay()
    {
        if (_searchOverlay != null) _searchOverlay.Visible = false;
    }

    private void RefreshSearchResults()
    {
        if (_searchResults == null) return;
        string q = (_searchBox.Text ?? "").Trim();
        _searchResults.Items.Clear();
        foreach (var f in StatsSchemaStore.LoadAll())
        {
            if (q.Length > 0)
            {
                string hay = $"{f.Id} {f.Label} {f.Section} {f.Status} {f.Offset} {f.Reason}";
                if (hay.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0) continue;
            }
            string off = f.ResolveOffset() is int o ? $"0x{o:X}" : "—";
            _searchResults.Items.Add(new SearchHit(f, $"{f.Label}  [{f.Status}]  {off}"));
        }
    }

    private void ActivateSearchSelection()
    {
        if (_searchResults.SelectedItem is not SearchHit hit && _searchResults.Items.Count > 0)
            hit = (SearchHit)_searchResults.Items[0];
        else if (_searchResults.SelectedItem is SearchHit h)
            hit = h;
        else
            return;

        HideSearchOverlay();
        OpenSchemaField(hit.Field);
    }

    private void OpenSchemaField(StatsSchemaField field)
    {
        // Single-wizard UI: jump to matching action row when possible; otherwise show schema info.
        var row = _wizardRows.FirstOrDefault(r =>
            field.Id.Equals(r.Action.Id, StringComparison.OrdinalIgnoreCase)
            || (field.Id.StartsWith("total_reputation_stage_", StringComparison.Ordinal)
                && r.Action.Id == "rep_s" + field.Id[^1])
            || (field.Id.StartsWith("total_spent_slot_", StringComparison.Ordinal) && r.Action.Id == "spent")
            || (field.Id == "races_entered" && r.Action.Id == "entered")
            || (field.Id == "bank" && r.Action.Id == "bank")
            || (field.Id == "game_completion" && r.Action.Id == "complete")
            || (field.Id == "win_percent" && r.Action.Id == "win_record"));

        if (row.On != null)
        {
            row.On.Focus();
            row.On.Checked = true;
            return;
        }

        MessageBox.Show(this,
            $"{field.Label}\nStatus: {field.Status}\n\n{field.Reason ?? "No wizard action — unverified/runtime."}",
            "Schema field",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private sealed class SearchHit
    {
        public StatsSchemaField Field { get; }
        public string Display { get; }
        public SearchHit(StatsSchemaField field, string display) { Field = field; Display = display; }
        public override string ToString() => Display;
    }

    // ── WIZARD ──────────────────────────────────────────────────────────────

    private void BuildWizardInto(Panel page)
    {
        var root = MakePageRoot(page);

        var presetRow = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            BackColor = UiTheme.Bg,
            Margin = new Padding(0, 0, 0, 12),
        };
        presetRow.Controls.Add(new Label
        {
            Text = "Preset",
            AutoSize = true,
            ForeColor = UiTheme.TextMuted,
            Font = UiTheme.UiFont(8.5f),
            Margin = new Padding(0, 6, 8, 0),
        });
        _wizardPreset = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170 };
        UiTheme.StyleCombo(_wizardPreset);
        foreach (var n in StatsWizardPresets.ListPresetNames().Distinct(StringComparer.OrdinalIgnoreCase))
            _wizardPreset.Items.Add(n);
        // Default Custom — targets sync from the open/started save (not Max everything).
        int customIdx = _wizardPreset.Items.IndexOf("Custom");
        _wizardPreset.SelectedIndex = customIdx >= 0 ? customIdx : 0;
        _wizardPreset.SelectedIndexChanged += (_, _) =>
            LoadWizardPreset(_wizardPreset.SelectedItem?.ToString() ?? "Custom");
        presetRow.Controls.Add(_wizardPreset);
        _wizardPresetName = new TextBox
        {
            Width = 140,
            Text = "Custom",
            Font = UiTheme.UiFont(9f),
            BackColor = UiTheme.Elevated,
            ForeColor = UiTheme.TextStrong,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(10, 2, 6, 0),
        };
        presetRow.Controls.Add(_wizardPresetName);
        presetRow.Controls.Add(UiTheme.Compact(UiTheme.Btn("Save preset", () =>
        {
            var preset = CaptureWizardPreset(_wizardPresetName.Text.Trim());
            StatsWizardPresets.SavePreset(preset);
            if (!_wizardPreset.Items.Contains(preset.Name))
                _wizardPreset.Items.Add(preset.Name);
            MessageBox.Show(this, $"Saved {preset.Name}.json", "Preset",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        })));
        root.Controls.Add(presetRow);

        AddWizardSection(root, "Money & completion", ["bank", "complete", "entered"]);
        AddWizardSection(root, "Unlocks", ["shops_all", "extreme", "car_lot", "slots"]);
        AddWizardSection(root, "Career progress", ["win_record", "career_jane"]);
        AddWizardSection(root, "Reputation & spent", ["rep_s1", "rep_s2", "rep_s3", "rep_s4", "rep_s5", "spent"]);

        // Ensure every DefaultActions() id has a row (order from sections above).
        foreach (var a in StatsWizardPresets.DefaultActions())
        {
            if (_wizardRows.Any(r => r.Action.Id == a.Id)) continue;
            AddWizardRow(root, a);
        }

        var applyRow = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            BackColor = UiTheme.Bg,
            Margin = new Padding(0, 8, 0, 6),
        };
        var apply = UiTheme.Btn("Apply selected", () =>
        {
            var actions = _wizardRows.Select(r =>
            {
                r.Action.Enabled = r.On.Checked;
                r.Action.Target = r.Target.Text.Trim();
                return r.Action;
            }).ToList();
            string summary = "";
            var result = RecordEdit("wizard apply", () =>
            {
                summary = StatsWizardPresets.Apply(_working, actions);
            });
            LoadFromBuffer();
            Notify();
            _wizardLog.Text = string.IsNullOrWhiteSpace(summary) ? result.Summary : summary;
            MessageBox.Show(this,
                string.IsNullOrWhiteSpace(summary) ? result.Summary : summary,
                "Career Stats Wizard",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }, primary: true);
        apply.Width = 180;
        applyRow.Controls.Add(apply);
        applyRow.Controls.Add(UiTheme.Compact(UiTheme.Btn("Select all", () =>
        {
            foreach (var r in _wizardRows) r.On.Checked = true;
        })));
        applyRow.Controls.Add(UiTheme.Compact(UiTheme.Btn("Select none", () =>
        {
            foreach (var r in _wizardRows) r.On.Checked = false;
        })));
        root.Controls.Add(applyRow);

        _wizardLog = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(700, 0),
            ForeColor = UiTheme.Lilac,
            Font = UiTheme.UiFont(8f),
            Text = "Ctrl+Z / Ctrl+Y undo·redo · Ctrl+K schema search",
            Margin = new Padding(0, 4, 0, 0),
        };
        root.Controls.Add(_wizardLog);

        LoadWizardPreset(_wizardPreset.SelectedItem?.ToString() ?? "Custom");
    }

    private void AddWizardSection(FlowLayoutPanel root, string title, string[] ids)
    {
        root.Controls.Add(UiTheme.SectionHead(title));
        var defaults = StatsWizardPresets.DefaultActions().ToDictionary(a => a.Id, StringComparer.Ordinal);
        foreach (var id in ids)
        {
            if (!defaults.TryGetValue(id, out var a)) continue;
            AddWizardRow(root, a);
        }
    }

    private void AddWizardRow(FlowLayoutPanel root, StatsWizardAction a)
    {
        var row = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 3),
            BackColor = UiTheme.Bg,
        };
        var on = new CheckBox
        {
            Text = a.Label,
            Checked = a.Enabled,
            AutoSize = true,
            MinimumSize = new Size(280, 0),
            ForeColor = UiTheme.Text,
            Font = UiTheme.UiFont(9.5f),
            Margin = new Padding(0, 4, 10, 0),
            Cursor = Cursors.Hand,
        };
        bool hasTarget = !string.IsNullOrEmpty(a.Target)
            || a.Id.StartsWith("rep_", StringComparison.Ordinal)
            || a.Id is "bank" or "spent" or "entered" or "shops_all" or "complete";
        var target = new TextBox
        {
            Width = 120,
            Text = a.Target ?? "",
            Font = UiTheme.Mono(9.5f),
            BackColor = UiTheme.Elevated,
            ForeColor = UiTheme.TextStrong,
            BorderStyle = BorderStyle.FixedSingle,
            Enabled = hasTarget,
            Visible = hasTarget,
        };
        var action = new StatsWizardAction
        {
            Id = a.Id,
            Label = a.Label,
            Enabled = a.Enabled,
            Target = a.Target,
        };
        _wizardRows.Add((on, target, action));
        row.Controls.Add(on);
        if (hasTarget) row.Controls.Add(target);
        root.Controls.Add(row);
    }

    private void LoadWizardPreset(string name)
    {
        bool custom = name.Equals("Custom", StringComparison.OrdinalIgnoreCase);
        var preset = StatsWizardPresets.LoadPreset(name) ?? StatsWizardPresets.BuiltIn(name);
        _wizardPresetName.Text = preset.Name;
        foreach (var row in _wizardRows)
        {
            var match = preset.Actions.FirstOrDefault(a => a.Id == row.Action.Id);
            if (match == null) continue;
            row.On.Checked = match.Enabled;
            row.Action.Enabled = match.Enabled;
            // Named presets (Max / Legit / user) bring their own targets.
            // Custom always mirrors the live save via SyncWizardTargetsFromBuffer.
            if (!custom && match.Target != null)
            {
                row.Target.Text = match.Target;
                row.Action.Target = match.Target;
            }
        }
        if (custom)
            SyncWizardTargetsFromBuffer();
    }

    private StatsWizardPreset CaptureWizardPreset(string name) => new()
    {
        Name = string.IsNullOrWhiteSpace(name) ? "Custom" : name,
        Actions = _wizardRows.Select(r => new StatsWizardAction
        {
            Id = r.Action.Id,
            Label = r.Action.Label,
            Enabled = r.On.Checked,
            Target = r.Target.Text.Trim(),
        }).ToList(),
    };

    /// <summary>
    /// Fill target boxes + status line from the open save.
    /// Used for Custom (default) and after Apply — not while a Max/Legit preset is selected
    /// (those keep their intended target numbers until Apply).
    /// </summary>
    private void SyncWizardTargetsFromBuffer()
    {
        int money = CareerStatsService.ReadMoney(_working);
        uint enteredVal = CareerStatsService.BufferHasRacesEntered(_working)
            ? CareerStatsService.GetRacesEntered(_working) : 0;
        int spent0 = CareerStatsService.BufferHasTotalSpent(_working, 0)
            ? CareerStatsService.GetTotalSpentOnCar(_working, 0) : 0;

        // Only overwrite editable targets when Custom is active (or preset combo not ready yet).
        string preset = _wizardPreset?.SelectedItem?.ToString() ?? "Custom";
        bool syncBoxes = preset.Equals("Custom", StringComparison.OrdinalIgnoreCase)
                         || _wizardPreset == null;
        if (!syncBoxes || _wizardRows.Count == 0) return;

        foreach (var row in _wizardRows)
        {
            if (!row.Target.Visible) continue;
            string? text = row.Action.Id switch
            {
                "bank" => money.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "complete" => CareerStatsService.IsCareerComplete(_working) ? "1" : "0",
                "entered" => enteredVal.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "spent" => spent0.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "shops_all" => CareerStatsService.CountHiddenShopsUnlocked(_working)
                    .ToString(System.Globalization.CultureInfo.InvariantCulture),
                "rep_s1" when CareerStatsService.BufferHasPackedStages(_working)
                    => CareerStatsService.GetStageTotalReputation(_working, 1).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture),
                "rep_s2" when CareerStatsService.BufferHasPackedStages(_working)
                    => CareerStatsService.GetStageTotalReputation(_working, 2).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture),
                "rep_s3" when CareerStatsService.BufferHasPackedStages(_working)
                    => CareerStatsService.GetStageTotalReputation(_working, 3).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture),
                "rep_s4" when CareerStatsService.BufferHasPackedStages(_working)
                    => CareerStatsService.GetStageTotalReputation(_working, 4).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture),
                "rep_s5" when CareerStatsService.BufferHasPackedStages(_working)
                    => CareerStatsService.GetStageTotalReputation(_working, 5).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture),
                _ => null,
            };
            if (text == null) continue;
            row.Target.Text = text;
            row.Action.Target = text;
        }
    }
}
