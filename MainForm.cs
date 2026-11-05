using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace NFSU2CustomCarSaves;

public sealed class MainForm : Form
{
    private BrowserSaveSession? _session;
    private SaveRack _rack = SaveRack.Load();
    private readonly ListBox _slots = new();
    private readonly ListBox _rackList = new();
    private readonly Label _status = new();
    private readonly Label _clipStatus = new();
    private readonly Label _dynoStatus = new();
    private readonly ToolTip _tip = new() { AutoPopDelay = 12000, InitialDelay = 350, ReshowDelay = 200, ShowAlways = true, OwnerDraw = true };
    private List<SlotInfo> _slotInfos = new();
    private int _tipSlotsIndex = -2;
    private int _tipRackIndex = -2;
    private readonly DropOverlay _dropOverlay = new();
    private StarfieldControl? _hero;
    private readonly ComboBox _flagPick = new();
    private readonly TextBox _nameBox = new();
    private bool _syncingName;

    private readonly Font _fHeroTitle = new("Segoe UI Semibold", 22f, FontStyle.Bold | FontStyle.Italic);
    private readonly Font _fHeroSub = new("Segoe UI", 9.5f);
    private readonly Font _fHeroTag = new("Segoe UI Semibold", 8.5f);
    private readonly Font _fTip = new("Segoe UI", 9f);

