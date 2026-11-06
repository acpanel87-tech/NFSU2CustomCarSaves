namespace NFSU2CustomCarSaves;

/// <summary>Step 4: undo/redo, search, raw hex, Fully Modded wizard (partial).</summary>
public sealed partial class CareerStatsForm
{
    // Avoid clash with Control.Layout event inside Form methods.
    private static int CareerProfileSize => global::NFSU2CustomCarSaves.Layout.ProfileSize;

    private ListBox _historyList = null!;
    private Label _historyDetail = null!;
    private TextBox _searchBox = null!;
    private ListBox _searchResults = null!;
    private Panel _searchOverlay = null!;

    private NumericUpDown _rawOffset = null!;
    private ComboBox _rawKind = null!;
    private TextBox _rawValue = null!;
    private Label _rawReadout = null!;
    private TextBox _rawLabel = null!;
    private TextBox _rawId = null!;
    private ComboBox _rawType = null!;

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

    /// <summary>Snapshot → mutate → CRC → history list → UI refresh.</summary>
    private StatsEditResult RecordEdit(string label, Action mutate)
    {
        var result = _history.Apply(_working, label, mutate);
        ShowDiff(result);
        RefreshHistoryList();
        return result;
    }

    private void DoUndo()
    {
        var result = _history.Undo(_working);
        ShowDiff(result);
        LoadFromBuffer();
        RefreshHistoryList();
        Notify();
    }

    private void DoRedo()
    {
        var result = _history.Redo(_working);
        ShowDiff(result);
        LoadFromBuffer();
        RefreshHistoryList();
        Notify();
    }

    // ── HISTORY ─────────────────────────────────────────────────────────────

    private void BuildHistoryInto(Panel page)
    {
        var root = MakePageRoot(page);
        root.Controls.Add(UiTheme.SectionHead("Undo / Redo"));
        root.Controls.Add(new Label
        {
            Text = "Ctrl+Z undo · Ctrl+Y redo · list shows field, old → new, offset.",
            AutoSize = true,
            ForeColor = UiTheme.Lilac,
            Font = UiTheme.UiFont(8.5f),
            Margin = new Padding(0, 0, 0, 6),
        });

        var row = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true,
            Margin = new Padding(0, 0, 0, 8),
            BackColor = UiTheme.Bg,
        };
        row.Controls.Add(UiTheme.Compact(UiTheme.Btn("Undo", DoUndo)));
        row.Controls.Add(UiTheme.Compact(UiTheme.Btn("Redo", DoRedo)));
        root.Controls.Add(row);

