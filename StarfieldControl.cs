using System.Drawing.Drawing2D;

namespace NFSU2CustomCarSaves;

/// <summary>
/// Galaxy panel: cached gradient/nebula backdrop + twinkling stars + optional overlay painter.
/// Animate=false gives a calm static sky (transparent children composite fine over it).
/// </summary>
internal class StarfieldControl : Panel
{
    private readonly Starfield _field;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 33 };
    private Bitmap? _bg;
    private Size _bgSize;
    private bool _animate = true;
    private long _last;

    public Action<Graphics, Rectangle>? Overlay { get; set; }
    public float Time => _field.Time;

    public bool Animate
    {
        get => _animate;
        set
        {
            _animate = value;
            SyncTimer();
            Invalidate();
        }
    }

    public StarfieldControl(int stars = 90, int seed = 7)
    {
        _field = new Starfield(stars, seed);
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = UiTheme.Bg;
        _timer.Tick += (_, _) => Tick();
    }

    private void SyncTimer()
    {
        bool want = _animate && IsHandleCreated && Visible && !DesignMode;
        if (want && !_timer.Enabled)
        {
            _last = Environment.TickCount64;
            _timer.Start();
        }
        else if (!want && _timer.Enabled)
        {
            _timer.Stop();
        }
    }

    private void Tick()
    {
        long now = Environment.TickCount64;
        float dt = Math.Clamp((now - _last) / 1000f, 0f, 0.1f);
        _last = now;
        var f = FindForm();
        if (f == null || f.WindowState == FormWindowState.Minimized) return;
        _field.Step(dt);
        Invalidate();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        SyncTimer();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        _timer.Stop();
        base.OnHandleDestroyed(e);
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        SyncTimer();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var r = ClientRectangle;
        if (r.Width <= 0 || r.Height <= 0) return;
        if (_bg == null || _bgSize != r.Size)
        {
            _bg?.Dispose();
            _bg = GalaxyCanvas.RenderBackdrop(r.Size);
            _bgSize = r.Size;
        }
        var g = e.Graphics;
        g.DrawImageUnscaled(_bg, 0, 0);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        _field.Draw(g, r, _animate ? 1f : 0.75f);
        Overlay?.Invoke(g, r);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _bg?.Dispose();
        }
        base.Dispose(disposing);
    }
}