    public MainForm()
    {
        UiTheme.ApplyForm(this);
        Text = "Custom Car Saves";
        try
        {
            string ico = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
            if (File.Exists(ico)) Icon = new Icon(ico);
        }
        catch { /* default icon */ }
        Width = 1060;
        Height = 700;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(940, 600);
        KeyPreview = true;
        AllowDrop = true;
        KeyDown += OnFormKeyDown;
        DragEnter += OnDragEnter;
        DragOver += OnDragOver;
        DragLeave += OnDragLeave;
        DragDrop += OnDragDrop;

        _tip.Popup += TipPopup;
        _tip.Draw += TipDraw;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = UiTheme.Bg,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 118f));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        Controls.Add(root);

        // Animated hero band (logo + title + twinkling stars)
        _hero = new StarfieldControl(110, 11) { Dock = DockStyle.Fill, Margin = new Padding(0) };
        _hero.Overlay = PaintHero;
        root.Controls.Add(_hero, 0, 0);

        // Calm static galaxy body
        var body = new StarfieldControl(60, 23) { Animate = false, Dock = DockStyle.Fill, Margin = new Padding(0) };
        root.Controls.Add(body, 0, 1);

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(22, 12, 22, 12),
            BackColor = Color.Transparent,
        };
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        body.Controls.Add(content);

        // ── Save file ──
        content.Controls.Add(UiTheme.SectionHead("Save file"), 0, 0);
        var fileRow = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true,
            Margin = new Padding(0, 0, 0, 8),
        };
        // Actions first, then player identity (name + flag) — name = nfsu2.xyz / in-game name.
        fileRow.Controls.Add(T(UiTheme.Btn("Start", StartFreshSave, start: true),
            "Create a fresh default save using the Name box (default Player)."));
        fileRow.Controls.Add(T(UiTheme.Btn("Open…", OpenSave, true),
            "Open a career save, GARAGE.DAT, zip or folder."));
        fileRow.Controls.Add(T(UiTheme.Btn("Save zip…", SaveSession, true),
            "Export zip for nfsu2.xyz Restore (includes name + flag in settings.ini)."));
        fileRow.Controls.Add(T(UiTheme.Btn("100% Save", MaxOutSave, info: true),
            "Jane free-play unlocks + max money. Cars in slots stay as they are."));
        fileRow.Controls.Add(BuildIdentityBar());
        fileRow.Controls.Add(T(UiTheme.Btn("Wipe data…", WipeAllData, danger: true),
            "Delete Custom Car Saves data (rack, temp). Game saves stay intact."));
        content.Controls.Add(fileRow, 0, 1);

        // ── Car slots + rack ──
        var mid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Margin = new Padding(0),
        };
        mid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56f));
        mid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44f));
        mid.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // titles
        mid.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // buttons
        mid.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // hints
        mid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // lists
        content.Controls.Add(mid, 0, 2);

        mid.Controls.Add(UiTheme.SectionHead("Car slots"), 0, 0);
        mid.Controls.Add(UiTheme.SectionHead("Save rack"), 1, 0);

        var slotTop = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true,
            Margin = new Padding(0, 0, 0, 4),
        };
        slotTop.Controls.Add(T(UiTheme.Btn("Copy", CopySlot, true), "Copy the selected slot (Ctrl+C)."));
        slotTop.Controls.Add(T(UiTheme.Btn("Paste", PasteSlot, true), "Paste the clipboard car into the selected slot (Ctrl+V)."));
        slotTop.Controls.Add(T(UiTheme.Btn("Unlock", UnlockSelected), "Unlock the selected slot."));
        slotTop.Controls.Add(T(UiTheme.Btn("Add car…", AddOrReplaceCar, info: true), "Place (or replace with) a stock in-game car."));
        slotTop.Controls.Add(T(UiTheme.Btn("Tune…", OpenSelectedTune, accent: true), "Performance, dyno and livery for the selected slot (double-click also works)."));
        mid.Controls.Add(slotTop, 0, 1);

        var rackTop = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true,
            Margin = new Padding(0, 0, 0, 4),
        };
        rackTop.Controls.Add(T(UiTheme.Btn("Clear rack", ClearRackSlot, danger: true), "Empty the selected rack slot."));
        rackTop.Controls.Add(T(UiTheme.Btn("Slot ←", RackToSlot, true), "Rack → selected car slot."));
        rackTop.Controls.Add(T(UiTheme.Btn("→ Rack", SlotToRack, true), "Selected car slot → rack."));
        mid.Controls.Add(rackTop, 1, 1);

        mid.Controls.Add(UiTheme.Muted("Slots 1–10 · double-click = tune", 420), 0, 2);
        mid.Controls.Add(UiTheme.Muted("125 slots · stored locally", 360), 1, 2);

        UiTheme.StyleList(_slots);
        UiTheme.StyleList(_rackList);
        _slots.MouseMove += (_, e) => HoverTip(_slots, e, isRack: false);
        _slots.MouseLeave += (_, _) => { _tipSlotsIndex = -2; _tip.Hide(_slots); };
        _slots.DoubleClick += (_, _) => { try { OpenSelectedTune(); } catch (Exception ex) { Msg(ex.Message); } };
        _slots.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { try { OpenSelectedTune(); e.Handled = true; } catch (Exception ex) { Msg(ex.Message); } }
        };
        _rackList.MouseMove += (_, e) => HoverTip(_rackList, e, isRack: true);
        _rackList.MouseLeave += (_, _) => { _tipRackIndex = -2; _tip.Hide(_rackList); };
        mid.Controls.Add(UiTheme.Card(_slots, new Padding(6)), 0, 3);
        mid.Controls.Add(UiTheme.Card(_rackList, new Padding(6)), 1, 3);

        // ── More tools + status ──
        var bottom = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            AutoSize = true,
            Margin = new Padding(0, 10, 0, 0),
        };
        bottom.Controls.Add(UiTheme.SectionHead("More tools"));
        var tools = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = new Padding(0, 0, 0, 6) };
        tools.Controls.Add(T(UiTheme.Btn("Apply dyno…", ApplyPresetQuick, true), "Apply a saved dyno preset to the selected slot."));
        tools.Controls.Add(T(UiTheme.Btn("Copy dyno", CopyDyno), "Copy the dyno of the selected slot."));
        tools.Controls.Add(T(UiTheme.Btn("Paste dyno", PasteDyno), "Paste the copied dyno onto the selected slot."));
        tools.Controls.Add(T(UiTheme.Btn("Import .carslot", ImportCarslot), "Import a .carslot file into the selected slot."));
        tools.Controls.Add(T(UiTheme.Btn("Export .carslot", ExportCarslot), "Export the selected slot as a .carslot file."));
        tools.Controls.Add(T(UiTheme.Btn("Copy from save…", CopyFromSave), "Copy a slot from another career save."));
        tools.Controls.Add(T(UiTheme.Btn("Guide · Credits", ShowGuideCredits, info: true), "Quick guide and credits (BARKIE · @extrabarkie)."));
        bottom.Controls.Add(tools);

        _status.AutoSize = true;
        _status.ForeColor = UiTheme.Primary;
        _status.Font = UiTheme.UiFont(9f);
        _status.Margin = new Padding(0, 6, 0, 2);
        _status.Text = "Fresh start — drop a zip / Open… · slots empty until you load a save.";
        _clipStatus.AutoSize = true;
        _clipStatus.ForeColor = UiTheme.TextMuted;
        _clipStatus.Text = "Clipboard: empty";
        _dynoStatus.AutoSize = true;
        _dynoStatus.ForeColor = UiTheme.TextMuted;
        _dynoStatus.Text = DynoService.Summarize(null);
        bottom.Controls.Add(_status);
        bottom.Controls.Add(_clipStatus);
        bottom.Controls.Add(_dynoStatus);
        content.Controls.Add(bottom, 0, 3);

        Controls.Add(_dropOverlay);
        _dropOverlay.DragEnter += OnDragEnter;
        _dropOverlay.DragOver += OnDragOver;
        _dropOverlay.DragLeave += OnDragLeave;
        _dropOverlay.DragDrop += OnDragDrop;
        WireDragDrop(this);

        // Fresh slate every launch: no auto-open, empty clipboards, empty slot list.
        BeginFreshSlate();
    }

    private Button T(Button b, string tip)
    {
        _tip.SetToolTip(b, tip);
        return b;
    }

    private Control BuildIdentityBar()
    {
        var host = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(8, 2, 6, 4),
            Padding = new Padding(0),
        };

        var nameLbl = new Label
        {
            Text = "Name",
            AutoSize = true,
            ForeColor = UiTheme.TextMuted,
            Font = UiTheme.UiFont(8.5f),
            Margin = new Padding(0, 8, 6, 0),
        };
        _nameBox.Width = 120;
        _nameBox.MaxLength = NFSU2CustomCarSaves.Layout.NameLength;
        _nameBox.Text = "Player";
        _nameBox.Font = UiTheme.UiFont(9.5f);
        _nameBox.BackColor = UiTheme.Elevated;
        _nameBox.ForeColor = UiTheme.TextStrong;
        _nameBox.BorderStyle = BorderStyle.FixedSingle;
        _nameBox.Margin = new Padding(0, 4, 10, 0);
        _nameBox.Leave += (_, _) => ApplyNameFromBox(quiet: true);
        _nameBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                ApplyNameFromBox(quiet: false);
            }
        };
        _tip.SetToolTip(_nameBox, "Player name on nfsu2.xyz (free roam + profile)");

        var flagLbl = new Label
        {
            Text = "Flag",
            AutoSize = true,
            ForeColor = UiTheme.TextMuted,
            Font = UiTheme.UiFont(8.5f),
            Margin = new Padding(0, 8, 6, 0),
        };
        _flagPick.DropDownStyle = ComboBoxStyle.DropDownList;
        _flagPick.Width = 180;
        _flagPick.IntegralHeight = false;
        _flagPick.MaxDropDownItems = 8;
        _flagPick.DropDownHeight = 200;
        _flagPick.ItemHeight = 22;
        UiTheme.StyleCombo(_flagPick);
        _flagPick.Font = UiTheme.UiFont(9f);
        _flagPick.DrawMode = DrawMode.OwnerDrawFixed;
        _flagPick.DrawItem += DrawFlagPickItem;
        foreach (var opt in RoamFlagPrefs.All)
            _flagPick.Items.Add(opt);

        string want = RoamFlagPrefs.Load();
        int idx = 0;
        for (int i = 0; i < _flagPick.Items.Count; i++)
        {
            if (_flagPick.Items[i] is RoamFlagPrefs.FlagOption o &&
                o.Code.Equals(want, StringComparison.OrdinalIgnoreCase))
            {
                idx = i;
                break;
            }
        }
        _flagPick.SelectedIndex = idx;
        _flagPick.SelectedIndexChanged += (_, _) =>
        {
            if (_flagPick.SelectedItem is RoamFlagPrefs.FlagOption o)
            {
                RoamFlagPrefs.Save(o.Code);
                SetStatus(string.IsNullOrEmpty(o.Code)
                    ? "Free-roam flag: none."
                    : $"Free-roam flag: {o.Label}");
            }
        };
        _tip.SetToolTip(_flagPick, "Free-roam flag on nfsu2.xyz");

        host.Controls.Add(nameLbl);
        host.Controls.Add(_nameBox);
        host.Controls.Add(flagLbl);
        host.Controls.Add(_flagPick);
        return host;
    }

    private void SyncNameBox(string? name)
    {
        _syncingName = true;
        try
        {
            string n = string.IsNullOrWhiteSpace(name) ? "Player" : name.Trim();
            if (n.Length > NFSU2CustomCarSaves.Layout.NameLength)
                n = n[..NFSU2CustomCarSaves.Layout.NameLength];
            _nameBox.Text = n;
        }
        finally { _syncingName = false; }
    }

    private string ReadNameBox()
    {
        string n = (_nameBox.Text ?? "").Trim();
        if (string.IsNullOrEmpty(n)) n = "Player";
        if (n.Length > NFSU2CustomCarSaves.Layout.NameLength)
            n = n[..NFSU2CustomCarSaves.Layout.NameLength];
        return n;
    }

    private void ApplyNameFromBox(bool quiet)
    {
        if (_syncingName) return;
        string newName = ReadNameBox();
        SyncNameBox(newName);
        if (_session == null) return;
        if (!BrowserSaveSession.IsValidProfileName(newName))
        {
            if (!quiet)
                MessageBox.Show(this, $"Invalid name (1–{NFSU2CustomCarSaves.Layout.NameLength} characters).", Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            SyncNameBox(_session.ProfileName);
            return;
        }
        if (newName.Equals(_session.ProfileName, StringComparison.Ordinal) &&
            newName.Equals(_session.ReadInGameName(), StringComparison.Ordinal))
            return;
        try
        {
            _session.Rename(newName);
            RefreshSlots();
            if (!quiet)
                SetStatus($"Player name → {_session.ProfileName} (nfsu2.xyz + in-game). Save zip… to export.");
        }
        catch (Exception ex)
        {
            SyncNameBox(_session.ProfileName);
            if (!quiet)
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void DrawFlagPickItem(object? sender, DrawItemEventArgs e)
    {
        e.DrawBackground();
        if (e.Index < 0 || e.Index >= _flagPick.Items.Count) return;
        if (_flagPick.Items[e.Index] is not RoamFlagPrefs.FlagOption opt) return;

        bool selected = (e.State & DrawItemState.Selected) != 0;
        Color fg = selected ? UiTheme.TextStrong : UiTheme.Text;

        int pad = 4;
        int iconH = Math.Max(10, e.Bounds.Height - 6);
        int iconW = (int)(iconH * 1.5f);
        var icon = RoamFlagPrefs.GetIcon(opt.Code);
        if (icon != null)
        {
            var ir = new Rectangle(e.Bounds.X + pad, e.Bounds.Y + (e.Bounds.Height - iconH) / 2, iconW, iconH);
            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            e.Graphics.DrawImage(icon, ir);
        }
        else if (string.IsNullOrEmpty(opt.Code))
        {
            // none
            using var pen = new Pen(UiTheme.TextMuted, 1.5f);
            int s = Math.Min(14, iconH);
            var r = new Rectangle(e.Bounds.X + pad + 2, e.Bounds.Y + (e.Bounds.Height - s) / 2, s, s);
            e.Graphics.DrawEllipse(pen, r);
            e.Graphics.DrawLine(pen, r.Left + 3, r.Top + 3, r.Right - 3, r.Bottom - 3);
        }

        var textRect = new Rectangle(e.Bounds.X + pad + iconW + 6, e.Bounds.Y, e.Bounds.Width - iconW - pad * 2 - 6, e.Bounds.Height);
        TextRenderer.DrawText(e.Graphics, opt.Label, _flagPick.Font, textRect, fg,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        e.DrawFocusRectangle();
    }

    private void PaintHero(Graphics g, Rectangle r)
    {
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        float pulse = 0.5f + 0.5f * MathF.Sin((_hero?.Time ?? 0f) * 2.2f);
        GalaxyCanvas.DrawLogo(g, GalaxyAssets.LogoFit(168, 92), new Rectangle(18, 10, 168, 92), pulse);

        float x = 200;
        GalaxyCanvas.DrawGradientText(g, "Custom Car Saves", _fHeroTitle, x, 18, UiTheme.Primary, UiTheme.Lilac);
        using (var sub = new SolidBrush(UiTheme.TextMuted))
            g.DrawString("Your garage. Your rules.", _fHeroSub, sub, x, 62);
        using (var tag = new SolidBrush(Color.FromArgb(225, UiTheme.Lilac)))
            g.DrawString("by BARKIE  ·  Discord @extrabarkie", _fHeroTag, tag, x, 84);

        using var line = new LinearGradientBrush(new Rectangle(0, r.Bottom - 2, Math.Max(2, r.Width), 2),
            Color.FromArgb(0, UiTheme.Lilac), Color.FromArgb(150, UiTheme.Primary), 0f);
        g.FillRectangle(line, 0, r.Bottom - 2, r.Width, 2);
    }

    private void TipPopup(object? sender, PopupEventArgs e)
    {
        string text = e.AssociatedControl != null ? _tip.GetToolTip(e.AssociatedControl) : "";
        var sz = TextRenderer.MeasureText(text, _fTip, new Size(440, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        e.ToolTipSize = new Size(sz.Width + 24, sz.Height + 18);
    }

    private void TipDraw(object? sender, DrawToolTipEventArgs e)
    {
        var g = e.Graphics;
        var r = e.Bounds;
        using (var br = new LinearGradientBrush(r, Color.FromArgb(0x26, 0x22, 0x5E), Color.FromArgb(0x14, 0x14, 0x38), 90f))
            g.FillRectangle(br, r);
        using (var pen = new Pen(Color.FromArgb(0x7A, 0x6C, 0xFF)))
            g.DrawRectangle(pen, 0, 0, r.Width - 1, r.Height - 1);
        TextRenderer.DrawText(g, e.ToolTipText, _fTip,
            new Rectangle(12, 9, r.Width - 24, r.Height - 18), UiTheme.TextStrong,
            TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
    }

    private void BeginFreshSlate()
    {
        _session?.Dispose();
        _session = null;
        SlotClipboard.ClearMemory();
        DynoService.ClearClipboard();
        RefreshSlots();
        RefreshRack();
        UpdateClipStatus();
        UpdateDynoStatus();
        SetStatus("Fresh start — drop a zip / Open… · slots empty until you load a save.");
    }

    private void ShowGuideCredits()
    {
        using var guide = new GalaxyGuideForm();
        guide.ShowDialog(this);
    }

    private void WireDragDrop(Control root)
    {
        root.AllowDrop = true;
        root.DragEnter -= OnDragEnter;
        root.DragOver -= OnDragOver;
        root.DragLeave -= OnDragLeave;
        root.DragDrop -= OnDragDrop;
        root.DragEnter += OnDragEnter;
        root.DragOver += OnDragOver;
        root.DragLeave += OnDragLeave;
        root.DragDrop += OnDragDrop;
        foreach (Control child in root.Controls)
        {
            if (ReferenceEquals(child, _dropOverlay)) continue;
            WireDragDrop(child);
        }
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (!TryGetDropPaths(e, out var paths) || !DropImport.LooksLikeDrop(paths))
        {
            e.Effect = DragDropEffects.None;
            return;
        }
        e.Effect = DragDropEffects.Copy;
        _dropOverlay.ShowDrop();
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (!TryGetDropPaths(e, out var paths) || !DropImport.LooksLikeDrop(paths))
        {
            e.Effect = DragDropEffects.None;
            return;
        }
        e.Effect = DragDropEffects.Copy;
        _dropOverlay.ShowDrop();
    }

    private void OnDragLeave(object? sender, EventArgs e)
    {
        // Only hide when the cursor is really outside the window
        var pt = PointToClient(Cursor.Position);
        if (!ClientRectangle.Contains(pt))
            _dropOverlay.HideDrop();
    }

    private void OnDragDrop(object? sender, DragEventArgs e)
    {
        _dropOverlay.HideDrop();
        try
        {
            if (!TryGetDropPaths(e, out var paths))
                throw new InvalidOperationException("No files in the drop.");

            OpenDroppedOrPicked(paths);
            if (_session != null)
                SetStatus($"Dropped: {_session.ProfileName} — {_session.GarageNote}",
                    _session.GarageState == GarageState.Loaded || _session.GaragePath != null);
        }
        catch (Exception ex) { Msg(ex.Message); }
    }

    private static bool TryGetDropPaths(DragEventArgs e, out string[] paths)
    {
        paths = Array.Empty<string>();
        if (e.Data == null) return false;

        // Explorer FileDrop (with autoConvert for some shells)
        object? raw = null;
        if (e.Data.GetDataPresent(DataFormats.FileDrop, autoConvert: true))
            raw = e.Data.GetData(DataFormats.FileDrop, autoConvert: true);
        else if (e.Data.GetDataPresent("FileDrop"))
            raw = e.Data.GetData("FileDrop");
        else if (e.Data.GetDataPresent("FileNameW"))
            raw = e.Data.GetData("FileNameW");
        else if (e.Data.GetDataPresent("FileName"))
            raw = e.Data.GetData("FileName");

        if (raw is string[] arr && arr.Length > 0)
        {
            paths = arr.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim().Trim('"')).ToArray();
            return paths.Length > 0;
        }

        if (raw is string one && !string.IsNullOrWhiteSpace(one))
        {
            paths = [one.Trim().Trim('"')];
            return true;
        }

        return false;
    }

    private void OnFormKeyDown(object? sender, KeyEventArgs e)
    {
        try
        {
            if (e.Control && e.KeyCode == Keys.C) { CopySlot(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.V) { PasteSlot(); e.Handled = true; }
        }
        catch (Exception ex) { Msg(ex.Message); }
    }

    private void StartFreshSave()
    {
        if (_session != null && _session.Dirty)
        {
            var ask = MessageBox.Show(this,
                "You have unsaved edits.\n\nCreate a fresh default save anyway? (current work is discarded unless you Save zip… first)",
                "Start fresh save",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (ask != DialogResult.Yes) return;
        }

        string name = ReadNameBox();
        if (!BrowserSaveSession.IsValidProfileName(name))
            throw new InvalidOperationException($"Invalid name (1–{NFSU2CustomCarSaves.Layout.NameLength} characters).");

        _session?.Dispose();
        _session = BrowserSaveSession.CreateFreshDefault(name);
        SyncNameBox(_session.ProfileName);
        RefreshSlots();
        UpdateClipStatus();
        UpdateDynoStatus();
        SetStatus($"{_session.ProfileName} — fresh default · empty slots 1–10 · free-play ready. Save zip… when done.");
        MessageBox.Show(this,
            $"Fresh save “{_session.ProfileName}” is ready.\n\n" +
            "• Career / shop free-play (Jane-style)\n" +
            "• Slots 1–10 empty & unlocked\n" +
            "• Max money\n\n" +
            "Add cars, tune, then Save zip… and Restore on nfsu2.xyz.",
            "Start",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void OpenSave()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Open career save, GARAGE.DAT, zip, or folder files",
            // *.* first: career saves often have no extension (BARKIE, Joey)
            Filter =
                "All files (*.*)|*.*|" +
                "GARAGE.DAT|GARAGE.DAT;garage.dat|" +
                "Zip / Rar (*.zip;*.rar;*.7z)|*.zip;*.rar;*.7z|" +
                "Dat files (*.dat)|*.dat",
            FilterIndex = 1,
            CheckFileExists = true,
            Multiselect = true,
        };
        if (dlg.ShowDialog(this) != DialogResult.OK || dlg.FileNames.Length == 0)
            return;

        OpenDroppedOrPicked(dlg.FileNames);
    }

    private void OpenDroppedOrPicked(string[] paths)
    {
        if (paths.Length == 1 && Directory.Exists(paths[0]))
        {
            OpenPath(paths[0]);
            return;
        }

        if (paths.Length == 1 && DropImport.Classify(paths[0]) is DropFileKind.Archive)
        {
            OpenPath(paths[0]);
            return;
        }

        // One career file: open the folder next to it (so a sibling GARAGE.DAT comes along)
        if (paths.Length == 1 && DropImport.Classify(paths[0]) == DropFileKind.Career)
        {
            string? dir = Path.GetDirectoryName(paths[0]);
            if (!string.IsNullOrEmpty(dir) && DropImport.FindSiblingGarage(dir) != null)
            {
                OpenPath(dir);
                return;
            }
        }

        var resolved = DropImport.Resolve(paths);
        if (!string.IsNullOrEmpty(resolved.AttachGaragePath))
        {
            if (_session == null)
                throw new InvalidOperationException(
                    "Only GARAGE.DAT selected — also select the career save (or open it first).");
            _session.AttachGarage(resolved.AttachGaragePath);
            RefreshSlots();
            SetStatus($"GARAGE.DAT attached — {_session.GarageNote}");
            return;
        }
        if (string.IsNullOrEmpty(resolved.OpenPath))
            throw new InvalidOperationException("Could not open file(s).");
        OpenPath(resolved.OpenPath!);
    }

    private void OpenPath(string path)
    {
        _session?.Dispose();
        _session = BrowserSaveSession.Open(path);
        SyncNameBox(_session.ProfileName);
        RefreshSlots();
        int harvested = 0;
        try { harvested = TunePresetService.HarvestFromSave(_session.Working, _session.ProfileName); }
        catch { /* non-fatal */ }
        int pack = 0;
        try { pack = TunePresetService.EnsureDynoSavesImported(); }
        catch { /* non-fatal */ }
        int liveries = 0;
        try { liveries = LiveryService.HarvestFromSave(_session.Working, _session.ProfileName); }
        catch { /* non-fatal */ }
        int liveryPack = 0;
        try { liveryPack = LiveryService.ImportFromDynoSavesFolder(); }
        catch { /* non-fatal */ }
        string extra = "";
        if (harvested > 0) extra += $"  ·  +{harvested} dyno from this save";
        if (pack > 0) extra += $"  ·  +{pack} from DYNO SAVES pack";
        if (liveries > 0) extra += $"  ·  +{liveries} livery from this save";
        if (liveryPack > 0) extra += $"  ·  +{liveryPack} livery from DYNO SAVES";
        SetStatus($"{_session.ProfileName} — {_session.GarageNote}{extra}",
            _session.GarageState == GarageState.Loaded);
    }

    private void SetStatus(string text, bool ok = true)
    {
        _status.Text = text;
        _status.ForeColor = ok ? UiTheme.Primary : UiTheme.Warning;
    }

    private void SetOk(string text)
    {
        _status.Text = text;
        _status.ForeColor = UiTheme.Success;
    }

    private void RefreshSlots()
    {
        int keep = _slots.SelectedIndex;
        _slots.Items.Clear();
        _slotInfos = _session?.Slots() ?? new List<SlotInfo>();
        byte[]? working = _session?.Working;
        foreach (var s in _slotInfos)
        {
            string name = CarNames.ListLabel(s.CarHash, s.IsOccupied, s.IsLocked);
            string state = s.IsLocked && !s.IsOccupied ? "Locked"
                : s.IsOccupied ? "Filled"
                : "Open";
            if (!s.IsOccupied && !s.IsLocked)
                name = "(empty)";

            string stats = "";
            if (working != null && s.IsOccupied)
            {
                try
                {
                    var t = CarTuneService.Read(working, s.Index);
                    stats = $"  ·  {t.PerfInstalled}/{t.PerfTotal} perf  ·  {t.UniqueOn}/{t.UniqueTotal} uniq  ·  {t.UnlocksOn}/{t.UnlocksTotal} unlock";
                }
                catch { /* slot out of range */ }
            }

            _slots.Items.Add($"Slot {s.Index + 1}  {state,-6}  {name}{stats}");
        }
        if (_slots.Items.Count == 0) return;
        _slots.SelectedIndex = keep >= 0 && keep < _slots.Items.Count ? keep : 0;
        _tipSlotsIndex = -2;
    }

    private void RefreshRack()
    {
        int keep = _rackList.SelectedIndex;
        _rackList.Items.Clear();
        for (int i = 0; i < SaveRack.Capacity; i++)
            _rackList.Items.Add(_rack.Label(i));
        if (_rackList.Items.Count > 0)
            _rackList.SelectedIndex = keep >= 0 && keep < _rackList.Items.Count ? keep : 0;
        _tipRackIndex = -2;
    }

    private void HoverTip(ListBox lb, MouseEventArgs e, bool isRack)
    {
        int idx = lb.IndexFromPoint(e.Location);
        if (isRack)
        {
            if (idx == _tipRackIndex) return;
            _tipRackIndex = idx;
            if (idx < 0 || idx >= SaveRack.Capacity) { _tip.Hide(lb); return; }
            _tip.SetToolTip(lb, _rack.Tooltip(idx));
        }
        else
        {
            if (idx == _tipSlotsIndex) return;
            _tipSlotsIndex = idx;
            if (idx < 0 || idx >= _slotInfos.Count) { _tip.Hide(lb); return; }
            var s = _slotInfos[idx];
            string state = s.IsLocked
                ? "Locked — Unlock first"
                : s.IsOccupied ? "Filled" : "Empty — Add car / Paste / .carslot";
            _tip.SetToolTip(lb, CarNames.Tooltip(s.CarHash, $"Slot {s.Index + 1} of 10\nState: {state}"));
        }
    }

    private void UpdateClipStatus()
    {
        var p = SlotClipboard.Memory;
        _clipStatus.Text = p == null
            ? "Clipboard: empty  ·  Ctrl+C / Ctrl+V"
            : $"Clipboard: {p.CarName} ({p.XName})  ·  Ctrl+C / Ctrl+V";
    }

    private void UpdateDynoStatus()
    {
        _dynoStatus.Text = DynoService.Summarize(DynoService.ClipboardPkg)
            + "  ·  Copy dyno → open another save → Paste dyno (works on any car)";
    }

    private void CopyDyno()
    {
        int src = SelectedSlot();
        var pkg = DynoService.Export(_session!.Working, src);
        DynoService.CopyToClipboard(pkg);
        UpdateDynoStatus();
        SetStatus($"Dyno copied: {pkg.SourceCar} (slot {src + 1}) — paste on another save/car");
    }

    private void PasteDyno()
    {
        int dst = SelectedSlot();
        var pkg = DynoService.PasteFromClipboard()
                  ?? throw new InvalidOperationException("No dyno copied — use Copy dyno first.");
        DynoService.Apply(_session!.Working, dst, pkg);
        _session.Dirty = true;
        RefreshSlots();
        UpdateDynoStatus();
        SetStatus($"Dyno pasted → slot {dst + 1} (source: {pkg.SourceCar})");
    }

    private void ApplyPresetQuick()
    {
        int slot = SelectedSlot();
        var info = GarageDatService.ReadSlot(_session!.Working, slot);
        uint carHash = info.CarHash;
        string want = carHash == 0 ? "" : carHash.ToString("X8");
        var all = TunePresetService.LoadDynoPresets(refresh: true);
        var presets = carHash == 0
            ? all
            : all.Where(p =>
                !string.IsNullOrWhiteSpace(p.CarHash) &&
                p.CarHash.Equals(want, StringComparison.OrdinalIgnoreCase)).ToList();
        if (all.Count == 0)
            throw new InvalidOperationException(
                "No dyno presets yet. Open a save (auto-harvest) or save a car from Tune → Dyno.");
        if (presets.Count == 0)
            throw new InvalidOperationException(
                $"No dyno presets for {info.DisplayName} yet. Open a save with that model, or save this car from Tune → Dyno.");

        using var f = new Form
        {
            Text = carHash == 0 ? "Apply dyno preset" : $"Apply dyno preset — {info.DisplayName}",
            Width = 620,
            Height = 500,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            FormBorderStyle = FormBorderStyle.FixedDialog,
        };
        UiTheme.ApplyForm(f);
        var tip = UiTheme.Muted(
            carHash == 0
                ? "Showing all presets (empty slot). Label shows where the tune came from. Vinyl/livery is never overwritten."
                : $"Only presets for {info.DisplayName}. Vinyl/livery is never overwritten.",
            580);
        tip.Dock = DockStyle.Top;
        tip.Padding = new Padding(8, 8, 8, 4);
        var list = new ListBox { Dock = DockStyle.Fill };
        UiTheme.StyleList(list);
        foreach (var p in presets) list.Items.Add(p);
        if (list.Items.Count > 0) list.SelectedIndex = 0;
        var note = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 64,
            ForeColor = UiTheme.TextMuted,
            Font = UiTheme.UiFont(8.5f),
            Padding = new Padding(8, 4, 8, 0),
        };
        void ShowNote()
        {
            if (list.SelectedItem is TunePreset p)
                note.Text =
                    $"{p.CarName} · from {p.SaveName}" +
                    (p.Slot > 0 ? $" (source slot {p.Slot})" : "") +
                    $"\n{p.AeroNote}\nVinyl/livery stays on your car.";
        }
        var upgrades = new CheckBox
        {
            Text = "Apply required parts (only for +parts presets)",
            Checked = true,
            AutoSize = true,
            Dock = DockStyle.Bottom,
            ForeColor = UiTheme.Text,
            Padding = new Padding(8, 2, 8, 2),
        };
        var copyBody = new CheckBox
        {
            Text = "Also copy bumper/spoiler/widebody (same car only — not vinyl)",
            Checked = false,
            AutoSize = true,
            Dock = DockStyle.Bottom,
            ForeColor = UiTheme.TextMuted,
            Padding = new Padding(8, 2, 8, 4),
        };
        void SyncPartsChecks()
        {
            bool needs = list.SelectedItem is TunePreset p && p.NeedsParts && p.HasUpgrades;
            upgrades.Enabled = needs;
            upgrades.Checked = needs;
            copyBody.Enabled = needs;
            if (!needs) copyBody.Checked = false;
        }
        list.SelectedIndexChanged += (_, _) => { ShowNote(); SyncPartsChecks(); };
        ShowNote();
        SyncPartsChecks();

        void DoApply()
        {
            if (list.SelectedItem is not TunePreset p) return;
            string what = TunePresetService.ApplyDyno(_session!.Working, slot, p, upgrades.Checked, copyBody.Checked);
            _session.Dirty = true;
            f.DialogResult = DialogResult.OK;
            f.Close();
            RefreshSlots();
            SetStatus($"Dyno → slot {slot + 1}: {p.DisplayLabel} ({what})");
        }

        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8, 8, 8, 4),
            BackColor = UiTheme.Bg,
        };
        bar.Controls.Add(UiTheme.Btn("Apply dyno", DoApply, primary: true));
        bar.Controls.Add(UiTheme.Btn("Cancel", () => { f.DialogResult = DialogResult.Cancel; f.Close(); }));
        f.Controls.Add(list);
        f.Controls.Add(note);
        f.Controls.Add(copyBody);
        f.Controls.Add(upgrades);
        f.Controls.Add(bar);
        f.Controls.Add(tip);
        list.DoubleClick += (_, _) => DoApply();
        f.ShowDialog(this);
    }

    private int SelectedSlot()
    {
        if (_session == null) throw new InvalidOperationException("Open a save first.");
        if (_slots.SelectedIndex < 0) throw new InvalidOperationException("Select a save slot on the left.");
        return _slots.SelectedIndex;
    }

    private int SelectedRack()
    {
        if (_rackList.SelectedIndex < 0) throw new InvalidOperationException("Select a rack slot on the right.");
        return _rackList.SelectedIndex;
    }

    private void CopySlot()
    {
        int src = SelectedSlot();
        var pkg = _session!.ExportPackage(src, _session.ProfileName);
        SlotClipboard.Copy(pkg);
        UpdateClipStatus();
        SetOk($"Copied: slot {src + 1} — {pkg.CarName}");
    }

    private void PasteSlot()
    {
        int dst = SelectedSlot();
        var pkg = SlotClipboard.Paste()
                  ?? throw new InvalidOperationException("Clipboard is empty — copy a slot first.");
        _session!.ImportPackage(pkg, dst);
        RefreshSlots();
        UpdateClipStatus();
        SetOk($"Pasted → slot {dst + 1}: {pkg.CarName}");
    }

    private void SlotToRack()
    {
        int src = SelectedSlot();
        int rack = SelectedRack();
        var pkg = _session!.ExportPackage(src, _session.ProfileName);
        if (_rack.Get(rack) != null)
        {
            if (MessageBox.Show(this, $"Overwrite rack R{rack + 1}?", Text,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
        }
        _rack.Set(rack, pkg);
        RefreshRack();
        SetOk($"Slot {src + 1} → rack R{rack + 1} ({pkg.CarName})");
    }

    private void RackToSlot()
    {
        int rack = SelectedRack();
        int dst = SelectedSlot();
        var pkg = _rack.Get(rack)
                  ?? throw new InvalidOperationException($"Rack R{rack + 1} is empty.");
        _session!.ImportPackage(pkg, dst);
        RefreshSlots();
        SetOk($"Rack R{rack + 1} → slot {dst + 1}: {pkg.CarName}");
    }

    private void ClearRackSlot()
    {
        int rack = SelectedRack();
        if (_rack.Get(rack) == null) return;
        if (MessageBox.Show(this, $"Clear rack R{rack + 1}?", Text,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        _rack.Clear(rack);
        RefreshRack();
        _status.Text = $"Rack R{rack + 1} cleared.";
    }

    private void ImportCarslot()
    {
        int dst = SelectedSlot();
        using var dlg = new OpenFileDialog
        {
            Filter = "Car slot (*.carslot.json;*.json)|*.carslot.json;*.json",
            Title = "Import .carslot",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var pkg = CarSlotIo.Load(dlg.FileName);
        _session!.ImportPackage(pkg, dst);
        RefreshSlots();
        SetOk($"Imported → slot {dst + 1}: {pkg.CarName}");
    }

    private void CopyFromSave()
    {
        int dst = SelectedSlot();
        using var dlg = new OpenFileDialog
        {
            Filter = "Career save / all|*.*|20CM career|*",
            Title = "Source career save",
            CheckFileExists = true,
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        string? raw = Prompt("Source slot number (1–10):", "1");
        if (raw == null) return;
        if (!int.TryParse(raw.Trim(), out int srcHuman) || srcHuman < 1 || srcHuman > 10)
            throw new InvalidOperationException("Invalid slot number.");

        _session!.CopyFromCareerFile(dlg.FileName, srcHuman - 1, dst);
        RefreshSlots();
        SetOk($"Copied from slot {srcHuman} → slot {dst + 1}");
    }

    private void ExportCarslot()
    {
        int src = SelectedSlot();
        var pkg = _session!.ExportPackage(src, _session.ProfileName);
        using var dlg = new SaveFileDialog
        {
            Filter = "Car slot (*.carslot.json)|*.carslot.json",
            FileName = Sanitize(pkg.CarName) + ".carslot.json",
            Title = "Export .carslot",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        CarSlotIo.Save(pkg, dlg.FileName);
        SetOk("Exported: " + dlg.FileName);
    }

    private void UnlockSelected()
    {
        int i = SelectedSlot();
        _session!.Unlock(i);
        RefreshSlots();
        SetStatus($"Slot {i + 1} unlocked.");
    }

    private void AddOrReplaceCar()
    {
        if (_session == null) throw new InvalidOperationException("Open a save first.");
        int slot = SelectedSlot();
        var info = GarageDatService.ReadSlot(_session.Working, slot);
        bool replace = info.IsOccupied;
        using var dlg = new AddCarForm(replace, info.DisplayName);
        if (dlg.ShowDialog(this) != DialogResult.OK || dlg.Selected == null) return;

        if (replace)
        {
            if (MessageBox.Show(this,
                    $"Replace {info.DisplayName} in slot {slot + 1} with:\n{dlg.Selected.Name}?\n\n" +
                    "Stock body is written. Dyno region is kept if present — open Tune for dyno/livery after.",
                    "Replace car", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
        }

        _session.PlaceDonorCar(slot, dlg.Selected);
        RefreshSlots();
        SetStatus($"{(replace ? "Replaced" : "Added")} → slot {slot + 1}: {dlg.Selected.Name}");
    }

    private void OpenSelectedTune()
    {
        int i = SelectedSlot();
        _session!.OpenTuneEditor(i, this, () =>
        {
            RefreshSlots();
            SetStatus($"Slot {i + 1} updated — don't forget Save zip…");
        });
        RefreshSlots();
    }

    private void MaxOutSave()
    {
        if (_session == null) throw new InvalidOperationException("Open a save first.");
        if (MessageBox.Show(this,
                "100% Save (Jane free-play, browser-safe)?\n\n" +
                "• Career / free roam like Jane (not stuck in Rachel)\n" +
                "• Car shop / lot fully unlocked\n" +
                "• Hidden shops unlocked\n" +
                "• All garage slots unlocked\n" +
                "• Money → 99,999,999\n" +
                "• Cars already in slots are LEFT UNCHANGED (no retune)\n\n" +
                "After this: Save zip… then on nfsu2.xyz use Restore.\n" +
                "Confirm dialog must say “Restore this backup?” (not “Add profile”).\n" +
                "If it says GARAGE.DAT is left out → slots 6–10 will not load.",
                "100% Save",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        string report = _session.MaxOut();
        RefreshSlots();
        SetStatus("100% Save: " + report);
        MessageBox.Show(this, report + "\n\nPress Save zip… to write the backup.", "100% Save complete",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void SaveSession()
    {
        if (_session == null) throw new InvalidOperationException("Open a save first.");
        ApplyNameFromBox(quiet: true);
        using var dlg = new SaveFileDialog
        {
            Filter = "Zip (*.zip)|*.zip",
            FileName = _session.SuggestZipFileName(),
            Title = "Save as zip",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            OverwritePrompt = true,
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        _session.ExportZip(dlg.FileName);
        RefreshSlots();
        SetStatus("Zip saved: " + dlg.FileName);
        MessageBox.Show(this,
            "Zip includes registry.txt + settings.ini so nfsu2.xyz keeps GARAGE.DAT (slots 6–10).\n\n" +
            "On nfsu2.xyz → Saves → Restore a backup.\n" +
            "Confirm must be “Restore this backup?” — not “Add this profile?”.\n" +
            "If the dialog says GARAGE.DAT is left out, cancel and use this new zip.\n\n" +
            "Note: Restore replaces profiles + settings.ini in that browser with this zip.",
            "Save zip",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void WipeAllData()
    {
        var ask = MessageBox.Show(this,
            "Delete all Custom Car Saves data?\n\n" +
            "• Save rack (125 slots)\n" +
            "• Temp files\n" +
            "• Carslot clipboard\n\n" +
            "Your game saves / NFSU2 BROWSER SAVE stay intact.\n\n" +
            "Yes = delete  ·  No = cancel",
            "Wipe / uninstall data",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (ask != DialogResult.Yes) return;

        bool desk = MessageBox.Show(this,
            "Also delete Desktop backups?\n(folders NFSU2CustomCarSaves_Backup_*)",
            "Desktop backups",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question) == DialogResult.Yes;

        // Close open session first so temp folders can be deleted
        _session?.Dispose();
        _session = null;
        RefreshSlots();

        var report = Uninstaller.Wipe(includeDesktopBackups: desk, clearClipboard: true);
        _rack = SaveRack.Load();
        RefreshRack();
        UpdateClipStatus();

        MessageBox.Show(this, Uninstaller.Summarize(report), "Wipe complete",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
        _status.Text = "App data cleared. Open a save again to continue.";
        _status.ForeColor = UiTheme.Amber;

        if (MessageBox.Show(this,
                "Exit the program now?\n(You can delete the .exe folder manually afterward.)",
                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            Close();
    }

    private static string Sanitize(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "car" : name.Trim();
    }

    private string? Prompt(string label, string initial)
    {
        using var f = new Form
        {
            Text = Text,
            ClientSize = new Size(420, 150),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
        };
        UiTheme.ApplyForm(f);

        var lbl = new Label { Text = label, Left = 18, Top = 16, AutoSize = true, MaximumSize = new Size(384, 0), ForeColor = UiTheme.Text };
        var tb = new TextBox
        {
            Left = 18,
            Top = 46,
            Width = 384,
            Text = initial,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = UiTheme.Elevated,
            ForeColor = UiTheme.TextStrong,
            Font = UiTheme.UiFont(10f),
        };
        var ok = UiTheme.Btn("OK", () => f.DialogResult = DialogResult.OK, primary: true);
        var cancel = UiTheme.Btn("Cancel", () => f.DialogResult = DialogResult.Cancel);
        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 54,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(14, 8, 14, 4),
            BackColor = UiTheme.BgDeep,
        };
        bar.Controls.Add(ok);
        bar.Controls.Add(cancel);
        f.Controls.Add(lbl);
        f.Controls.Add(tb);
        f.Controls.Add(bar);
        f.AcceptButton = ok;
        f.CancelButton = cancel;
        f.ActiveControl = tb;
        return f.ShowDialog(this) == DialogResult.OK ? tb.Text : null;
    }

    private void Msg(string text) =>
        MessageBox.Show(this, text, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _session?.Dispose();
        _fHeroTitle.Dispose();
        _fHeroSub.Dispose();
        _fHeroTag.Dispose();
        _fTip.Dispose();
        base.OnFormClosed(e);
    }
}
