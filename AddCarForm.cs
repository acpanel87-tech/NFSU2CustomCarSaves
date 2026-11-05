namespace NFSU2CustomCarSaves;

/// <summary>Add / Replace car picker: searchable list of all in-game donors (galaxy shell).</summary>
public sealed class AddCarForm : Form
{
    private readonly ListBox _list = new();
    private readonly TextBox _search = new();
    private readonly Label _hint = new();
    private readonly List<CarDonor> _all;
    private readonly bool _replace;

    public CarDonor? Selected { get; private set; }

    public AddCarForm(bool replace, string? currentCarName = null)
    {
        _replace = replace;
        _all = CarDonorService.LoadAll().ToList();

        UiTheme.ApplyForm(this);
        Text = replace ? "Replace car" : "Add car";
        Width = 520;
        Height = 640;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;

        var host = new StarfieldControl(45, 91) { Animate = false, Dock = DockStyle.Fill };
        Controls.Add(host);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(18),
            BackColor = Color.Transparent,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        host.Controls.Add(root);

        root.Controls.Add(UiTheme.SectionHead(replace ? "Replace car with…" : "Add car…"), 0, 0);
        root.Controls.Add(UiTheme.Muted(
            replace
                ? $"Replaces {(string.IsNullOrWhiteSpace(currentCarName) ? "this slot" : currentCarName)}. Stock body — then Tune for dyno/livery."
                : "Places a stock in-game car in the selected slot. Then open Tune for dyno + livery.",
            460), 0, 1);

        // Search field with a cyan glow edge
        var searchShell = new Panel
        {
            Height = 38,
            Dock = DockStyle.Top,
            Padding = new Padding(1),
            BackColor = Color.FromArgb(0x5F, 0x9F, 0xE0),
            Margin = new Padding(0, 4, 0, 10),
        };
        var searchInner = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Elevated, Padding = new Padding(10, 7, 10, 6) };
        _search.BorderStyle = BorderStyle.None;
        _search.Dock = DockStyle.Fill;
        _search.BackColor = UiTheme.Elevated;
        _search.ForeColor = UiTheme.TextStrong;
        _search.Font = UiTheme.UiFont(10f);
        _search.PlaceholderText = replace ? "Replace car with…" : "Search cars…";
        _search.TextChanged += (_, _) => ReloadList();
        searchInner.Controls.Add(_search);
        searchShell.Controls.Add(searchInner);
        root.Controls.Add(searchShell, 0, 2);

        var listShell = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(1),
            BackColor = UiTheme.Border,
            Margin = new Padding(0, 0, 0, 10),
        };
        var listInner = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Surface, Padding = new Padding(6) };
        var head = new Label
        {
            Text = "Cars",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold | FontStyle.Italic),
            ForeColor = UiTheme.Lilac,
            Dock = DockStyle.Top,
            Padding = new Padding(4, 2, 4, 6),
        };
        UiTheme.StyleList(_list);
        _list.Dock = DockStyle.Fill;
        _list.IntegralHeight = false;
        _list.ItemHeight = 26;
        _list.Font = UiTheme.UiFont(10f);
        _list.DoubleClick += (_, _) => AcceptSelected();
        listInner.Controls.Add(_list);
        listInner.Controls.Add(head);
        listShell.Controls.Add(listInner);
        root.Controls.Add(listShell, 0, 3);

        var bottom = new TableLayoutPanel
        {
            ColumnCount = 2,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 0),
            BackColor = Color.Transparent,
        };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _hint.AutoSize = true;
        _hint.ForeColor = UiTheme.TextMuted;
        _hint.Font = UiTheme.UiFont(8.5f);
        _hint.Margin = new Padding(0, 10, 0, 0);
        var btns = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        btns.Controls.Add(UiTheme.Btn(replace ? "Replace" : "Add car", AcceptSelected, primary: true));
        btns.Controls.Add(UiTheme.Btn("Cancel", () => { DialogResult = DialogResult.Cancel; Close(); }));
        bottom.Controls.Add(_hint, 0, 0);
        bottom.Controls.Add(btns, 1, 0);
        root.Controls.Add(bottom, 0, 4);

        ReloadList();
        ActiveControl = _search;
    }

    private void ReloadList()
    {
        string q = (_search.Text ?? "").Trim();
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var d in _all)
        {
            if (q.Length > 0 && d.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            _list.Items.Add(d);
        }
        _list.EndUpdate();
        _hint.Text = $"{_list.Items.Count} cars";
        if (_list.Items.Count > 0) _list.SelectedIndex = 0;
    }

    private void AcceptSelected()
    {
        if (_list.SelectedItem is not CarDonor d)
            throw new InvalidOperationException("Pick a car from the list.");
        Selected = d;
        DialogResult = DialogResult.OK;
        Close();
    }
}
