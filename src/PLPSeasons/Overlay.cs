using System.Linq;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using PLPSeasons.Core;

namespace PLPSeasons;

/// <summary>One transparent, click-through, always-on-top window per monitor. Works in physical pixels.</summary>
sealed class Overlay : Window
{
    public static bool Legacy;
    public readonly RectI Screen;            // physical pixels
    public readonly Simulation Sim;
    public IntPtr Handle { get; private set; }
    readonly Painter painter;

    public Overlay(RectI screen, int seed)
    {
        Screen = screen;
        Sim = new Simulation(screen.Width, screen.Height, seed);
        WindowStyle = WindowStyle.None; Background = Brushes.Transparent;
        // Fast path (default): a hardware-rendered window made see-through by DWM, instead of WPF's software
        // "AllowsTransparency" layered window that copies the whole screen every frame. --legacy keeps the old way.
        AllowsTransparency = Legacy;
        ShowInTaskbar = false; Topmost = true; ShowActivated = false; ResizeMode = ResizeMode.NoResize;
        Title = "PLP Seasons overlay";
        Left = 0; Top = 0; Width = 10; Height = 10;
        painter = new Painter(this);
        Content = painter;
        // mouse: only reaches us while the pointer is over snow / a ball / a snowman (App toggles click-through)
        painter.MouseLeftButtonDown += (_, e) => { var q = Phys(e.GetPosition(painter)); Sim.MouseDown(q.X, q.Y, e.ClickCount); painter.CaptureMouse(); e.Handled = true; };
        painter.MouseMove += (_, e) => { var q = Phys(e.GetPosition(painter)); Sim.MouseMove(q.X, q.Y); };
        painter.MouseLeftButtonUp += (_, e) => { var q = Phys(e.GetPosition(painter)); Sim.MouseUp(q.X, q.Y); painter.ReleaseMouseCapture(); };
        SourceInitialized += (_, _) =>
        {
            Handle = new WindowInteropHelper(this).Handle;
            if (!Legacy)
            {
                var src = HwndSource.FromHwnd(Handle);
                if (src?.CompositionTarget != null) src.CompositionTarget.BackgroundColor = Colors.Transparent;
                var m = new Native.MARGINS { L = -1, R = -1, T = -1, B = -1 };
                Native.DwmExtendFrameIntoClientArea(Handle, ref m);
            }
            Native.MakeClickThrough(Handle);
            if (!Legacy) Native.SetLayeredWindowAttributes(Handle, 0, 255, 2 /*LWA_ALPHA*/);
            Place();
        };
        DpiChanged += (_, _) => Place();
    }

    Point Phys(Point p) { var d = VisualTreeHelper.GetDpi(this); return new Point(p.X * d.DpiScaleX, p.Y * d.DpiScaleY); }
    bool clickThrough = true;
    /// <summary>Let clicks pass to the windows below, or take them (pointer over snow we can play with).</summary>
    public void SetInteractive(bool on)
    {
        if (Handle == IntPtr.Zero || on == !clickThrough) return;
        clickThrough = !on;
        var ex = (long)Native.GetWindowLongPtr(Handle, Native.GWL_EXSTYLE);
        ex = on ? ex & ~(long)Native.WS_EX_TRANSPARENT : ex | Native.WS_EX_TRANSPARENT;
        Native.SetWindowLongPtr(Handle, Native.GWL_EXSTYLE, new IntPtr(ex));
    }

    public void Place() => Native.SetWindowPos(Handle, Native.HWND_TOPMOST, Screen.Left, Screen.Top, Screen.Width, Screen.Height, Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW);

    public void Redraw() => painter.InvalidateVisual();
    public void KeepOnTop() => Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0, 0x1 | 0x2 | Native.SWP_NOACTIVATE);
}

/// <summary>Draws particles and snow caps. The DPI transform maps physical pixels to WPF units.</summary>
sealed class Painter : FrameworkElement
{
    readonly Overlay o;
    static readonly Brush FlakeSoft, FlakeCore, SnowFill, SnowEdge, Flash;
    static readonly Brush[] LeafBrushes, PetalBrushes;
    static readonly Pen DropPen, DropPenFar, SplashPen, SnowLine;
    static readonly Geometry Leaf, Petal;

