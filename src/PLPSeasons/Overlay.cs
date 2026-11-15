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
    public readonly RectI Screen;            // physical pixels
    public readonly Simulation Sim;
    public IntPtr Handle { get; private set; }
    readonly Painter painter;

    public Overlay(RectI screen, int seed)
    {
        Screen = screen;
        Sim = new Simulation(screen.Width, screen.Height, seed);
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        ShowInTaskbar = false; Topmost = true; ShowActivated = false; ResizeMode = ResizeMode.NoResize;
        Title = "PLP Seasons overlay";
        Left = 0; Top = 0; Width = 10; Height = 10;
        painter = new Painter(this);
        Content = painter;
        SourceInitialized += (_, _) =>
        {
            Handle = new WindowInteropHelper(this).Handle;
            Native.MakeClickThrough(Handle);
            Place();
        };
        DpiChanged += (_, _) => Place();
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
    }
    static T F<T>(T f) where T : Freezable { f.Freeze(); return f; }

    public Painter(Overlay owner) { o = owner; IsHitTestVisible = false; }

    protected override void OnRender(DrawingContext dc)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        dc.PushTransform(new ScaleTransform(1 / dpi.DpiScaleX, 1 / dpi.DpiScaleY));
        var sim = o.Sim;
        DrawSnowCaps(dc, sim);
        foreach (ref readonly var p in System.Runtime.InteropServices.CollectionsMarshal.AsSpan(sim.P))
        {
            if (!p.Alive) continue;
            switch (p.Kind)
            {
                case Kind.Flake:
                    if (p.Size > 3.2) dc.DrawEllipse(FlakeSoft, null, new Point(p.X, p.Y), p.Size, p.Size);
                    else dc.DrawEllipse(FlakeCore, null, new Point(p.X, p.Y), p.Size * 0.6, p.Size * 0.6);
                    break;
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
}
