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
    private readonly Label _liveryStatus = new();
    private readonly Label _slotHint = new();
    private readonly ToolTip _tip = new() { AutoPopDelay = 12000, InitialDelay = 350, ReshowDelay = 200, ShowAlways = true, OwnerDraw = true };
    private List<SlotInfo> _slotInfos = new();
    private List<MyCarInfo> _myCarInfos = new();
    /// <summary>False = Career garage 1–10; True = main-menu Customize → My Cars (20).</summary>
    private bool _menuMyCars;
    private int _tipSlotsIndex = -2;
    private int _tipRackIndex = -2;
    private Button? _btnAreaCareer;
    private Button? _btnAreaMyCars;
    private Button? _btnStealTransfer;
    private Button? _btnUnlock;
    private Button? _btnRackCars;
    private Button? _btnRackLiveries;
    private Button? _btnRackClear;
    private Button? _btnRackToSlot;
    private Button? _btnSlotToRack;
    private Button? _btnRackReload;
    private readonly Label _rackHint = new();
    private CheckBox? _liverySameCarOnly;
    /// <summary>False = car Save rack; True = livery library in the right list.</summary>
    private bool _rackShowLiveries;
    private readonly DropOverlay _dropOverlay = new();
    private StarfieldControl? _hero;
    private PictureBox? _settingsBtn;
    private TableLayoutPanel? _rootLayout;
    private readonly ComboBox _flagPick = new();
    private readonly ComboBox _txPick = new();
    private readonly TextBox _nameBox = new();
    private readonly TextBox _moneyBox = new();
    private bool _syncingName;
    private bool _syncingTx;
    private bool _syncingFlag;
    private bool _syncingMoney;
    private bool _moneyTouched;

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
        // Beta.7-style window, resizable + maximize (Windows maximize stays above the taskbar).
        Width = 1060;
        Height = 700;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(940, 600);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = true;
        Resize += (_, _) => AdaptHeroHeight();
        KeyPreview = true;
        AllowDrop = true;
        KeyDown += OnFormKeyDown;
        DragEnter += OnDragEnter;
        DragOver += OnDragOver;
        DragLeave += OnDragLeave;
        DragDrop += OnDragDrop;

        _tip.Popup += TipPopup;
        _tip.Draw += TipDraw;

        _rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = UiTheme.Bg,
        };
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 118f));
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        Controls.Add(_rootLayout);

        // Animated hero band (logo + title + twinkling stars)
        _hero = new StarfieldControl(110, 11) { Dock = DockStyle.Fill, Margin = new Padding(0) };
        _hero.Overlay = PaintHero;
        _rootLayout.Controls.Add(_hero, 0, 0);
        PlaceHeroSettingsButton();

        // Calm static galaxy body
        var body = new StarfieldControl(60, 23) { Animate = false, Dock = DockStyle.Fill, Margin = new Padding(0) };
        _rootLayout.Controls.Add(body, 0, 1);

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(22, 6, 22, 4),
            BackColor = Color.Transparent,
        };
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        body.Controls.Add(content);

        // ── Save file (tight block so Car slots can grow upward) ──
        var saveSection = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 3,
            Margin = new Padding(0, 0, 0, 0),
            Padding = new Padding(0),
        };
        saveSection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        var saveHead = UiTheme.SectionHead("Save file");
        saveHead.Margin = new Padding(0, 0, 0, 2);
        saveSection.Controls.Add(saveHead, 0, 0);

        // Identity fields on top; action buttons underneath (Wipe right next to 100% Save).
        var fileBlock = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
        };
        fileBlock.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        fileBlock.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        fileBlock.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        fileBlock.Controls.Add(BuildIdentityBar(), 0, 0);

        var actions = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true,
            Margin = new Padding(0, 2, 0, 0),
        };
        actions.Controls.Add(T(UiTheme.Btn("Start", StartFreshSave, start: true),
            "Empty post-Rachel freeroam base (cars wiped, starter cash). Use 100% Save / Stats for full unlocks."));
        actions.Controls.Add(T(UiTheme.Btn("Open…", OpenSave, true),
            "Open a career save, GARAGE.DAT, zip or folder."));
        actions.Controls.Add(T(UiTheme.Btn("Save zip…", SaveSession, true),
            "Export zip for nfsu2.xyz Restore (includes name + flag in settings.ini)."));
        actions.Controls.Add(T(UiTheme.Btn("100% Save", MaxOutSave, info: true),
            "Jane free-play unlocks + max money. Cars in slots stay as they are."));
        actions.Controls.Add(T(UiTheme.Btn("Wipe data…", WipeAllData, dangerBright: true),
            "Clear temp / session / work folders and clipboards only. Does not empty racks or dyno/livery libraries. Game saves stay intact."));
        actions.Controls.Add(T(UiTheme.Btn("Delete racks…", DeleteRacks, danger: true),
            "Empty all racks and libraries (cars, dyno, liveries), then restore stock lists. Game saves stay intact."));
        fileBlock.Controls.Add(actions, 0, 1);
        saveSection.Controls.Add(fileBlock, 0, 1);
        content.Controls.Add(saveSection, 0, 0);

        // ── Car slots + rack (takes leftover height — sits tight under Save file) ──
        var mid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5,
            Margin = new Padding(0, 2, 0, 0),
        };
        mid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56f));
        mid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44f));
        mid.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // titles
        mid.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // area tabs
        mid.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // action buttons
        mid.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // hints
        mid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // lists
        content.Controls.Add(mid, 0, 1);

        var carHead = UiTheme.SectionHead("Car slots");
        carHead.Margin = new Padding(0, 0, 0, 2);
        var rackHead = UiTheme.SectionHead("Save rack");
        rackHead.Margin = new Padding(0, 0, 0, 2);
        mid.Controls.Add(carHead, 0, 0);
        mid.Controls.Add(rackHead, 1, 0);

        // Tabs: Career garage vs in-game "Customize Car Select" (My Cars)
        var areaTabs = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true,
            Margin = new Padding(0, 0, 0, 2),
        };
        _btnAreaCareer = UiTheme.Btn("Career garage", () => SetCarArea(menuMyCars: false), primary: true);
        _btnAreaMyCars = UiTheme.Btn("Customize Car Select", () => SetCarArea(menuMyCars: true));
        if (_btnAreaCareer is GlowButton gCareer0) gCareer0.Kind = GlowKind.PinkDark;
        if (_btnAreaMyCars is GlowButton gMy0) gMy0.Kind = GlowKind.Pink;
        areaTabs.Controls.Add(T(_btnAreaCareer,
            "Career garage slots 1–10 (profile + GARAGE.DAT)."));
        areaTabs.Controls.Add(T(_btnAreaMyCars,
            "In-game: Main menu → Customize → Customize Car Select (up to 20 tuned cars). Copy here, then paste into Career."));
        mid.Controls.Add(areaTabs, 0, 1);

        // Same row as area tabs — fills the gap under “Save rack” (Cars / Liveries).
        var rackTabs = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true,
            Margin = new Padding(0, 0, 0, 2),
        };
        _btnRackCars = UiTheme.Compact(UiTheme.Btn("Cars", () => SetRackLibrary(liveries: false), primary: true));
        _btnRackLiveries = UiTheme.Compact(UiTheme.Btn("Liveries", () => SetRackLibrary(liveries: true)));
        if (_btnRackCars is GlowButton gRackCars0) gRackCars0.Kind = GlowKind.PinkDark;
        if (_btnRackLiveries is GlowButton gRackLiv0) gRackLiv0.Kind = GlowKind.Pink;
        rackTabs.Controls.Add(T(_btnRackCars, "Unlimited car Save rack — store / recall full cars."));
        rackTabs.Controls.Add(T(_btnRackLiveries, "Livery library — apply paint/vinyl/decals to the selected car."));
        mid.Controls.Add(rackTabs, 1, 1);

        var slotTop = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true,
            Margin = new Padding(0, 0, 0, 4),
        };
        slotTop.Controls.Add(T(UiTheme.Btn("Copy", CopySlot, true), "Copy full car (body + dyno + livery + tune). Works on Career and Customize Car Select."));
        slotTop.Controls.Add(T(UiTheme.Btn("Paste", PasteSlot, true), "Paste the clipboard car into the selected slot (Ctrl+V)."));
        _btnStealTransfer = T(UiTheme.Btn("Steal → Customize…", StealTransfer, accent: true),
            "Copy the selected Career car into a Customize Car Select slot.");
        slotTop.Controls.Add(_btnStealTransfer);
        _btnUnlock = T(UiTheme.Btn("Unlock", UnlockSelected), "Unlock the selected Career slot (not used for Customize cars).");
        slotTop.Controls.Add(_btnUnlock);
        slotTop.Controls.Add(T(UiTheme.Btn("Add car…", AddOrReplaceCar, info: true), "Place (or replace with) a stock in-game car."));
        slotTop.Controls.Add(T(UiTheme.Btn("Tune…", OpenSelectedTune, accent: true), "Performance, dyno and livery for Career or Customize (double-click also works)."));
        SyncStealButton();
        mid.Controls.Add(slotTop, 0, 2);

        // Same Slot ← / → Rack row for both right tabs — labels + actions follow Cars vs Liveries.
        var rackActions = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true,
            Margin = new Padding(0, 0, 0, 4),
        };
        _btnRackClear = T(UiTheme.Btn("Clear", ClearRackOrLivery, danger: true), "");
        _btnRackToSlot = T(UiTheme.Btn("Slot ←", RackListToSelectedSlot, true), "");
        _btnSlotToRack = T(UiTheme.Btn("→ Rack", SelectedSlotToRackList, true), "");
        _btnRackReload = T(UiTheme.Btn("Reload", () => RefreshRightLibrary(refreshLiveries: true)),
            "Reload liveries from library + DYNO SAVES.");
        _liverySameCarOnly = new CheckBox
        {
            Text = "Same car",
            AutoSize = true,
            Checked = true,
            ForeColor = UiTheme.TextMuted,
            Font = UiTheme.UiFont(8.5f),
            Margin = new Padding(8, 6, 0, 0),
            BackColor = Color.Transparent,
            Visible = false,
        };
        _tip.SetToolTip(_liverySameCarOnly, "Only show liveries matching the selected car’s model.");
        _liverySameCarOnly.CheckedChanged += (_, _) => { if (_rackShowLiveries) RefreshRightLibrary(); };
        rackActions.Controls.Add(_btnRackClear);
        rackActions.Controls.Add(_btnRackToSlot);
        rackActions.Controls.Add(_btnSlotToRack);
        rackActions.Controls.Add(_btnRackReload);
        rackActions.Controls.Add(_liverySameCarOnly);
        mid.Controls.Add(rackActions, 1, 2);
        SyncRackActionButtons();

        _slotHint.AutoSize = true;
        _slotHint.ForeColor = UiTheme.TextMuted;
        _slotHint.Font = UiTheme.UiFont(8.5f);
        _slotHint.Margin = new Padding(0, 0, 0, 2);
        _slotHint.MaximumSize = new Size(520, 0);
        _slotHint.Text = "Career garage 1–10 · double-click = tune";
        mid.Controls.Add(_slotHint, 0, 3);

        _rackHint.AutoSize = true;
        _rackHint.ForeColor = UiTheme.TextMuted;
        _rackHint.Font = UiTheme.UiFont(8.5f);
        _rackHint.Margin = new Padding(0, 0, 0, 2);
        _rackHint.MaximumSize = new Size(420, 0);
        _rackHint.Text = "Cars rack · → Rack adds · Slot ← recalls";
        mid.Controls.Add(_rackHint, 1, 3);

        UiTheme.StyleList(_slots);
        UiTheme.StyleList(_rackList);
        _slots.MouseMove += (_, e) => HoverTip(_slots, e, isRack: false);
        _slots.MouseLeave += (_, _) => { _tipSlotsIndex = -2; _tip.Hide(_slots); };
        _slots.DoubleClick += (_, _) => { try { OpenSelectedTune(); } catch (Exception ex) { Msg(ex.Message); } };
        _slots.SelectedIndexChanged += (_, _) =>
        {
            if (_rackShowLiveries && _liverySameCarOnly?.Checked == true)
                RefreshRightLibrary();
        };
        _slots.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { try { OpenSelectedTune(); e.Handled = true; } catch (Exception ex) { Msg(ex.Message); } }
        };
        _rackList.MouseMove += (_, e) => HoverTip(_rackList, e, isRack: true);
        _rackList.MouseLeave += (_, _) => { _tipRackIndex = -2; _tip.Hide(_rackList); };
        _rackList.DoubleClick += (_, _) =>
        {
            try { RackListToSelectedSlot(); } catch (Exception ex) { Msg(ex.Message); }
        };
        mid.Controls.Add(UiTheme.Card(_slots, new Padding(6)), 0, 4);
        mid.Controls.Add(UiTheme.Card(_rackList, new Padding(6)), 1, 4);

        // ── More tools + status ──
        var bottom = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            AutoSize = true,
            Margin = new Padding(0, 4, 0, 0),
        };
        bottom.Controls.Add(UiTheme.SectionHead("More tools"));
        var tools = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = new Padding(0, 0, 0, 6) };
        tools.Controls.Add(T(UiTheme.Btn("Apply dyno…", ApplyPresetQuick, true), "Apply a saved dyno preset to Career or Customize Car Select."));
        tools.Controls.Add(T(UiTheme.Btn("Copy dyno", CopyDyno), "Copy dyno from Career or Customize Car Select."));
        tools.Controls.Add(T(UiTheme.Btn("Paste dyno", PasteDyno), "Paste dyno onto Career or Customize Car Select."));
        tools.Controls.Add(T(UiTheme.Btn("Copy livery", CopyLivery), "Copy paint/vinyl/decals/neon from Career or Customize Car Select."));
        tools.Controls.Add(T(UiTheme.Btn("Paste livery", PasteLivery), "Paste livery onto Career or Customize Car Select."));
        tools.Controls.Add(T(UiTheme.Btn("Import .carslot", ImportCarslot), "Import a .carslot file into the selected slot."));
        tools.Controls.Add(T(UiTheme.Btn("Export .carslot", ExportCarslot), "Export the selected slot as a .carslot file."));
        tools.Controls.Add(T(UiTheme.Btn("Copy from save…", CopyFromSave), "Copy a slot from another career save."));
        tools.Controls.Add(T(UiTheme.Btn("Guide · Credits", ShowGuideCredits, info: true), "Quick guide and credits (BARKIE · @extrabarkie)."));
        bottom.Controls.Add(tools);

        _status.AutoSize = true;
        _status.ForeColor = UiTheme.Primary;
        _status.Font = UiTheme.UiFont(9f);
        _status.Margin = new Padding(0, 2, 0, 2);
        _status.Text = "Fresh start — drop a zip / Open… · slots empty until you load a save.";
        _clipStatus.AutoSize = true;
        _clipStatus.ForeColor = UiTheme.TextMuted;
        _clipStatus.Text = "Clipboard: empty";
        _dynoStatus.AutoSize = true;
        _dynoStatus.ForeColor = UiTheme.TextMuted;
        _dynoStatus.Text = DynoService.Summarize(null);
        _liveryStatus.AutoSize = true;
        _liveryStatus.ForeColor = UiTheme.TextMuted;
        _liveryStatus.Text = LiveryService.SummarizeClipboard(null);
        bottom.Controls.Add(_status);
        bottom.Controls.Add(_clipStatus);
        bottom.Controls.Add(_dynoStatus);
        bottom.Controls.Add(_liveryStatus);
        content.Controls.Add(bottom, 0, 2);

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
            Margin = new Padding(0, 0, 6, 0),
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

        // Fresh app session: Name = Player, Flag = (none) — not last roam_flag.txt.
        _flagPick.SelectedIndexChanged += (_, _) =>
        {
            if (_syncingFlag) return;
            if (_flagPick.SelectedItem is RoamFlagPrefs.FlagOption o)
            {
                RoamFlagPrefs.Save(o.Code);
                SetStatus(string.IsNullOrEmpty(o.Code)
                    ? "Free-roam flag: none."
                    : $"Free-roam flag: {o.Label}");
            }
        };
        ApplyDefaultIdentity();
        _tip.SetToolTip(_flagPick, "Free-roam flag on nfsu2.xyz");

        var txLbl = new Label
        {
            Text = "Transmission",
            AutoSize = true,
            ForeColor = UiTheme.TextMuted,
            Font = UiTheme.UiFont(8.5f),
            Margin = new Padding(8, 8, 6, 0),
        };
        _txPick.DropDownStyle = ComboBoxStyle.DropDownList;
        _txPick.Width = 110;
        UiTheme.StyleCombo(_txPick);
        _txPick.Font = UiTheme.UiFont(9f);
        _txPick.Margin = new Padding(0, 4, 0, 0);
        _txPick.Items.Add("Automatic");
        _txPick.Items.Add("Manual");
        _txPick.SelectedIndex = 0;
        _txPick.SelectedIndexChanged += (_, _) => ApplyTransmissionFromPick();
        _tip.SetToolTip(_txPick, "In-game Options → Player → Transmission (saved in career profile).");

        var moneyLbl = new Label
        {
            Text = "Money",
            AutoSize = true,
            ForeColor = UiTheme.TextMuted,
            Font = UiTheme.UiFont(8.5f),
            Margin = new Padding(8, 8, 6, 0),
        };
        _moneyBox.Width = 96;
        _moneyBox.Text = "";
        _moneyBox.Font = UiTheme.UiFont(9.5f);
        _moneyBox.BackColor = UiTheme.Elevated;
        _moneyBox.ForeColor = UiTheme.TextStrong;
        _moneyBox.BorderStyle = BorderStyle.FixedSingle;
        _moneyBox.Margin = new Padding(0, 4, 0, 0);
        _moneyBox.TextChanged += (_, _) =>
        {
            if (_syncingMoney) return;
            _moneyTouched = true;
            // Live into the open/fresh working save (no reformat while typing).
            PushMoneyToSession(quiet: true, reformat: false);
        };
        _moneyBox.Leave += (_, _) => ApplyMoneyFromBox(quiet: true);
        _moneyBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                ApplyMoneyFromBox(quiet: false);
            }
        };
        _tip.SetToolTip(_moneyBox, "Career cash — syncs live into the save you’re editing. Save zip… to keep it.");

        var statsBtn = UiTheme.Compact(UiTheme.Btn("Stats", OpenCareerStats));
        statsBtn.Margin = new Padding(6, 3, 0, 0);
        _tip.SetToolTip(statsBtn,
            "Career Stats — money, Game Completion, Win%, transmission, Jane unlocks.");

        host.Controls.Add(nameLbl);
        host.Controls.Add(_nameBox);
        host.Controls.Add(flagLbl);
        host.Controls.Add(_flagPick);
        host.Controls.Add(txLbl);
        host.Controls.Add(_txPick);
        host.Controls.Add(moneyLbl);
        host.Controls.Add(_moneyBox);
        host.Controls.Add(statsBtn);
        return host;
    }

    private void OpenCareerStats()
    {
        if (_session == null)
        {
            MessageBox.Show(this,
                "Open a save or Start a fresh profile first.",
                Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // Only push Money box → save if the user edited it; otherwise trust the save
        // (Start / Open already wrote the buffer — don't clobber with a stale box).
        if (_moneyTouched)
            PushMoneyToSession(quiet: true, reformat: true);
        else
            SyncMoneyBox(PlayerOptionsService.GetMoney(_session.Working));

        using var f = new CareerStatsForm(_session.Working, () =>
        {
            _session.Dirty = true;
            // Keep identity bar live while Stats stays open (modal but parent still visible).
            SyncMoneyBox(PlayerOptionsService.GetMoney(_session.Working));
            SyncTransmissionPick(PlayerOptionsService.GetTransmission(_session.Working));
            SetStatus(CareerStatsService.Summarize(_session.Working));
        }, _session.ProfileName);
        f.ShowDialog(this);

        SyncMoneyBox(PlayerOptionsService.GetMoney(_session.Working));
        SyncTransmissionPick(PlayerOptionsService.GetTransmission(_session.Working));
        SetStatus(CareerStatsService.Summarize(_session.Working));
    }

    private void SyncMoneyBox(int amount)
    {
        _syncingMoney = true;
        try
        {
            if (amount < 0) amount = 0;
            if (amount > NFSU2CustomCarSaves.Layout.MaxMoney) amount = NFSU2CustomCarSaves.Layout.MaxMoney;
            _moneyBox.Text = amount.ToString("N0");
            _moneyTouched = false;
        }
        finally { _syncingMoney = false; }
    }

    private static bool TryReadMoneyBoxText(string? text, out int amount)
    {
        amount = 0;
        string raw = (text ?? "").Trim().Replace(",", "").Replace(".", "").Replace(" ", "");
        if (raw.Length == 0) return false;
        if (!int.TryParse(raw, out amount)) return false;
        if (amount < 0) amount = 0;
        if (amount > NFSU2CustomCarSaves.Layout.MaxMoney) amount = NFSU2CustomCarSaves.Layout.MaxMoney;
        return true;
    }

    private bool TryReadMoneyBox(out int amount) => TryReadMoneyBoxText(_moneyBox.Text, out amount);

    /// <summary>
    /// Write Money box → open session. <paramref name="reformat"/> = N0 display (Leave/Enter only).
    /// </summary>
    private void PushMoneyToSession(bool quiet, bool reformat)
    {
        if (_syncingMoney) return;
        if (!TryReadMoneyBox(out int amount))
        {
            if (!quiet)
                MessageBox.Show(this, "Money must be a whole number.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            if (reformat && _session != null)
                SyncMoneyBox(PlayerOptionsService.GetMoney(_session.Working));
            return;
        }
        if (reformat) SyncMoneyBox(amount);
        if (_session == null) return;
        try
        {
            if (PlayerOptionsService.GetMoney(_session.Working) == amount) return;
            PlayerOptionsService.SetMoney(_session.Working, amount);
            _session.Dirty = true;
            SetStatus($"Money → {amount:N0}. Save zip… to keep it.");
        }
        catch (Exception ex)
        {
            if (!quiet)
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            SyncMoneyBox(PlayerOptionsService.GetMoney(_session.Working));
        }
    }

    private void ApplyMoneyFromBox(bool quiet) => PushMoneyToSession(quiet, reformat: true);

    private void SyncTransmissionPick(PlayerOptionsService.TransmissionMode mode)
    {
        _syncingTx = true;
        try
        {
            _txPick.SelectedIndex = mode == PlayerOptionsService.TransmissionMode.Manual ? 1 : 0;
        }
        finally { _syncingTx = false; }
    }

    private void ApplyTransmissionFromPick()
    {
        if (_syncingTx || _session == null) return;
        var mode = _txPick.SelectedIndex == 1
            ? PlayerOptionsService.TransmissionMode.Manual
            : PlayerOptionsService.TransmissionMode.Automatic;
        try
        {
            PlayerOptionsService.SetTransmission(_session.Working, mode);
            _session.Dirty = true;
            SetStatus($"Transmission → {mode} (Options → Player). Save zip… to keep it.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            SyncTransmissionPick(PlayerOptionsService.GetTransmission(_session.Working));
        }
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

    private void SyncFlagPick(string? code)
    {
        _syncingFlag = true;
        try
        {
            string want = (code ?? "").Trim().ToLowerInvariant();
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
            RoamFlagPrefs.Save(want);
        }
        finally { _syncingFlag = false; }
    }

    /// <summary>Default identity for app launch / Start: Name = Player, Flag = none.</summary>
    private void ApplyDefaultIdentity()
    {
        SyncNameBox("Player");
        SyncFlagPick("");
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
        var logo = GalaxyAssets.LogoFit(168, 92);
        int logoBoxH = 92;
        int logoTop = Math.Max(0, (r.Height - logoBoxH) / 2);
        GalaxyCanvas.DrawLogo(g, logo, new Rectangle(18, logoTop, 168, logoBoxH), pulse);

        float x = 200;
        var title = GalaxyAssets.TextFit(260, 34);
        float titleH = title?.Height ?? g.MeasureString("Custom Car Saves", _fHeroTitle).Height;
        float subH = _fHeroSub.GetHeight(g);
        float tagH = _fHeroTag.GetHeight(g);
        // wordmark + 3px + subtitle + 2px + credits — vertically centered in banner
        float stackH = titleH + 3 + subH + 2 + tagH;
        float ty = Math.Max(0, (r.Height - stackH) / 2f);
        float subY = ty + titleH + 3;

        if (title != null)
            g.DrawImageUnscaled(title, (int)x, (int)ty);
        else
            GalaxyCanvas.DrawGradientText(g, "Custom Car Saves", _fHeroTitle, x, ty, UiTheme.Primary, UiTheme.Lilac);

        using (var sub = new SolidBrush(UiTheme.TextMuted))
            g.DrawString("Your garage. Your rules.", _fHeroSub, sub, x, subY);
        using (var tag = new SolidBrush(Color.FromArgb(225, UiTheme.Lilac)))
            g.DrawString("by BARKIE  ·  Discord @extrabarkie", _fHeroTag, tag, x, subY + subH + 2);

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
        LiveryService.ClearClipboard();
        RefreshSlots();
        // Reload after startup ImportFromBundledSaves so stock cars show in the rack.
        _rack = SaveRack.Load();
        RefreshRightLibrary();
        UpdateClipStatus();
        UpdateDynoStatus();
        UpdateLiveryStatus();
        SetStatus("Fresh start — drop a zip / Open… · slots empty until you load a save.");
    }

    private void PlaceHeroSettingsButton()
    {
        if (_hero == null) return;

        const int size = 36;
        var icon = GalaxyAssets.SettingsFit(size, size);
        _settingsBtn = new PictureBox
        {
            Size = new Size(size, size),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Image = icon,
            Location = new Point(Math.Max(8, _hero.ClientSize.Width - size - 14), 14),
        };
        _tip.SetToolTip(_settingsBtn, "Settings — libraries, theme, startup.");
        _settingsBtn.Click += (_, _) => OpenSettings();
        _hero.Controls.Add(_settingsBtn);
        _settingsBtn.BringToFront();
        _hero.Resize += (_, _) =>
        {
            if (_settingsBtn == null || _hero == null) return;
            _settingsBtn.Location = new Point(Math.Max(8, _hero.ClientSize.Width - _settingsBtn.Width - 14), 14);
        };
    }

    private void ShowGuideCredits()
    {
        using var guide = new GalaxyGuideForm();
        guide.ShowDialog(this);
    }

    private void OpenSettings()
    {
        using var f = new SettingsForm(ReloadLibrariesFromDisk);
        if (f.ShowDialog(this) != DialogResult.OK) return;
        ApplyUiTheme();
        ReloadLibrariesFromDisk();
    }

    /// <summary>Reload save rack + right library lists from disk (after seed / settings).</summary>
    public void ReloadLibrariesFromDisk()
    {
        try { TunePresetService.InvalidateCache(); } catch { /* ignore */ }
        _rack = SaveRack.Load();
        RefreshRightLibrary(refreshLiveries: true);
        UpdateDynoStatus();
        UpdateLiveryStatus();
    }

    /// <summary>Refresh shell colors after dark/light toggle (minimal — form + status labels).</summary>
    public void ApplyUiTheme()
    {
        UiTheme.ApplyForm(this);
        if (_rootLayout != null) _rootLayout.BackColor = UiTheme.Bg;
        _status.ForeColor = UiTheme.Primary;
        _clipStatus.ForeColor = UiTheme.TextMuted;
        _dynoStatus.ForeColor = UiTheme.TextMuted;
        _liveryStatus.ForeColor = UiTheme.TextMuted;
        _slotHint.ForeColor = UiTheme.TextMuted;
        _rackHint.ForeColor = UiTheme.TextMuted;
        _slots.BackColor = UiTheme.Surface;
        _slots.ForeColor = UiTheme.Text;
        _rackList.BackColor = UiTheme.Surface;
        _rackList.ForeColor = UiTheme.Text;
        _nameBox.BackColor = UiTheme.Elevated;
        _nameBox.ForeColor = UiTheme.TextStrong;
        _moneyBox.BackColor = UiTheme.Elevated;
        _moneyBox.ForeColor = UiTheme.TextStrong;
        UiTheme.StyleCombo(_flagPick);
        UiTheme.StyleCombo(_txPick);
        Invalidate(true);
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

        // New save always starts as Player + no free-roam flag (edit after if needed).
        ApplyDefaultIdentity();
        string name = ReadNameBox();
        if (!BrowserSaveSession.IsValidProfileName(name))
            throw new InvalidOperationException($"Invalid name (1–{NFSU2CustomCarSaves.Layout.NameLength} characters).");

        _session?.Dispose();
        _session = BrowserSaveSession.CreateFreshDefault(name);
        SyncNameBox(_session.ProfileName);
        // Apply UI transmission choice onto the fresh post-Rachel career
        ApplyTransmissionFromPick();
        SyncTransmissionPick(PlayerOptionsService.GetTransmission(_session.Working));
        // Starter cash unless the user already typed Money before Start.
        if (_moneyTouched && TryReadMoneyBox(out int startMoney))
        {
            PlayerOptionsService.SetMoney(_session.Working, startMoney);
            CareerStatsService.EnsureCareerChecksums(_session.Working);
            _session.Dirty = true;
        }
        SyncMoneyBox(PlayerOptionsService.GetMoney(_session.Working));
        RefreshSlots();
        UpdateClipStatus();
        UpdateDynoStatus();
        int starterCash = global::NFSU2CustomCarSaves.Layout.FreshStartMoney;
        SetStatus($"{_session.ProfileName} — empty post-Rachel base · freeroam · no cars · ${starterCash:N0}. Save zip… when done.");
        MessageBox.Show(this,
            $"Fresh “0” save “{_session.ProfileName}” is ready.\n\n" +
            "• Rachel / intro cleared → freeroam (Jane-coherent career)\n" +
            "• Starter car removed — Career + Customize empty\n" +
            "• Slots 1–10 unlocked & empty\n" +
            $"• Starter money ${starterCash:N0} (not maxed)\n" +
            "• Editable stats cleared (Entered / Spent / stage Rep → 0)\n\n" +
            "Add cars / use Stats or 100% Save when you want unlocks.\n" +
            "Then Save zip… and Restore on nfsu2.xyz.",
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
        SyncTransmissionPick(PlayerOptionsService.GetTransmission(_session.Working));
        SyncMoneyBox(PlayerOptionsService.GetMoney(_session.Working));
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

    private void SetCarArea(bool menuMyCars)
    {
        _menuMyCars = menuMyCars;
        if (_btnAreaCareer is GlowButton gCareer)
            gCareer.Kind = menuMyCars ? GlowKind.Pink : GlowKind.PinkDark;
        if (_btnAreaMyCars is GlowButton gMy)
            gMy.Kind = menuMyCars ? GlowKind.PinkDark : GlowKind.Pink;

        _slotHint.Text = menuMyCars
            ? "Customize Car Select (1–20) · Steal → Career… or Copy / Paste"
            : "Career garage 1–10 · Steal → Customize… or Copy / Paste";
        SyncStealButton();
        UpdateDynoStatus();
        RefreshSlots();
        SyncRackActionButtons();
        if (_rackShowLiveries && _liverySameCarOnly?.Checked == true)
            RefreshRightLibrary();
        if (_session != null && menuMyCars)
        {
            int filled = _myCarInfos.Count(c => c.IsOccupied);
            SetStatus($"Customize Car Select — {filled} tuned car(s) in this save (main menu Customize)");
        }
    }

    private void SyncStealButton()
    {
        if (_btnStealTransfer != null)
        {
            if (_menuMyCars)
            {
                _btnStealTransfer.Text = "Steal → Career…";
                _tip.SetToolTip(_btnStealTransfer,
                    "Copy the selected Customize car into a Career garage slot.");
            }
            else
            {
                _btnStealTransfer.Text = "Steal → Customize…";
                _tip.SetToolTip(_btnStealTransfer,
                    "Copy the selected Career car into a Customize Car Select slot.");
            }
        }
        if (_btnUnlock != null)
            _btnUnlock.Enabled = !_menuMyCars;
    }

    private void StealTransfer()
    {
        if (_menuMyCars) StealCustomizeToCareer();
        else StealCareerToCustomize();
    }

    private void RackListToSelectedSlot()
    {
        if (_rackShowLiveries) ApplyLiveryFromLibrary();
        else RackToSlot();
    }

    private void SelectedSlotToRackList()
    {
        if (_rackShowLiveries) SaveLiveryFromSelected();
        else SlotToRack();
    }

    private void ClearRackOrLivery()
    {
        if (_rackShowLiveries) DeleteSelectedLiveryFromLibrary();
        else ClearRackSlot();
    }

    private void DeleteSelectedLiveryFromLibrary()
    {
        if (_rackList.SelectedItem is not LiveryPreset p)
            throw new InvalidOperationException("Select a livery in the list.");
        if (!p.CanDelete)
            throw new InvalidOperationException("That livery cannot be deleted (bundled / stock).");
        if (MessageBox.Show(this,
                $"Clear this entry from the livery library?\n\n{p.DisplayLabel}",
                "Continue?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        LiveryService.DeleteFromLibrary(p);
        RefreshLiveryList(refresh: true);
        SetOk($"Cleared livery: {p.DisplayLabel}");
    }

    private void RefreshSlots()
    {
        int keep = _slots.SelectedIndex;
        _slots.Items.Clear();
        byte[]? working = _session?.Working;

        if (_menuMyCars)
        {
            _myCarInfos = working != null ? MyCarsService.ReadAll(working) : new List<MyCarInfo>();
            _slotInfos = new List<SlotInfo>();
            foreach (var c in _myCarInfos)
            {
                string name = c.IsOccupied ? c.DisplayName : "(empty)";
                string state = c.IsOccupied ? "Filled" : "Open";
                string stats = "";
                if (working != null && c.IsOccupied)
                {
                    try
                    {
                        var t = CarTuneService.Read(working, c.Index, menuMyCars: true);
                        stats = $"  ·  {t.PerfInstalled}/{t.PerfTotal} perf  ·  {t.UniqueOn}/{t.UniqueTotal} uniq";
                    }
                    catch { /* skip */ }
                }
                _slots.Items.Add($"Customize {c.Index + 1,2}  {state,-6}  {name}{stats}");
            }
        }
        else
        {
            _myCarInfos = new List<MyCarInfo>();
            _slotInfos = _session?.Slots() ?? new List<SlotInfo>();
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

                // Slots 1–5 = career profile; 6–10 = GARAGE.DAT body (still Career garage, not Customize Car Select).
                string group = s.Index < NFSU2CustomCarSaves.Layout.CareerSlotCount ? "Career" : "Career+";
                _slots.Items.Add($"{group} {s.Index + 1}  {state,-6}  {name}{stats}");
            }
        }

        if (_slots.Items.Count == 0) return;
        _slots.SelectedIndex = keep >= 0 && keep < _slots.Items.Count ? keep : 0;
        _tipSlotsIndex = -2;
    }

    private void SetRackLibrary(bool liveries)
    {
        _rackShowLiveries = liveries;
        if (_btnRackCars is GlowButton gCars)
            gCars.Kind = liveries ? GlowKind.Pink : GlowKind.PinkDark;
        if (_btnRackLiveries is GlowButton gLiv)
            gLiv.Kind = liveries ? GlowKind.PinkDark : GlowKind.Pink;

        SyncRackActionButtons();
        RefreshRightLibrary(refreshLiveries: liveries);
    }

    /// <summary>
    /// Slot ← / → Rack / Clear follow the open right tab (Cars rack vs Liveries)
    /// and always target the open left list (Career garage or Customize Car Select).
    /// </summary>
    private void SyncRackActionButtons()
    {
        string area = _menuMyCars ? "Customize Car Select" : "Career garage";
        bool liv = _rackShowLiveries;

        if (_btnRackReload != null) _btnRackReload.Visible = liv;
        if (_liverySameCarOnly != null) _liverySameCarOnly.Visible = liv;

        if (_btnRackClear != null)
        {
            _btnRackClear.Text = "Clear";
            _tip.SetToolTip(_btnRackClear, liv
                ? "Clear / remove the selected livery from the library (bundled may refuse)."
                : "Remove the selected car from the Save rack.");
        }
        if (_btnRackToSlot != null)
        {
            _btnRackToSlot.Text = "Slot ←";
            _tip.SetToolTip(_btnRackToSlot, liv
                ? $"Selected livery → selected car in {area}. Double-click also works."
                : $"Selected Cars rack entry → selected car in {area}.");
        }
        if (_btnSlotToRack != null)
        {
            _btnSlotToRack.Text = liv ? "→ Library" : "→ Rack";
            _tip.SetToolTip(_btnSlotToRack, liv
                ? $"Selected car in {area} → save graphics into the livery library."
                : $"Selected car in {area} → add to Cars rack (overwrite optional if selected).");
        }

        _rackHint.Text = liv
            ? $"Liveries · Slot ← applies to {area} · → Library saves from that car"
            : $"Cars rack · {_rack.Count} car(s) · Slot ← / → Rack use {area}";
    }

    private void RefreshRightLibrary(bool refreshLiveries = false)
    {
        if (_rackShowLiveries) RefreshLiveryList(refreshLiveries);
        else RefreshRack();
    }

    private void RefreshRack()
    {
        int keep = _rackList.SelectedIndex;
        _rackList.Items.Clear();
        for (int i = 0; i < _rack.Count; i++)
            _rackList.Items.Add(_rack.Label(i));

        string area = _menuMyCars ? "Customize Car Select" : "Career garage";
        if (_rackList.Items.Count == 0)
        {
            _rackHint.Text = $"Cars rack empty — → Rack from {area} to store a car";
            _tipRackIndex = -2;
            return;
        }

        _rackList.SelectedIndex = keep >= 0 && keep < _rackList.Items.Count ? keep : 0;
        _rackHint.Text = $"Cars rack · {_rack.Count} car(s) · Slot ← / → Rack use {area}";
        _tipRackIndex = -2;
    }

    private void RefreshLiveryList(bool refresh = false)
    {
        int keep = _rackList.SelectedIndex;
        string? keepId = _rackList.SelectedItem is LiveryPreset sel ? sel.Id : null;
        _rackList.Items.Clear();

        string wantHash = "";
        bool sameOnly = _liverySameCarOnly?.Checked != false;
        if (sameOnly && _session != null)
        {
            try
            {
                int slot = SelectedSlot();
                if (_menuMyCars)
                {
                    var mc = MyCarsService.Read(_session.Working, slot);
                    if (mc.IsOccupied) wantHash = mc.CarHash.ToString("X8");
                }
                else
                {
                    var info = GarageDatService.ReadSlot(_session.Working, slot);
                    if (info.IsOccupied) wantHash = info.CarHash.ToString("X8");
                }
            }
            catch { wantHash = ""; }
        }

        foreach (var p in LiveryService.LoadAll(refresh))
        {
            if (sameOnly)
            {
                if (string.IsNullOrEmpty(wantHash)) continue;
                if (string.IsNullOrWhiteSpace(p.CarHash)) continue;
                if (!p.CarHash.Equals(wantHash, StringComparison.OrdinalIgnoreCase)) continue;
            }
            _rackList.Items.Add(p);
        }

        if (_rackList.Items.Count == 0)
        {
            string area = _menuMyCars ? "Customize Car Select" : "Career garage";
            _rackHint.Text = sameOnly
                ? (string.IsNullOrEmpty(wantHash)
                    ? $"Select a filled car in {area} — Same car filter is on"
                    : $"No liveries for this car yet — → Library from {area}, or open a save with this model")
                : "No liveries in the library yet";
            _tipRackIndex = -2;
            return;
        }

        int pick = 0;
        if (!string.IsNullOrEmpty(keepId))
        {
            for (int i = 0; i < _rackList.Items.Count; i++)
            {
                if (_rackList.Items[i] is LiveryPreset p && p.Id == keepId) { pick = i; break; }
            }
        }
        else if (keep >= 0)
            pick = Math.Clamp(keep, 0, _rackList.Items.Count - 1);

        _rackList.SelectedIndex = pick;
        string areaOk = _menuMyCars ? "Customize Car Select" : "Career garage";
        _rackHint.Text = $"{_rackList.Items.Count} livery(s) · Slot ← → {areaOk}";
        _tipRackIndex = -2;
    }

    private void ApplyLiveryFromLibrary()
    {
        if (_session == null) throw new InvalidOperationException("Open a save first.");
        if (_rackList.SelectedItem is not LiveryPreset p)
            throw new InvalidOperationException("Select a livery in the list.");
        int dst = SelectedSlot();
        EnsureSlotDynoReadable(dst);
        string what = LiveryService.Apply(_session.Working, dst, p, LiveryApplyMode.Replace, _menuMyCars);
        _session.Dirty = true;
        RefreshSlots();
        UpdateLiveryStatus();
        SetOk($"Livery → {AreaLabel(dst)}: {p.DisplayLabel} ({what})");
    }

    private void SaveLiveryFromSelected()
    {
        if (_session == null) throw new InvalidOperationException("Open a save first.");
        int src = SelectedSlot();
        EnsureSlotDynoReadable(src);
        var p = LiveryService.SaveToLibrary(_session.Working, src, _session.ProfileName, _menuMyCars);
        RefreshLiveryList(refresh: true);
        SetOk($"Saved livery: {p.DisplayLabel}");
    }

    private void HoverTip(ListBox lb, MouseEventArgs e, bool isRack)
    {
        int idx = lb.IndexFromPoint(e.Location);
        if (isRack)
        {
            if (idx == _tipRackIndex) return;
            _tipRackIndex = idx;
            if (_rackShowLiveries)
            {
                if (idx < 0 || idx >= lb.Items.Count || lb.Items[idx] is not LiveryPreset liv)
                { _tip.Hide(lb); return; }
                _tip.SetToolTip(lb, liv.HoverTip);
                return;
            }
            if (idx < 0 || idx >= _rack.Count) { _tip.Hide(lb); return; }
            _tip.SetToolTip(lb, _rack.Tooltip(idx));
        }
        else
        {
            if (idx == _tipSlotsIndex) return;
            _tipSlotsIndex = idx;
            if (_menuMyCars)
            {
                if (idx < 0 || idx >= _myCarInfos.Count) { _tip.Hide(lb); return; }
                var c = _myCarInfos[idx];
                string state = c.IsOccupied ? "Filled" : "Empty — Add car / Paste / .carslot";
                _tip.SetToolTip(lb, CarNames.Tooltip(c.CarHash,
                    $"Customize Car Select (main menu)\nSlot {c.Index + 1} of 20\nState: {state}\nCopy → Career garage → Paste"));
            }
            else
            {
                if (idx < 0 || idx >= _slotInfos.Count) { _tip.Hide(lb); return; }
                var s = _slotInfos[idx];
                string state = s.IsLocked
                    ? "Locked — Unlock first"
                    : s.IsOccupied ? "Filled" : "Empty — Add car / Paste / .carslot";
                string group = s.Index < NFSU2CustomCarSaves.Layout.CareerSlotCount
                    ? "Career (profile)"
                    : "Career+ (GARAGE.DAT slots 6–10)";
                _tip.SetToolTip(lb, CarNames.Tooltip(s.CarHash, $"{group}\nSlot {s.Index + 1} of 10\nState: {state}"));
            }
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
            + "  ·  Career + Customize Car Select";
    }

    private void UpdateLiveryStatus()
    {
        _liveryStatus.Text = LiveryService.SummarizeClipboard(LiveryService.ClipboardPkg)
            + "  ·  Career + Customize Car Select";
    }

    private void CopyDyno()
    {
        int src = SelectedSlot();
        EnsureSlotDynoReadable(src);
        var pkg = DynoService.Export(_session!.Working, src, menuMyCars: _menuMyCars);
        DynoService.CopyToClipboard(pkg);
        UpdateDynoStatus();
        SetStatus($"Dyno copied: {pkg.SourceCar} ({AreaLabel(src)}) — paste on Career or Customize");
    }

    private void PasteDyno()
    {
        int dst = SelectedSlot();
        EnsureSlotDynoReadable(dst);
        var pkg = DynoService.PasteFromClipboard()
                  ?? throw new InvalidOperationException("No dyno copied — use Copy dyno first.");
        DynoService.Apply(_session!.Working, dst, pkg, _menuMyCars);
        _session.Dirty = true;
        RefreshSlots();
        UpdateDynoStatus();
        SetStatus($"Dyno pasted → {AreaLabel(dst)} (source: {pkg.SourceCar})");
    }

    private void CopyLivery()
    {
        int src = SelectedSlot();
        EnsureSlotDynoReadable(src);
        var pkg = LiveryService.ExportToClipboard(
            _session!.Working, src, _session.ProfileName, _menuMyCars);
        UpdateLiveryStatus();
        SetStatus($"Livery copied: {pkg.CarName} ({AreaLabel(src)}) — paste on Career or Customize");
    }

    private void PasteLivery()
    {
        int dst = SelectedSlot();
        EnsureSlotDynoReadable(dst);
        var pkg = LiveryService.PasteFromClipboard()
                  ?? throw new InvalidOperationException("No livery copied — use Copy livery first.");
        string what = LiveryService.Apply(_session!.Working, dst, pkg, LiveryApplyMode.Replace, _menuMyCars);
        _session.Dirty = true;
        RefreshSlots();
        UpdateLiveryStatus();
        SetStatus($"Livery pasted → {AreaLabel(dst)} ({what})");
    }

    /// <summary>One-shot: Customize Car Select → Career garage (full car: dyno + livery + tune).</summary>
    private void StealCustomizeToCareer()
    {
        if (_session == null) throw new InvalidOperationException("Open a save first.");
        if (!_menuMyCars)
            throw new InvalidOperationException(
                "Switch to Customize Car Select first, pick a tuned car, then Steal → Career…");

        int src = SelectedSlot();
        var info = MyCarsService.Read(_session.Working, src);
        if (!info.IsOccupied)
            throw new InvalidOperationException("That Customize slot is empty.");

        int prefer = PreferEmptyCareerSlot(_session.Working);
        string? raw = Prompt($"Paste {info.DisplayName} into Career slot (1–10):", prefer.ToString());
        if (raw == null) return;
        if (!int.TryParse(raw.Trim(), out int dstHuman) || dstHuman < 1 || dstHuman > 10)
            throw new InvalidOperationException("Invalid Career slot (use 1–10).");
        int dst = dstHuman - 1;

        var existing = GarageDatService.ReadSlot(_session.Working, dst);
        if (existing.IsOccupied)
        {
            if (MessageBox.Show(this,
                    $"Overwrite Career {dstHuman} ({existing.DisplayName}) with\n{info.DisplayName} from Customize?\n\n" +
                    "Full car: body, dyno, livery, performance.",
                    "Steal → Career", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
        }

        var pkg = _session.ExportMyCarPackage(src);
        SlotClipboard.Copy(pkg);
        _session.ImportPackage(pkg, dst);
        SetCarArea(menuMyCars: false);
        _slots.SelectedIndex = Math.Clamp(dst, 0, Math.Max(0, _slots.Items.Count - 1));
        UpdateClipStatus();
        SetOk($"Stolen Customize {src + 1} → Career {dstHuman}: {pkg.CarName} (dyno + livery + tune)");
    }

    /// <summary>One-shot: Career garage → Customize Car Select (full car: dyno + livery + tune).</summary>
    private void StealCareerToCustomize()
    {
        if (_session == null) throw new InvalidOperationException("Open a save first.");
        if (_menuMyCars)
            throw new InvalidOperationException(
                "Switch to Career garage first, pick a car, then Steal → Customize…");
        if (!MyCarsService.BufferHasMyCars(_session.Working))
            throw new InvalidOperationException("This save has no Customize Car Select (My Cars) block.");

        int src = SelectedSlot();
        var info = GarageDatService.ReadSlot(_session.Working, src);
        if (!info.IsOccupied)
            throw new InvalidOperationException("That Career slot is empty.");

        int prefer = PreferEmptyCustomizeSlot(_session.Working);
        string? raw = Prompt($"Paste {info.DisplayName} into Customize slot (1–20):", prefer.ToString());
        if (raw == null) return;
        if (!int.TryParse(raw.Trim(), out int dstHuman) || dstHuman < 1 || dstHuman > MyCarsLayout.Count)
            throw new InvalidOperationException($"Invalid Customize slot (use 1–{MyCarsLayout.Count}).");
        int dst = dstHuman - 1;

        var existing = MyCarsService.Read(_session.Working, dst);
        if (existing.IsOccupied)
        {
            if (MessageBox.Show(this,
                    $"Overwrite Customize {dstHuman} ({existing.DisplayName}) with\n{info.DisplayName} from Career?\n\n" +
                    "Full car: body, dyno, livery, performance.",
                    "Steal → Customize", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
        }

        var pkg = _session.ExportPackage(src, _session.ProfileName);
        SlotClipboard.Copy(pkg);
        _session.ImportMyCarPackage(pkg, dst);
        SetCarArea(menuMyCars: true);
        _slots.SelectedIndex = Math.Clamp(dst, 0, Math.Max(0, _slots.Items.Count - 1));
        UpdateClipStatus();
        SetOk($"Stolen Career {src + 1} → Customize {dstHuman}: {pkg.CarName} (dyno + livery + tune)");
    }

    private static int PreferEmptyCareerSlot(byte[] working)
    {
        int count = working.Length >= NFSU2CustomCarSaves.Layout.ExtraSlotRecordBase
                     + NFSU2CustomCarSaves.Layout.CareerSlotCount * NFSU2CustomCarSaves.Layout.SlotStride
            ? NFSU2CustomCarSaves.Layout.MaxSlotCount
            : NFSU2CustomCarSaves.Layout.CareerSlotCount;
        for (int i = 0; i < count; i++)
        {
            if (!GarageDatService.ReadSlot(working, i).IsOccupied)
                return i + 1;
        }
        return 1;
    }

    private static int PreferEmptyCustomizeSlot(byte[] working)
    {
        foreach (var c in MyCarsService.ReadAll(working))
        {
            if (!c.IsOccupied)
                return c.Index + 1;
        }
        return 1;
    }

    /// <summary>Career+ slots 6–10 live in GARAGE.DAT — create/attach if the buffer is career-only.</summary>
    private void EnsureSlotDynoReadable(int slot)
    {
        if (_menuMyCars)
        {
            if (!MyCarsService.BufferHasMyCars(_session!.Working))
                throw new InvalidOperationException("This save has no My Cars (menu Customize) block.");
            return;
        }
        if (slot < NFSU2CustomCarSaves.Layout.CareerSlotCount) return;
        int need = NFSU2CustomCarSaves.Layout.ExtraSlotRecordBase
                   + NFSU2CustomCarSaves.Layout.CareerSlotCount * NFSU2CustomCarSaves.Layout.SlotStride;
        if (_session!.Working.Length >= need) return;
        _session.EnsureMyCarsGarage();
        RefreshSlots();
        if (_session.Working.Length < need)
            throw new InvalidOperationException(
                "Career slots 6–10 need GARAGE.DAT. Open a browser zip that includes it.");
    }

    private void ApplyPresetQuick()
    {
        int slot = SelectedSlot();
        string carName;
        uint carHash;
        if (_menuMyCars)
        {
            var mc = MyCarsService.Read(_session!.Working, slot);
            carName = mc.DisplayName;
            carHash = mc.CarHash;
        }
        else
        {
            var info = GarageDatService.ReadSlot(_session!.Working, slot);
            carName = info.DisplayName;
            carHash = info.CarHash;
        }
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
                $"No dyno presets for {carName} yet. Open a save with that model, or save this car from Tune → Dyno.");

        using var f = new Form
        {
            Text = carHash == 0 ? "Apply dyno preset" : $"Apply dyno preset — {carName}",
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
                ? "Dyno only by default. Tick “performance / unique” only if you want parts replaced. Vinyl/livery stays."
                : $"Only presets for {carName}. Dyno only by default — parts stay unless you tick the box. Vinyl/livery stays.",
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
            Text = "Also replace performance / unique parts (off = dyno only)",
            Checked = false,
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
            if (!needs) upgrades.Checked = false;
            copyBody.Enabled = needs && upgrades.Checked;
            if (!copyBody.Enabled) copyBody.Checked = false;
        }
        upgrades.CheckedChanged += (_, _) => SyncPartsChecks();
        list.SelectedIndexChanged += (_, _) => { ShowNote(); SyncPartsChecks(); };
        ShowNote();
        SyncPartsChecks();

        void DoApply()
        {
            if (list.SelectedItem is not TunePreset p) return;
            string what = TunePresetService.ApplyDyno(
                _session!.Working, slot, p, upgrades.Checked, copyBody.Checked, _menuMyCars);
            _session.Dirty = true;
            f.DialogResult = DialogResult.OK;
            f.Close();
            RefreshSlots();
            SetStatus($"Dyno → {AreaLabel(slot)}: {p.DisplayLabel} ({what})");
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
        if (_menuMyCars)
        {
            if (_slots.SelectedIndex >= _myCarInfos.Count)
                throw new InvalidOperationException("Select a My Cars slot.");
            return _myCarInfos[_slots.SelectedIndex].Index;
        }
        if (_slots.SelectedIndex >= _slotInfos.Count)
            throw new InvalidOperationException("Select a Career slot.");
        return _slotInfos[_slots.SelectedIndex].Index;
    }

    private string AreaLabel(int slot) =>
        _menuMyCars ? $"Customize {slot + 1}"
        : slot < NFSU2CustomCarSaves.Layout.CareerSlotCount ? $"Career {slot + 1}" : $"Career+ {slot + 1}";

    private int SelectedRack()
    {
        if (_rackShowLiveries)
            throw new InvalidOperationException("Switch to the Cars tab for the Save rack.");
        if (_rack.Count == 0)
            throw new InvalidOperationException("Save rack is empty — use → Rack first.");
        if (_rackList.SelectedIndex < 0 || _rackList.SelectedIndex >= _rack.Count)
            throw new InvalidOperationException("Select a car in the Save rack.");
        return _rackList.SelectedIndex;
    }

    private void CopySlot()
    {
        int src = SelectedSlot();
        var pkg = _menuMyCars
            ? _session!.ExportMyCarPackage(src)
            : _session!.ExportPackage(src, _session.ProfileName);
        SlotClipboard.Copy(pkg);
        UpdateClipStatus();
        SetOk($"Copied: {AreaLabel(src)} — {pkg.CarName}");
    }

    private void PasteSlot()
    {
        int dst = SelectedSlot();
        var pkg = SlotClipboard.Paste()
                  ?? throw new InvalidOperationException("Clipboard is empty — copy a slot first.");
        if (_menuMyCars) _session!.ImportMyCarPackage(pkg, dst);
        else _session!.ImportPackage(pkg, dst);
        RefreshSlots();
        UpdateClipStatus();
        SetOk($"Pasted → {AreaLabel(dst)}: {pkg.CarName}");
    }

    private void SlotToRack()
    {
        int src = SelectedSlot();
        var pkg = _menuMyCars
            ? _session!.ExportMyCarPackage(src)
            : _session!.ExportPackage(src, _session.ProfileName);

        int sel = _rackList.SelectedIndex;
        bool canOverwrite = sel >= 0 && sel < _rack.Count && _rack.Get(sel) != null;
        if (canOverwrite)
        {
            var ask = MessageBox.Show(this,
                $"Overwrite rack R{sel + 1:000}, or add as a new entry?\n\n" +
                "Yes = overwrite selected\nNo = add as new\nCancel = abort",
                Text,
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);
            if (ask == DialogResult.Cancel) return;
            if (ask == DialogResult.Yes)
            {
                _rack.Set(sel, pkg);
                RefreshRack();
                _rackList.SelectedIndex = sel < _rackList.Items.Count ? sel : _rack.Count - 1;
                SetOk($"{AreaLabel(src)} → rack R{sel + 1:000} ({pkg.CarName})");
                return;
            }
        }

        _rack.Add(pkg);
        RefreshRack();
        if (_rackList.Items.Count > 0)
            _rackList.SelectedIndex = _rack.Count - 1;
        SetOk($"{AreaLabel(src)} → rack R{_rack.Count:000} ({pkg.CarName})");
    }

    private void RackToSlot()
    {
        int rack = SelectedRack();
        int dst = SelectedSlot();
        var pkg = _rack.Get(rack)
                  ?? throw new InvalidOperationException($"Rack R{rack + 1:000} is empty.");
        if (_menuMyCars) _session!.ImportMyCarPackage(pkg, dst);
        else _session!.ImportPackage(pkg, dst);
        RefreshSlots();
        SetOk($"Rack R{rack + 1:000} → {AreaLabel(dst)}: {pkg.CarName}");
    }

    private void ClearRackSlot()
    {
        int rack = SelectedRack();
        if (MessageBox.Show(this,
                $"Clear this entry from the Save rack?\n\nR{rack + 1:000}",
                "Continue?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        _rack.RemoveAt(rack);
        RefreshRack();
        _status.Text = $"Rack R{rack + 1:000} cleared.";
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
        if (_menuMyCars) _session!.ImportMyCarPackage(pkg, dst);
        else _session!.ImportPackage(pkg, dst);
        RefreshSlots();
        SetOk($"Imported → {AreaLabel(dst)}: {pkg.CarName}");
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

        string? raw = Prompt(
            _menuMyCars
                ? "Source Career slot (1–10) to copy into My Cars:"
                : "Source slot number (1–10):",
            "1");
        if (raw == null) return;
        if (!int.TryParse(raw.Trim(), out int srcHuman) || srcHuman < 1 || srcHuman > 10)
            throw new InvalidOperationException("Invalid slot number.");

        if (_menuMyCars)
        {
            // Steal Career → My Cars: read source TunedCar, write menu block.
            byte[] other = File.ReadAllBytes(dlg.FileName);
            string? otherGarage = Path.Combine(Path.GetDirectoryName(dlg.FileName)!, "GARAGE.DAT");
            byte[] srcBuf = other;
            if (File.Exists(otherGarage))
            {
                var g = File.ReadAllBytes(otherGarage);
                var st = GarageDatService.Check(g, other);
                if (st.State == GarageState.Loaded)
                    srcBuf = GarageDatService.Merge(other, g, GarageState.Loaded);
            }
            GarageDatService.ReadRecord(srcBuf, srcHuman - 1, out var tuned, out _);
            uint hash = BitConverter.ToUInt32(tuned, NFSU2CustomCarSaves.Layout.CarHashInTuned);
            if (tuned[NFSU2CustomCarSaves.Layout.BoughtInTuned] != 1 || hash == 0)
                throw new InvalidOperationException("Source slot is empty.");
            MyCarsService.WriteTuned(_session!.Working, dst, tuned);
            _session.Dirty = true;
        }
        else
        {
            _session!.CopyFromCareerFile(dlg.FileName, srcHuman - 1, dst);
        }
        RefreshSlots();
        SetOk($"Copied from Career {srcHuman} → {AreaLabel(dst)}");
    }

    private void ExportCarslot()
    {
        int src = SelectedSlot();
        var pkg = _menuMyCars
            ? _session!.ExportMyCarPackage(src)
            : _session!.ExportPackage(src, _session.ProfileName);
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
        if (_menuMyCars)
        {
            SetStatus("Unlock is for Career slots — My Cars has no lock flag.");
            return;
        }
        int i = SelectedSlot();
        _session!.Unlock(i);
        RefreshSlots();
        SetStatus($"Slot {i + 1} unlocked.");
    }

    private void AddOrReplaceCar()
    {
        if (_session == null) throw new InvalidOperationException("Open a save first.");
        int slot = SelectedSlot();
        string display;
        bool replace;
        if (_menuMyCars)
        {
            var mc = MyCarsService.Read(_session.Working, slot);
            display = mc.DisplayName;
            replace = mc.IsOccupied;
        }
        else
        {
            var info = GarageDatService.ReadSlot(_session.Working, slot);
            display = info.DisplayName;
            replace = info.IsOccupied;
        }
        using var dlg = new AddCarForm(replace, display);
        if (dlg.ShowDialog(this) != DialogResult.OK || dlg.Selected == null) return;

        if (replace)
        {
            if (MessageBox.Show(this,
                    $"Replace {display} in {AreaLabel(slot)} with:\n{dlg.Selected.Name}?\n\n" +
                    "Stock body is written. Dyno region resets to midpoint — open Tune for dyno/livery after.",
                    "Replace car", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
        }

        if (_menuMyCars) _session.PlaceMyCarDonor(slot, dlg.Selected);
        else _session.PlaceDonorCar(slot, dlg.Selected);
        RefreshSlots();
        SetStatus($"{(replace ? "Replaced" : "Added")} → {AreaLabel(slot)}: {dlg.Selected.Name}");
    }

    private void OpenSelectedTune()
    {
        int i = SelectedSlot();
        _session!.OpenTuneEditor(i, this, () =>
        {
            RefreshSlots();
            SetStatus($"{AreaLabel(i)} updated — don't forget Save zip…");
        }, _menuMyCars);
        RefreshSlots();
    }

    private void MaxOutSave()
    {
        if (_session == null) throw new InvalidOperationException("Open a save first.");
        if (MessageBox.Show(this,
                "100% Save (Jane free-play, browser-safe)?\n\n" +
                "• Career free-play like Jane (Game Completion 100%; no Rachel / intro restart)\n" +
                "• Win% 100% — ForcePerfectWinRecord after Jane copy (7148 losses→wins + 7B00 field_5)\n" +
                "• Car shop / lot fully unlocked\n" +
                "• Specialty / hidden shops on map + Extreme shop catalogs\n" +
                "• Magazines / DVD / sponsors / outrun path from Jane (7148 + 887C)\n" +
                "• All garage slots unlocked\n" +
                $"• Money → {NFSU2CustomCarSaves.Layout.MaxMoney:N0}\n" +
                "• Cars already in slots are LEFT UNCHANGED (no retune)\n\n" +
                "After this: Save zip… then on nfsu2.xyz use Restore.\n" +
                "Confirm dialog must say “Restore this backup?” (not “Add profile”).\n" +
                "If it says GARAGE.DAT is left out → slots 6–10 will not load.",
                "100% Save",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        string report = _session.MaxOut();
        SyncMoneyBox(PlayerOptionsService.GetMoney(_session.Working));
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
            "Wipe temp / session data?\n\n" +
            "Will clear:\n" +
            "• Open car-save session & temp work folders\n" +
            "• LocalAppData / TEMP app folders\n" +
            "• Clipboards (carslot / dyno / livery)\n" +
            "• Optional: Desktop NFSU2CustomCarSaves_Backup_* (asked next)\n\n" +
            "Will NOT clear:\n" +
            "• Save rack, dyno library, livery library\n\n" +
            "Game saves / NFSU2 BROWSER SAVE stay intact.\n\n" +
            "Yes = continue  ·  No = cancel",
            "Wipe data",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (ask != DialogResult.Yes) return;

        if (!Uninstaller.ConfirmTypeDelete(this, "Confirm wipe",
                "This clears temp / session / clipboards only.\n" +
                "Racks and libraries are kept.\n\n" +
                "Type delete below, then press Delete."))
            return;

        bool desk = MessageBox.Show(this,
            "Also delete Desktop backups?\n(folders NFSU2CustomCarSaves_Backup_*)",
            "Desktop backups",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question) == DialogResult.Yes;

        // Close open session first so temp folders can be deleted
        _session?.Dispose();
        _session = null;
        RefreshSlots();

        var report = Uninstaller.WipeAppTemp(includeDesktopBackups: desk, clearClipboard: true);
        UpdateClipStatus();
        UpdateDynoStatus();
        UpdateLiveryStatus();

        MessageBox.Show(this, Uninstaller.Summarize(report, "Temp wipe complete."), "Wipe complete",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
        _status.Text = "Temp / session cleared. Racks kept. Open a save again to continue.";
        _status.ForeColor = UiTheme.Amber;
    }

    private void DeleteRacks()
    {
        var ask = MessageBox.Show(this,
            "Delete all racks and libraries?\n\n" +
            "Will clear:\n" +
            "• Save rack (stored cars)\n" +
            "• Dyno library\n" +
            "• Livery library\n" +
            "• Clipboards\n\n" +
            "Afterward stock is restored only if ‘Load default libraries’ is on in Settings.\n" +
            "Game saves / NFSU2 BROWSER SAVE stay intact.\n\n" +
            "Yes = continue  ·  No = cancel",
            "Delete racks",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (ask != DialogResult.Yes) return;

        if (!Uninstaller.ConfirmTypeDelete(this, "Confirm delete racks",
                "This empties your save rack, dyno library, and livery library,\n" +
                "then restores stock entries.\n\n" +
                "Type delete below, then press Delete."))
            return;

        var report = Uninstaller.WipeLibraries(clearClipboard: true);

        bool reseed = AppSettings.Load().LoadDefaultLibraries;
        try
        {
            LiveryService.ResetStockImportFlag();
            TunePresetService.InvalidateCache();
            if (reseed)
                AppPaths.SeedDefaultLibraries();
        }
        catch { /* stock refill best-effort */ }

        _rack = SaveRack.Load();
        RefreshRightLibrary(refreshLiveries: true);
        UpdateClipStatus();
        UpdateDynoStatus();
        UpdateLiveryStatus();

        string done = reseed
            ? "Racks cleared. Stock lists restored."
            : "Racks cleared. Stock not restored (disabled in Settings).";
        MessageBox.Show(this, Uninstaller.Summarize(report, done),
            "Delete racks complete",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
        _status.Text = reseed
            ? "Racks / libraries cleared and stock restored."
            : "Racks / libraries cleared (no stock re-seed).";
        _status.ForeColor = UiTheme.Amber;
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

    private void AdaptHeroHeight()
    {
        if (_rootLayout == null || _rootLayout.RowStyles.Count < 1 || IsDisposed) return;
        if (WindowState == FormWindowState.Minimized) return;

        bool compact = ClientSize.Height <= 700;
        float heroH = compact ? 90f : 118f;
        var style = _rootLayout.RowStyles[0];
        if (style.SizeType != SizeType.Absolute || Math.Abs(style.Height - heroH) > 0.5f)
            _rootLayout.RowStyles[0] = new RowStyle(SizeType.Absolute, heroH);
    }

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