    static Painter()
    {
        var soft = new RadialGradientBrush(Color.FromArgb(235, 255, 255, 255), Color.FromArgb(0, 255, 255, 255)) { GradientOrigin = new Point(0.5, 0.5) };
        soft.GradientStops.Clear();
        soft.GradientStops.Add(new GradientStop(Color.FromArgb(240, 255, 255, 255), 0));
        soft.GradientStops.Add(new GradientStop(Color.FromArgb(200, 250, 252, 255), 0.45));
        soft.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1));
        FlakeSoft = F(soft);
        FlakeCore = F(new SolidColorBrush(Color.FromArgb(245, 255, 255, 255)));
        var fill = new LinearGradientBrush(Color.FromRgb(255, 255, 255), Color.FromRgb(232, 240, 250), 90);
        SnowFill = F(fill);
        SnowEdge = F(new SolidColorBrush(Color.FromArgb(90, 150, 175, 210)));
        Flash = F(new SolidColorBrush(Color.FromArgb(255, 235, 240, 255)));
        DropPen = F(new Pen(F(new SolidColorBrush(Color.FromArgb(150, 200, 215, 235))), 1.3) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round });
        DropPenFar = F(new Pen(F(new SolidColorBrush(Color.FromArgb(80, 200, 215, 235))), 0.9));
        SplashPen = F(new Pen(F(new SolidColorBrush(Color.FromArgb(120, 220, 230, 245))), 1));
        SnowLine = F(new Pen(F(new SolidColorBrush(Color.FromArgb(70, 140, 165, 200))), 1));
        LeafBrushes = new[] { Color.FromRgb(214, 92, 34), Color.FromRgb(232, 160, 40), Color.FromRgb(178, 52, 30), Color.FromRgb(196, 128, 46) }
            .Select(c => F(new SolidColorBrush(c))).ToArray();
        PetalBrushes = new[] { Color.FromRgb(255, 196, 214), Color.FromRgb(255, 222, 232), Color.FromRgb(248, 176, 200) }.Select(c => F(new SolidColorBrush(c))).ToArray();
        Leaf = F(Geometry.Parse("M0,-1 C0.55,-0.6 0.6,0.25 0,1 C-0.6,0.25 -0.55,-0.6 0,-1 Z M0,-1 L0,1.25"));
        Petal = F(Geometry.Parse("M0,-1 C0.7,-0.7 0.7,0.6 0,1 C-0.7,0.6 -0.7,-0.7 0,-1 Z"));
        SoftSprite = Sprite(FlakeSoft, 1);
        var core = new RadialGradientBrush();
        core.GradientStops.Add(new GradientStop(Color.FromArgb(250, 255, 255, 255), 0));
        core.GradientStops.Add(new GradientStop(Color.FromArgb(235, 255, 255, 255), 0.55));
        core.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1));
        CoreSprite = Sprite(F(core), 1);
    }
    static T F<T>(T f) where T : Freezable { f.Freeze(); return f; }
    static readonly ImageSource SoftSprite, CoreSprite;
    static readonly Brush HitBrush = F(new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)));
    static readonly Brush BallFill = F(new RadialGradientBrush(Color.FromRgb(255, 255, 255), Color.FromRgb(206, 220, 238)) { GradientOrigin = new Point(0.35, 0.3), Center = new Point(0.42, 0.38), RadiusX = 0.7, RadiusY = 0.7 });
    static readonly Pen BallEdge = F(new Pen(F(new SolidColorBrush(Color.FromArgb(70, 120, 140, 170))), 1));
    static readonly Brush Shadow = F(new SolidColorBrush(Color.FromArgb(60, 40, 60, 90)));
    static readonly Brush Coal = F(new SolidColorBrush(Color.FromRgb(28, 28, 32)));
    static readonly Brush Carrot = F(new SolidColorBrush(Color.FromRgb(240, 128, 30)));
    static readonly Brush Scarf = F(new SolidColorBrush(Color.FromRgb(206, 40, 52)));
    static readonly Brush HatA = F(new SolidColorBrush(Color.FromRgb(40, 90, 170))), HatB = F(new SolidColorBrush(Color.FromRgb(190, 40, 50)));
    static readonly Pen Twig = F(new Pen(F(new SolidColorBrush(Color.FromRgb(92, 60, 36))), 3) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round });
    static readonly Pen Wire = F(new Pen(F(new SolidColorBrush(Color.FromArgb(240, 16, 26, 18))), 1.3));
    static readonly Color[] BulbColors = { Color.FromRgb(255, 70, 70), Color.FromRgb(255, 200, 60), Color.FromRgb(70, 200, 110), Color.FromRgb(80, 160, 255), Color.FromRgb(230, 110, 255) };
    static readonly Brush PlowBody = F(new SolidColorBrush(Color.FromRgb(242, 132, 30))), PlowDark = F(new SolidColorBrush(Color.FromRgb(40, 44, 52))),
        PlowBlade = F(new SolidColorBrush(Color.FromRgb(250, 205, 40))), PlowGlass = F(new SolidColorBrush(Color.FromArgb(220, 150, 200, 235)));
    static readonly Brush HeapFill = F(new SolidColorBrush(Color.FromRgb(246, 249, 253)));
    /// <summary>A flake drawn once into a small bitmap; every frame only copies it (much cheaper than gradients).</summary>
    static ImageSource Sprite(Brush b, double _)
    {
        const int N = 32;
        var v = new DrawingVisual();
        using (var dc = v.RenderOpen()) dc.DrawEllipse(b, null, new Point(N / 2.0, N / 2.0), N / 2.0, N / 2.0);
        var bmp = new System.Windows.Media.Imaging.RenderTargetBitmap(N, N, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(v); bmp.Freeze();
        return bmp;
    }

    public Painter(Overlay owner) { o = owner; IsHitTestVisible = true; RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.Linear); }

    protected override void OnRender(DrawingContext dc)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        dc.PushTransform(new ScaleTransform(1 / dpi.DpiScaleX, 1 / dpi.DpiScaleY));
        var sim = o.Sim;
        dc.DrawRectangle(HitBrush, null, new Rect(0, 0, sim.Width, sim.Height));   // 1/255 alpha: lets the window take the mouse when asked
        DrawGarlands(dc, sim);
        DrawSnowCaps(dc, sim);
        DrawWinter(dc, sim);
        foreach (ref readonly var p in System.Runtime.InteropServices.CollectionsMarshal.AsSpan(sim.P))
        {
            if (!p.Alive) continue;
            switch (p.Kind)
            {
                case Kind.Flake:
                {
                    double r = p.Size > 3.2 ? p.Size : p.Size * 0.75;
                    dc.DrawImage(p.Size > 3.2 ? SoftSprite : CoreSprite, new Rect(p.X - r, p.Y - r, r * 2, r * 2));
                    break;
                }
                case Kind.Crumb:
                    dc.DrawEllipse(FlakeCore, null, new Point(p.X, p.Y), p.Size, p.Size); break;
                case Kind.Drop:
                {
                    double len = p.VY * 0.022, k = sim.Wind * 0.25;
                    dc.DrawLine(p.Size > 1.4 ? DropPen : DropPenFar, new Point(p.X, p.Y), new Point(p.X - k * len, p.Y - len));
                    break;
                }
                case Kind.Splash:
                    dc.DrawEllipse(null, SplashPen, new Point(p.X, p.Y), p.Size * (1.2 - p.Life * 3), p.Size * 0.35); break;
                case Kind.Leaf: case Kind.Petal:
                {
                    var g = p.Kind == Kind.Leaf ? Leaf : Petal;
                    var b = p.Tint == 9 ? FlakeSoft : p.Kind == Kind.Leaf ? LeafBrushes[p.Tint % 4] : PetalBrushes[(int)(p.Phase * 10) % 3];
                    var m = new Matrix(); m.Scale(p.Size * 0.5 * Math.Abs(Math.Cos(p.Angle * 0.7)) + p.Size * 0.15, p.Size * 0.5); m.Rotate(p.Angle * 57.3); m.Translate(p.X, p.Y);
                    dc.PushTransform(new MatrixTransform(m));
                    dc.DrawGeometry(b, p.Kind == Kind.Leaf ? new Pen(b, 0.08) : null, g);
                    dc.Pop();
                    break;
                }
            }
        }
        dc.Pop();
    }

    void DrawSnowCaps(DrawingContext dc, Simulation sim)
    {
        if (sim.Flash > 0.01)
        {
            dc.PushOpacity(sim.Flash * 0.55);
            dc.DrawRectangle(Flash, null, new Rect(0, 0, sim.Width, sim.Height));
            dc.Pop();
        }
        foreach (var s in sim.Surfaces.Values)
        {
            var d = s.Depth; double max = 0; foreach (var v in d) max = Math.Max(max, v);
            if (max < 0.8) continue;
            var g = new StreamGeometry();
            using (var c = g.Open())
            {
                double baseY = s.Rect.Top + 1.5;
                c.BeginFigure(new Point(s.Rect.Left, baseY), true, true);
                var pts = new List<Point>(d.Length + 1);
                for (int i = 0; i < d.Length; i++) pts.Add(new Point(s.Rect.Left + i * Surface.Col, baseY - d[i]));
                pts.Add(new Point(s.Rect.Right, baseY - d[^1]));
                c.PolyLineTo(pts, true, true);
                c.LineTo(new Point(s.Rect.Right, baseY), true, true);
            }
            g.Freeze();
            dc.DrawGeometry(SnowFill, SnowLine, g);
        }
    }

    static double Tm => Environment.TickCount64 / 1000.0;

    void DrawGarlands(DrawingContext dc, Simulation sim)
    {
        if (sim.Season != Season.Winter) return;
        double t = Tm;
        foreach (var g in sim.Garlands)
        {
            double swing = g.Amp * Math.Sin(g.Ph);
            int n = Math.Max(1, (int)((g.X1 - g.X0) / 72)); double seg = (g.X1 - g.X0) / n;
            double Sag(int sI, double f) => 6 * 4 * f * (1 - f) * (1 + 0.12 * swing * Math.Cos(sI * 1.3)) + swing * 4 * f * (1 - f) * 0.9;
            double Shift(int sI, double f) => swing * 0.55 * 4 * f * (1 - f) * Math.Sin(g.Ph * 0.5 + sI);
            var geo = new StreamGeometry();
            using (var c = geo.Open())
                for (int si = 0; si < n; si++)
                {
                    c.BeginFigure(new Point(g.X0 + si * seg, g.Top), false, false);
                    for (int k = 1; k <= 10; k++) { double f = k / 10.0; c.LineTo(new Point(g.X0 + (si + f) * seg + Shift(si, f), g.Top + Sag(si, f)), true, true); }
                }
            geo.Freeze(); dc.DrawGeometry(null, Wire, geo);
            int prog = (int)(t / 8) % 4;
            for (int si = 0; si < n; si++)
                for (int bi = 0; bi < 3; bi++)
                {
                    double f = 0.2 + 0.3 * bi; int i = si * 3 + bi;
                    double x = g.X0 + (si + f) * seg + Shift(si, f), y = g.Top + Sag(si, f) + 5;
                    double ph = (i * 37 + (int)(g.Id % 97) * 11) % 100 / 100.0 * 6.28;
                    double b = 0.45 + 0.55 * Math.Max(0, Math.Sin(t * (sim.GarlandMode == 2 ? 0.9 : 1.6) + ph));
                    if (sim.GarlandMode == 1)
                        b = 0.2 + 0.8 * (prog switch { 1 => Math.Max(0, Math.Cos(t * 5 - i * 0.9)), 2 => ((int)(t * 2.2) + i) % 2 == 0 ? 1 : 0.15, 3 => 0.5 + 0.5 * Math.Sin(t * 2.2 - i * 0.35), _ => b });
                    var col = sim.GarlandMode == 2 ? Color.FromRgb(255, 200, 115) : BulbColors[(i + (int)(g.Id % 7)) % BulbColors.Length];
                    var glow = new RadialGradientBrush(Color.FromArgb((byte)(110 * b), col.R, col.G, col.B), Color.FromArgb(0, col.R, col.G, col.B)); glow.Freeze();
                    dc.DrawEllipse(glow, null, new Point(x, y + 1), 13, 13);
                    var bulb = new SolidColorBrush(Color.FromRgb((byte)(col.R * (0.55 + 0.45 * b)), (byte)(col.G * (0.55 + 0.45 * b)), (byte)(col.B * (0.55 + 0.45 * b)))); bulb.Freeze();
                    dc.DrawRectangle(Coal, null, new Rect(x - 1.6, y - 4.5, 3.2, 2.5));
                    dc.DrawEllipse(bulb, null, new Point(x, y + 1), 2.8, 4.3);
                }
        }
    }

    void DrawBall(DrawingContext dc, double x, double y, double r)
    {
        dc.DrawEllipse(BallFill, BallEdge, new Point(x, y), r, r);
    }

    void DrawWinter(DrawingContext dc, Simulation sim)
    {
        if (sim.Season != Season.Winter) return;
        double t = Tm;
        foreach (var m in sim.Snowmen)
        {
            double r0 = m.R[0], r1 = m.R[1], r2 = m.R[2];
            double c0 = m.Y - r0 * 0.9, c1 = c0 - r0 * 0.78 - r1 * 0.75, c2 = c1 - r1 * 0.8 - r2 * 0.75;
            double lean = Math.Sin(t * 0.9 + m.X * 0.01) * 0.025;
            double Px(double y) => m.X - (y - m.Y) * lean;
            dc.DrawEllipse(Shadow, null, new Point(m.X, m.Y + 1), r0 * 1.3, r0 * 0.28);
            // arms: twigs, the right one waves when the snowman is new
            double wave = m.Wave > 0 ? Math.Sin((2.6 - m.Wave) * 9) * 0.45 + 0.7 : 0;
            dc.DrawLine(Twig, new Point(Px(c1) - r1 * 0.7, c1 - r1 * 0.1), new Point(Px(c1) - r1 * 1.9, c1 - r1 * 0.75));
            double a = -0.5 - wave;
            dc.DrawLine(Twig, new Point(Px(c1) + r1 * 0.7, c1 - r1 * 0.1), new Point(Px(c1) + r1 * 0.7 + Math.Cos(a) * r1 * 1.3, c1 - r1 * 0.1 + Math.Sin(a) * r1 * 1.3));
            DrawBall(dc, Px(c0), c0, r0); DrawBall(dc, Px(c1), c1, r1); DrawBall(dc, Px(c2), c2, r2);
            for (int k = 0; k < 3; k++) dc.DrawEllipse(Coal, null, new Point(Px(c1), c1 - r1 * (0.45 - k * 0.4)), r1 * 0.1, r1 * 0.1);
            // scarf
            dc.DrawRoundedRectangle(Scarf, null, new Rect(Px(c2) - r2 * 1.05, c2 + r2 * 0.62, r2 * 2.1, r2 * 0.36), r2 * 0.15, r2 * 0.15);
            dc.DrawRoundedRectangle(Scarf, null, new Rect(Px(c2) + r2 * 0.25, c2 + r2 * 0.7, r2 * 0.35, r2 * 1.2), r2 * 0.12, r2 * 0.12);
            // face
            dc.DrawEllipse(Coal, null, new Point(Px(c2) - r2 * 0.3, c2 - r2 * 0.15), r2 * 0.1, r2 * 0.1);
            dc.DrawEllipse(Coal, null, new Point(Px(c2) + r2 * 0.3, c2 - r2 * 0.15), r2 * 0.1, r2 * 0.1);
            for (int k = 0; k < 5; k++) { double an = Math.PI * (0.2 + 0.6 * k / 4.0); dc.DrawEllipse(Coal, null, new Point(Px(c2) + Math.Cos(an) * r2 * 0.42, c2 + r2 * 0.05 + Math.Sin(an) * r2 * 0.35), r2 * 0.05, r2 * 0.05); }
            var nose = new StreamGeometry();
            using (var c = nose.Open()) { c.BeginFigure(new Point(Px(c2) + r2 * 0.05, c2 - r2 * 0.05), true, true); c.LineTo(new Point(Px(c2) + r2 * 0.95, c2 + r2 * 0.08), true, true); c.LineTo(new Point(Px(c2) + r2 * 0.05, c2 + r2 * 0.18), true, true); }
            nose.Freeze(); dc.DrawGeometry(Carrot, null, nose);
            // knitted hat with a pompom
            var hat = m.Hat == 0 ? HatA : HatB;
            dc.DrawRoundedRectangle(hat, null, new Rect(Px(c2) - r2 * 0.8, c2 - r2 * 1.45, r2 * 1.6, r2 * 0.75), r2 * 0.35, r2 * 0.35);
            dc.DrawRoundedRectangle(HeapFill, null, new Rect(Px(c2) - r2 * 0.88, c2 - r2 * 0.82, r2 * 1.76, r2 * 0.24), r2 * 0.1, r2 * 0.1);
            dc.DrawEllipse(HeapFill, null, new Point(Px(c2), c2 - r2 * 1.5), r2 * 0.22, r2 * 0.22);
        }
        foreach (var b in sim.Balls)
        {
            if (b.Rest) dc.DrawEllipse(Shadow, null, new Point(b.X, b.Y + b.R * 0.85), b.R * 1.1, b.R * 0.25);
            DrawBall(dc, b.X, b.Y, b.R);
            // a couple of marks so you can see it roll
            for (int k = 0; k < 3; k++) { double an = b.Ang + k * 2.1; dc.DrawEllipse(Shadow, null, new Point(b.X + Math.Cos(an) * b.R * 0.55, b.Y + Math.Sin(an) * b.R * 0.55), b.R * 0.12, b.R * 0.09); }
        }
        if (sim.Plow != PlowState.Idle && sim.Floor is { } f) DrawPlow(dc, sim, f);
    }

    void DrawPlow(DrawingContext dc, Simulation sim, Surface f)
    {
        double x = sim.PlowX, d = sim.PlowDir, ground = f.Rect.Top + 1, t = Tm;
        double S(double v) => x + v * d;          // mirror for the direction it drives
        // heap pushed in front of the blade
        double h = sim.PlowPileH;
        if (h > 1) dc.DrawEllipse(HeapFill, BallEdge, new Point(S(52 + h * 0.6), ground - h * 0.35), h * 0.95, h * 0.6);
        // body, cab, wheels, blade, beacon
        dc.DrawRoundedRectangle(PlowBody, null, new Rect(Math.Min(S(-40), S(30)), ground - 26, 70, 18), 4, 4);
        dc.DrawRoundedRectangle(PlowBody, null, new Rect(Math.Min(S(-34), S(-2)), ground - 46, 32, 22), 4, 4);
        dc.DrawRoundedRectangle(PlowGlass, null, new Rect(Math.Min(S(-28), S(-6)), ground - 42, 22, 12), 2, 2);
        dc.DrawEllipse(PlowDark, null, new Point(S(-24), ground - 6), 8, 8);
        dc.DrawEllipse(PlowDark, null, new Point(S(18), ground - 6), 8, 8);
        dc.DrawRectangle(PlowDark, null, new Rect(Math.Min(S(30), S(40)), ground - 18, 10, 4));
        dc.DrawRoundedRectangle(PlowBlade, null, new Rect(Math.Min(S(40), S(48)), ground - 30, 8, 30), 3, 3);
        bool on = ((int)(t * 3)) % 2 == 0;
        var beacon = new RadialGradientBrush(Color.FromArgb((byte)(on ? 200 : 60), 255, 170, 30), Color.FromArgb(0, 255, 170, 30)); beacon.Freeze();
        dc.DrawEllipse(beacon, null, new Point(S(-18), ground - 50), 14, 14);
        dc.DrawEllipse(PlowBlade, null, new Point(S(-18), ground - 49), 3.5, 3.5);
    }
}