        _historyList = new ListBox
        {
            Width = 700,
            Height = 220,
            Font = UiTheme.Mono(8.5f),
            BackColor = UiTheme.Elevated,
            ForeColor = UiTheme.TextStrong,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 0, 0, 6),
        };
        root.Controls.Add(_historyList);

        _historyDetail = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(700, 0),
            ForeColor = UiTheme.Lilac,
            Font = UiTheme.UiFont(8.5f),
            Text = "No changes yet.",
        };
        root.Controls.Add(_historyDetail);
    }

    private void RefreshHistoryList()
    {
        if (_historyList == null) return;
        _historyList.BeginUpdate();
        _historyList.Items.Clear();
        foreach (var line in _history.FormatChangeLines())
            _historyList.Items.Add(line);
        _historyList.EndUpdate();
        _historyDetail.Text = string.IsNullOrEmpty(_history.LastLabel)
            ? "No changes yet."
            : $"{_history.LastLabel} · undo={_history.CanUndo} redo={_history.CanRedo}";
    }

    // ── SEARCH (Ctrl+K) ─────────────────────────────────────────────────────

    private void EnsureSearchOverlay()
    {
        if (_searchOverlay != null) return;
        _searchOverlay = new Panel
        {
            Visible = false,
            Width = 520,
            Height = 320,
            BackColor = UiTheme.Surface,
            BorderStyle = BorderStyle.FixedSingle,
        };
        _searchBox = new TextBox
        {
            Width = 490,
            Font = UiTheme.UiFont(11f),
            BackColor = UiTheme.Elevated,
            ForeColor = UiTheme.TextStrong,
            BorderStyle = BorderStyle.FixedSingle,
            Location = new Point(12, 12),
        };
        _searchBox.TextChanged += (_, _) => FillSearchResults(_searchBox.Text);
        _searchBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; ActivateSearchSelection(); }
            if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; HideSearchOverlay(); }
            if (e.KeyCode == Keys.Down && _searchResults.Items.Count > 0)
            {
                e.SuppressKeyPress = true;
                _searchResults.Focus();
                _searchResults.SelectedIndex = 0;
            }
        };
        _searchResults = new ListBox
        {
            Width = 490,
            Height = 250,
            Location = new Point(12, 48),
            Font = UiTheme.UiFont(9f),
            BackColor = UiTheme.Elevated,
            ForeColor = UiTheme.TextStrong,
            BorderStyle = BorderStyle.FixedSingle,
        };
        _searchResults.DoubleClick += (_, _) => ActivateSearchSelection();
        _searchResults.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; ActivateSearchSelection(); }
        };
        _searchOverlay.Controls.Add(_searchBox);
        _searchOverlay.Controls.Add(_searchResults);
        Controls.Add(_searchOverlay);
        _searchOverlay.BringToFront();
    }

    private void ShowSearchOverlay()
    {
        EnsureSearchOverlay();
        _searchOverlay.Left = Math.Max(20, (ClientSize.Width - _searchOverlay.Width) / 2);
        _searchOverlay.Top = 72;
        _searchOverlay.Visible = true;
        _searchOverlay.BringToFront();
        _searchBox.Text = "";
        FillSearchResults("");
        _searchBox.Focus();
    }

    private void HideSearchOverlay()
    {
        if (_searchOverlay != null) _searchOverlay.Visible = false;
    }

    private void FillSearchResults(string query)
    {
        EnsureSearchOverlay();
        _searchResults.BeginUpdate();
        _searchResults.Items.Clear();
        string q = (query ?? "").Trim();
        foreach (var f in StatsSchemaStore.LoadAll())
        {
            string hay = $"{f.Id} {f.Label} {f.Section} {f.Offset} {f.Status} {f.Reason}";
            if (q.Length > 0 && hay.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            string off = f.ResolveOffset() is int o ? $"0x{o:X}" : "—";
            string mark = f.Status is "unverified" or "runtime" ? " [grey]" : "";
            _searchResults.Items.Add(new SearchHit(f, $"{f.Label} · {f.Status} · {off}{mark}"));
        }
        _searchResults.EndUpdate();
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
        string section = field.Section ?? "";
        if (field.Id.StartsWith("total_reputation", StringComparison.Ordinal)
            || field.Id.StartsWith("rep_races", StringComparison.Ordinal)
            || field.Id.StartsWith("avg_reputation", StringComparison.Ordinal)
            || field.Id.StartsWith("total_spent_slot_", StringComparison.Ordinal)
            || field.Id == "total_spent_on_car"
            || field.Id == "races_entered"
            || section.Contains("Reputation", StringComparison.OrdinalIgnoreCase)
            || section.Contains("Hidden Shops", StringComparison.OrdinalIgnoreCase)
            || section.Contains("Races", StringComparison.OrdinalIgnoreCase)
            || section.Contains("Rewards", StringComparison.OrdinalIgnoreCase))
        {
            ShowTab(1);
            if (field.Id.StartsWith("total_spent_slot_", StringComparison.Ordinal)
                && int.TryParse(field.Id.AsSpan("total_spent_slot_".Length), out int spentSlot)
                && spentSlot >= 0 && spentSlot < _spentSlot.Items.Count)
            {
                _silent = true;
                try { _spentSlot.SelectedIndex = spentSlot; }
                finally { _silent = false; }
                LoadSpentFromBuffer();
            }
        }
        else if (field.Id is "bank" or "game_completion" or "win_percent")
            ShowTab(0);
        else if (section.Contains("Unlock", StringComparison.OrdinalIgnoreCase)
                 || field.Id is "transmission" or "garage_car_count" or "slot_locks" or "career_started"
                 || field.Id is "shop_catalog_tiers" or "car_lot")
            ShowTab(2);
        else if (field.Status is "unverified" or "runtime" || field.ResolveOffset() == null)
        {
            ShowTab(4);
            if (field.ResolveOffset() is int off)
            {
                _silent = true;
                try { _rawOffset.Value = off; }
                finally { _silent = false; }
                RefreshRawReadout();
            }
            MessageBox.Show(this,
                $"{field.Label}\nStatus: {field.Status}\n\n{field.Reason ?? "No disk editor."}",
                "Schema field",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }
        else
            ShowTab(4);

        if (field.ResolveOffset() is int o2 && _rawOffset != null)
        {
            _silent = true;
            try { _rawOffset.Value = o2; }
            finally { _silent = false; }
            RefreshRawReadout();
        }
    }

    private sealed class SearchHit
    {
        public StatsSchemaField Field { get; }
        private readonly string _text;
        public SearchHit(StatsSchemaField field, string text) { Field = field; _text = text; }
        public override string ToString() => _text;
    }

    // ── RAW ─────────────────────────────────────────────────────────────────

    private void BuildRawInto(Panel page)
    {
        var root = MakePageRoot(page);
        root.Controls.Add(UiTheme.SectionHead("Raw / Hex"));
        root.Controls.Add(new Label
        {
            Text = "Read/write any career offset. Always backups via Ctrl+Z history + CRC @ 0x10/14/18.",
            AutoSize = true,
            ForeColor = UiTheme.Lilac,
            Font = UiTheme.UiFont(8.5f),
            Margin = new Padding(0, 0, 0, 8),
        });

        var row1 = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = UiTheme.Bg, Margin = new Padding(0, 0, 0, 6) };
        row1.Controls.Add(new Label { Text = "Offset", AutoSize = true, ForeColor = UiTheme.Text, Margin = new Padding(0, 6, 6, 0) });
        _rawOffset = new NumericUpDown
        {
            Hexadecimal = true,
            Minimum = 0,
            Maximum = CareerProfileSize - 1,
            Width = 100,
            Font = UiTheme.Mono(10f),
            BackColor = UiTheme.Elevated,
            ForeColor = UiTheme.TextStrong,
            BorderStyle = BorderStyle.FixedSingle,
        };
        _rawOffset.ValueChanged += (_, _) => { if (!_silent) RefreshRawReadout(); };
        row1.Controls.Add(_rawOffset);
        row1.Controls.Add(new Label { Text = "Type", AutoSize = true, ForeColor = UiTheme.Text, Margin = new Padding(12, 6, 6, 0) });
        _rawKind = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
        UiTheme.StyleCombo(_rawKind);
        _rawKind.Items.AddRange(["int8", "uint8", "int16", "uint16", "int32", "uint32", "float32", "hex"]);
        _rawKind.SelectedIndex = 4;
        _rawKind.SelectedIndexChanged += (_, _) => RefreshRawReadout();
        row1.Controls.Add(_rawKind);
        root.Controls.Add(row1);

        _rawReadout = new Label
        {
            AutoSize = true,
            ForeColor = UiTheme.TextStrong,
            Font = UiTheme.Mono(10f),
            Margin = new Padding(0, 0, 0, 6),
            Text = "value: —",
        };
        root.Controls.Add(_rawReadout);

        var row2 = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = UiTheme.Bg, Margin = new Padding(0, 0, 0, 8) };
        row2.Controls.Add(new Label { Text = "Write", AutoSize = true, ForeColor = UiTheme.Text, Margin = new Padding(0, 6, 6, 0) });
        _rawValue = new TextBox
        {
            Width = 220,
            Font = UiTheme.Mono(10f),
            BackColor = UiTheme.Elevated,
            ForeColor = UiTheme.TextStrong,
            BorderStyle = BorderStyle.FixedSingle,
        };
        row2.Controls.Add(_rawValue);
        var writeBtn = UiTheme.Compact(UiTheme.Btn("Write + CRC", () =>
        {
            try
            {
                int off = (int)_rawOffset.Value;
                var kind = ParseRawKind(_rawKind.SelectedItem?.ToString());
                var result = RecordEdit($"raw write @ 0x{off:X}", () =>
                {
                    // Mutate without nested CRC from RawWrite — history Apply already CRCs.
                    byte[] payload = CareerStatsService.EncodeRaw(kind, _rawValue.Text);
                    if (off + payload.Length > CareerProfileSize)
                        throw new InvalidOperationException("Write exceeds profile.");
                    Buffer.BlockCopy(payload, 0, _working, off, payload.Length);
                });
                RefreshRawReadout();
                LoadFromBuffer();
                Notify();
                MessageBox.Show(this, result.Summary, "Raw write", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Raw write", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }, primary: true));
        row2.Controls.Add(writeBtn);
        root.Controls.Add(row2);

        root.Controls.Add(UiTheme.SectionHead("Define schema field"));
        var defRow = new FlowLayoutPanel { AutoSize = true, WrapContents = true, BackColor = UiTheme.Bg, Margin = new Padding(0, 0, 0, 6) };
        _rawId = new TextBox { Width = 120, PlaceholderText = "id", Font = UiTheme.UiFont(9f), BackColor = UiTheme.Elevated, ForeColor = UiTheme.TextStrong, BorderStyle = BorderStyle.FixedSingle };
        _rawLabel = new TextBox { Width = 180, PlaceholderText = "label", Font = UiTheme.UiFont(9f), BackColor = UiTheme.Elevated, ForeColor = UiTheme.TextStrong, BorderStyle = BorderStyle.FixedSingle };
        _rawType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
        UiTheme.StyleCombo(_rawType);
        _rawType.Items.AddRange(["int8", "uint8", "int16", "uint16", "int32", "uint32", "float32"]);
        _rawType.SelectedIndex = 4;
        defRow.Controls.Add(_rawId);
        defRow.Controls.Add(_rawLabel);
        defRow.Controls.Add(_rawType);
        defRow.Controls.Add(UiTheme.Compact(UiTheme.Btn("Save to schema", () =>
        {
            try
            {
                int off = (int)_rawOffset.Value;
                string id = string.IsNullOrWhiteSpace(_rawId.Text) ? $"custom_{off:x}" : _rawId.Text.Trim();
                string label = string.IsNullOrWhiteSpace(_rawLabel.Text) ? id : _rawLabel.Text.Trim();
                string type = _rawType.SelectedItem?.ToString() ?? "int32";
                StatsSchemaStore.UpsertUserField(new StatsSchemaField
                {
                    Id = id,
                    Label = label,
                    Section = "User",
                    Status = "probable",
                    Type = type,
                    Offset = $"0x{off:X}",
                    OffsetDec = off,
                    Control = "slider+numeric",
                    Writable = true,
                    Reason = "User-defined from Raw tab.",
                });
                MessageBox.Show(this, $"Saved {id} @ 0x{off:X} to Data/stats.schema.user.json", "Schema",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Schema", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }, info: true)));
        root.Controls.Add(defRow);
        RefreshRawReadout();
    }

    private void RefreshRawReadout()
    {
        if (_rawOffset == null) return;
        int off = (int)_rawOffset.Value;
        string type = _rawKind.SelectedItem?.ToString() ?? "int32";
        string typed = CareerStatsService.FormatTyped(_working, off, type == "hex" ? "hex" : type);
        string hex = CareerStatsService.FormatHex(_working, off, Math.Min(8, CareerProfileSize - off));
        var known = StatsSchemaStore.FindByOffset(off);
        string name = known != null ? $" · {known.Label} [{known.Status}]" : "";
        _rawReadout.Text = $"0x{off:X}: {typed} · hex {hex}{name}";
    }

    private static CareerStatsService.RawValueKind ParseRawKind(string? s) => (s ?? "").ToLowerInvariant() switch
    {
        "int8" => CareerStatsService.RawValueKind.Int8,
        "uint8" => CareerStatsService.RawValueKind.UInt8,
        "int16" => CareerStatsService.RawValueKind.Int16,
        "uint16" => CareerStatsService.RawValueKind.UInt16,
        "uint32" => CareerStatsService.RawValueKind.UInt32,
        "float32" => CareerStatsService.RawValueKind.Float32,
        "hex" => CareerStatsService.RawValueKind.HexBytes,
        _ => CareerStatsService.RawValueKind.Int32,
    };

    // ── WIZARD ──────────────────────────────────────────────────────────────

    private void BuildWizardInto(Panel page)
    {
        var root = MakePageRoot(page);
        root.Controls.Add(UiTheme.SectionHead("Fully Modded wizard"));
        root.Controls.Add(new Label
        {
            Text = "Only proven/probable actions write. Unverified schema fields stay grey and inactive.",
            AutoSize = true,
            MaximumSize = new Size(700, 0),
            ForeColor = UiTheme.Lilac,
            Font = UiTheme.UiFont(8.5f),
            Margin = new Padding(0, 0, 0, 8),
        });

        var presetRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = UiTheme.Bg, Margin = new Padding(0, 0, 0, 8) };
        _wizardPreset = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
        UiTheme.StyleCombo(_wizardPreset);
        foreach (var n in StatsWizardPresets.ListPresetNames().Distinct(StringComparer.OrdinalIgnoreCase))
            _wizardPreset.Items.Add(n);
        _wizardPreset.SelectedIndex = Math.Max(0, _wizardPreset.Items.IndexOf("Max everything"));
        _wizardPreset.SelectedIndexChanged += (_, _) => LoadWizardPreset(_wizardPreset.SelectedItem?.ToString() ?? "Custom");
        presetRow.Controls.Add(_wizardPreset);
        _wizardPresetName = new TextBox
        {
            Width = 140,
            Text = "Custom",
            Font = UiTheme.UiFont(9f),
            BackColor = UiTheme.Elevated,
            ForeColor = UiTheme.TextStrong,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(8, 2, 6, 0),
        };
        presetRow.Controls.Add(_wizardPresetName);
        presetRow.Controls.Add(UiTheme.Compact(UiTheme.Btn("Save preset", () =>
        {
            var preset = CaptureWizardPreset(_wizardPresetName.Text.Trim());
            StatsWizardPresets.SavePreset(preset);
            if (!_wizardPreset.Items.Contains(preset.Name))
                _wizardPreset.Items.Add(preset.Name);
            MessageBox.Show(this, $"Saved {preset.Name}.json", "Preset", MessageBoxButtons.OK, MessageBoxIcon.Information);
        })));
        root.Controls.Add(presetRow);

        foreach (var a in StatsWizardPresets.DefaultActions())
        {
            var row = new FlowLayoutPanel
            {
                AutoSize = true,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, 2),
                BackColor = UiTheme.Bg,
            };
            var on = new CheckBox
            {
                Text = a.Label,
                Checked = a.Enabled,
                AutoSize = true,
                Width = 260,
                ForeColor = UiTheme.Text,
                Font = UiTheme.UiFont(9f),
                Margin = new Padding(0, 4, 8, 0),
            };
            var target = new TextBox
            {
                Width = 100,
                Text = a.Target ?? "",
                Font = UiTheme.Mono(9f),
                BackColor = UiTheme.Elevated,
                ForeColor = UiTheme.TextStrong,
                BorderStyle = BorderStyle.FixedSingle,
                Enabled = !string.IsNullOrEmpty(a.Target) || a.Id.StartsWith("rep_", StringComparison.Ordinal) || a.Id is "bank" or "spent" or "shops_all" or "complete",
            };
            var action = new StatsWizardAction { Id = a.Id, Label = a.Label, Enabled = a.Enabled, Target = a.Target };
            _wizardRows.Add((on, target, action));
            row.Controls.Add(on);
            row.Controls.Add(target);
            root.Controls.Add(row);
        }

        // Grey note for unverified schema fields (non-actions)
        var unverified = StatsSchemaStore.LoadAll()
            .Where(f => f.Status is "unverified" or "runtime")
            .Take(6)
            .ToList();
        if (unverified.Count > 0)
        {
            root.Controls.Add(UiTheme.SectionHead("Unverified (inactive)"));
            foreach (var f in unverified)
            {
                root.Controls.Add(new Label
                {
                    Text = $"· {f.Label} — {f.Reason ?? f.Status}",
                    AutoSize = true,
                    MaximumSize = new Size(700, 0),
                    ForeColor = UiTheme.TextMuted,
                    Font = UiTheme.UiFont(8f),
                    Margin = new Padding(0, 0, 0, 2),
                });
            }
        }

        var apply = UiTheme.Compact(UiTheme.Btn("Apply selected → save", () =>
        {
            var actions = _wizardRows.Select(r =>
            {
                r.Action.Enabled = r.On.Checked;
                r.Action.Target = r.Target.Text.Trim();
                return r.Action;
            }).ToList();
            var result = RecordEdit("wizard apply", () =>
            {
                StatsWizardPresets.Apply(_working, actions);
            });
            LoadFromBuffer();
            Notify();
            _wizardLog.Text = result.Summary;
            MessageBox.Show(this, result.Summary, "Wizard", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }, primary: true));
        apply.Margin = new Padding(0, 10, 0, 6);
        root.Controls.Add(apply);

        _wizardLog = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(700, 0),
            ForeColor = UiTheme.Lilac,
            Font = UiTheme.UiFont(8f),
            Text = "",
        };
        root.Controls.Add(_wizardLog);

        LoadWizardPreset(_wizardPreset.SelectedItem?.ToString() ?? "Max everything");
    }

    private void LoadWizardPreset(string name)
    {
        var preset = StatsWizardPresets.LoadPreset(name) ?? StatsWizardPresets.BuiltIn(name);
        _wizardPresetName.Text = preset.Name;
        foreach (var row in _wizardRows)
        {
            var match = preset.Actions.FirstOrDefault(a => a.Id == row.Action.Id);
            if (match == null) continue;
            row.On.Checked = match.Enabled;
            if (match.Target != null) row.Target.Text = match.Target;
            row.Action.Enabled = match.Enabled;
            row.Action.Target = match.Target;
        }
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
}
