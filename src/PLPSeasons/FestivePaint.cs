using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using PLPSeasons.Core;

namespace PLPSeasons;

/// <summary>0.5.4: drawing of the New Year things (Core/Festive.cs) — over the windows, like on the Mac.</summary>
sealed partial class Painter
{
    static readonly Geometry[] SplatGeo = MakeSplats();
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Splat, Geometry> splatShapes = new();
    static readonly Brush SplatShadow = Fz(new SolidColorBrush(Color.FromArgb(70, 40, 60, 95)));
    /// <summary>One splat's own shape (0.5.5): overlapping lumps where it hit, blobs and streaks thrown on along its
    /// flight, fine specks. Unit size: the splat spans -1 … 1.</summary>
    static Geometry SplatShape(Splat s)
    {
        if (splatShapes.TryGetValue(s, out var g)) return g;
        var r = new Random(s.Seed);
        double Rn(double a, double b) => a + (b - a) * r.NextDouble();
        var grp = new GeometryGroup { FillRule = FillRule.Nonzero };
        double ca = Math.Cos(s.Dir), sa = Math.Sin(s.Dir), core = Rn(0.3, 0.38), sq = Rn(0.75, 1);
        void Put(double u, double v, double rad, double stretch = 1, double ang = 0)
        {
            var e = new EllipseGeometry(new Point(0, 0), rad * stretch, rad);
            var m = new Matrix(); m.Rotate(ang * 57.2958); m.Translate(u * ca - v * sa, u * sa + v * ca);
            e.Transform = new MatrixTransform(m); grp.Children.Add(e);
        }
        for (int i = 0, n = (int)Rn(12, 20); i < n; i++) { double a = Rn(0, Math.PI * 2), d = core * Math.Sqrt(r.NextDouble()); Put(Math.Cos(a) * d * sq + core * 0.15, Math.Sin(a) * d, core * Rn(0.45, 0.8)); }
        for (int i = 0, n = (int)(Rn(12, 22) + 12 * s.Speed); i < n; i++)
        {
            bool fwd = r.NextDouble() < 0.75;
            double a = fwd ? Rn(-0.9, 0.9) : Rn(0, Math.PI * 2), d = core * (1.1 + Rn(0, 1.8) * (fwd ? 1 + s.Speed : 0.6));
            if (d > 0.92) continue;
            double rad = Math.Max(0.025, core * 0.62 * (1 - d) * Rn(0.45, 1.1));
            Put(Math.Cos(a) * d, Math.Sin(a) * d, rad, fwd ? Rn(1.2, 2.4) : 1, a);
            if (r.NextDouble() < 0.5) Put(Math.Cos(a) * d * 0.8, Math.Sin(a) * d * 0.8, rad * 0.6);
        }
        for (int i = 0, n = (int)Rn(18, 34); i < n; i++) { double a = Rn(0, Math.PI * 2), d = Rn(0.35, 0.95); Put(Math.Cos(a) * d, Math.Sin(a) * d, Rn(0.008, 0.025)); }
        grp.Freeze(); splatShapes.AddOrUpdate(s, grp);
        return grp;
    }
    static readonly Brush SplatFill = Fz(new RadialGradientBrush(Color.FromRgb(255, 255, 255), Color.FromRgb(196, 210, 232)) { GradientOrigin = new Point(0.4, 0.38), Center = new Point(0.45, 0.42), RadiusX = 0.6, RadiusY = 0.6 });
    static readonly Geometry IcicleGeo = Fz(Geometry.Parse("M-0.5,0 L0.5,0 C0.42,0.4 0.12,0.8 0.03,1 C-0.12,0.8 -0.42,0.4 -0.5,0 Z"));
    static readonly Brush IcicleFill = Fz(new LinearGradientBrush(new GradientStopCollection {
        new GradientStop(Color.FromArgb(150, 185, 215, 245), 0), new GradientStop(Color.FromArgb(235, 242, 250, 255), 0.35),
        new GradientStop(Color.FromArgb(160, 205, 230, 255), 0.6), new GradientStop(Color.FromArgb(140, 140, 184, 230), 1) }, new Point(0, 0.5), new Point(1, 0.5)));
    static readonly Pen IcicleEdge = Fz(new Pen(Fz(new SolidColorBrush(Color.FromArgb(110, 90, 128, 175))), 0.03));
    static readonly Brush Fur = Fz(new SolidColorBrush(Color.FromRgb(122, 75, 42))), FurDark = Fz(new SolidColorBrush(Color.FromRgb(74, 44, 23))), FurLight = Fz(new SolidColorBrush(Color.FromRgb(201, 162, 122)));
    static readonly Brush SantaRed = Fz(new SolidColorBrush(Color.FromRgb(200, 32, 42))), SleighRed = Fz(new SolidColorBrush(Color.FromRgb(179, 22, 31))), Gold = Fz(new SolidColorBrush(Color.FromRgb(244, 197, 66)));
    static readonly Brush White = Fz(new SolidColorBrush(Colors.White)), Skin = Fz(new SolidColorBrush(Color.FromRgb(242, 211, 181))), Sack = Fz(new SolidColorBrush(Color.FromRgb(123, 85, 50)));
    static readonly (Color box, Color ribbon)[] GiftColors = { (Color.FromRgb(216, 38, 46), Color.FromRgb(244, 197, 66)), (Color.FromRgb(31, 138, 76), Color.FromRgb(232, 48, 58)), (Color.FromRgb(47, 111, 214), Color.FromRgb(233, 238, 245)) };
    static readonly Dictionary<int, Brush> glowCache = new();
    static readonly Dictionary<string, Geometry> textCache = new();
    static T Fz<T>(T f) where T : Freezable { f.Freeze(); return f; }

    static Geometry[] MakeSplats()
    {
        var list = new Geometry[3];
        for (int k = 0; k < 3; k++)
        {
            var r = new Random(300 + k);
            var g = new StreamGeometry();
            using (var c = g.Open())
            {
                const int n = 22;
                for (int i = 0; i < n; i++)
                {
                    double a = (double)i / n * Math.PI * 2, rr = i % 3 == 0 ? 0.53 + r.NextDouble() * 0.3 : 0.4 + r.NextDouble() * 0.13;
                    var p = new Point(Math.Cos(a) * rr, Math.Sin(a) * rr);
                    if (i == 0) c.BeginFigure(p, true, true); else c.LineTo(p, true, true);
                }
            }
            var grp = new GeometryGroup { FillRule = FillRule.Nonzero };
            grp.Children.Add(g);
            for (int d = 0; d < 3; d++) { double x = -0.3 + r.NextDouble() * 0.6, l = 0.25 + r.NextDouble() * 0.4; grp.Children.Add(new RectangleGeometry(new Rect(x, 0.2, 0.08 + r.NextDouble() * 0.06, l), 0.04, 0.04)); }
            for (int d = 0; d < 7; d++) { double a = r.NextDouble() * Math.PI * 2, dd = 0.7 + r.NextDouble() * 0.22, s = 0.03 + r.NextDouble() * 0.04; grp.Children.Add(new EllipseGeometry(new Point(Math.Cos(a) * dd, Math.Sin(a) * dd), s, s)); }
            grp.Freeze(); list[k] = grp;
        }
        return list;
    }

    static Brush Glow(byte r, byte g, byte b, double a)
    {
        int q = (int)Math.Clamp(a * 10, 0, 10);
        int key = (r << 24) | (g << 16) | (b << 8) | q;
        if (glowCache.TryGetValue(key, out var br)) return br;
        var gb = new RadialGradientBrush();
        gb.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(255 * q / 10), r, g, b), 0));
        gb.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(90 * q / 10), r, g, b), 0.25));
        gb.GradientStops.Add(new GradientStop(Color.FromArgb(0, r, g, b), 1));
        gb.Freeze(); if (glowCache.Count < 4000) glowCache[key] = gb;
        return gb;
    }

    void DrawFestive(DrawingContext dc, Simulation sim)
    {
        double t = Tm, S = sim.Scale;
        // splats on the windows
        foreach (var s in sim.Splats)
        {
            double pop = Math.Min(1, s.Age * 9), fade = s.Age > 10 ? Math.Max(0, 1 - (s.Age - 10) / 4) : 1, r = s.R * (0.6 + 0.4 * pop);
            double y = s.Y + s.Slide; if (y > s.Win.Bottom - 4) continue;
            dc.PushOpacity(0.96 * fade);
            if (s.Slide > 2) dc.DrawRectangle(Fz(new SolidColorBrush(Color.FromArgb(50, 255, 255, 255))), null, new Rect(s.X - r * 0.28, s.Y, r * 0.55, s.Slide));
            var shape = SplatShape(s);
            dc.PushTransform(new MatrixTransform(r, 0, 0, r, s.X, y + 1.2 * S)); dc.DrawGeometry(SplatShadow, null, shape); dc.Pop();   // a soft cool shadow under it
            dc.PushTransform(new MatrixTransform(r, 0, 0, r, s.X, y));
            dc.DrawGeometry(SplatFill, null, shape);
            dc.Pop(); dc.Pop();
        }
        // icicles hanging from the top of the screen
        foreach (var c in sim.Icicles)
        {
            if (c.Len < 0.8) continue;
            double sw = c.Falling ? c.Rot : c.Wob * Math.Sin(t * 26 + c.X);
            double top = c.Falling ? c.Y - c.Len / 2 : 0;
            var m = new Matrix(); m.Scale(c.W, c.Len); m.Rotate(sw * 57.3); m.Translate(c.X, top);
            dc.PushTransform(new MatrixTransform(m)); dc.DrawGeometry(IcicleFill, IcicleEdge, IcicleGeo); dc.Pop();
        }
        // gifts
        foreach (var g in sim.Gifts)
        {
            double fade = g.Landed && g.Age > 110 ? Math.Max(0, 1 - (g.Age - 110) / 10) : 1, w = 30 * S;
            dc.PushOpacity(fade);
            if (g.Landed) dc.DrawEllipse(Shadow, null, new Point(g.X, g.Y + 13 * S), 17 * S, 3.5 * S);
            dc.PushTransform(new RotateTransform(g.Rot * 57.3, g.X, g.Y));
            var (box, rib) = GiftColors[g.C];
            var bb = new SolidColorBrush(box); var rb = new SolidColorBrush(rib);
            dc.DrawRectangle(bb, null, new Rect(g.X - w * 0.38, g.Y - w * 0.15, w * 0.76, w * 0.6));
            dc.DrawRectangle(bb, null, new Rect(g.X - w * 0.43, g.Y - w * 0.3, w * 0.86, w * 0.19));
            dc.DrawRectangle(rb, null, new Rect(g.X - w * 0.06, g.Y - w * 0.3, w * 0.12, w * 0.75));
            dc.DrawRectangle(rb, null, new Rect(g.X - w * 0.38, g.Y + w * 0.07, w * 0.76, w * 0.11));
            var bow = new Pen(rb, w * 0.08);
            dc.DrawEllipse(null, bow, new Point(g.X - w * 0.12, g.Y - w * 0.39), w * 0.12, w * 0.08);
            dc.DrawEllipse(null, bow, new Point(g.X + w * 0.12, g.Y - w * 0.39), w * 0.12, w * 0.08);
            dc.Pop(); dc.Pop();
        }
        DrawSleigh(dc, sim, t);
        // the sky lights up around a burst
        if (sim.FlashA > 0.01) dc.DrawEllipse(Glow(sim.FlashR, sim.FlashG, sim.FlashB, 0.16 * sim.FlashA), null, new Point(sim.FlashX, sim.FlashY), 210 * S, 210 * S);
        foreach (var r in sim.Rockets) dc.DrawEllipse(Glow(255, 230, 180, 1), null, new Point(r.X, r.Y), 4.5 * S, 4.5 * S);
        foreach (var s in sim.Sparks)
        {
            double k = s.Age / s.Life;
            switch (s.Kind)
            {
                case 2:
                {
                    double a = k > 0.8 ? (1 - k) * 5 : 1;
                    dc.PushTransform(new RotateTransform(s.Rot * 0.3 * 57.3, s.X, s.Y));
                    dc.DrawRectangle(new SolidColorBrush(Color.FromArgb((byte)(255 * a), s.R, s.G, s.B)), null, new Rect(s.X - s.Size / 2, s.Y - s.Size * 0.22 * Math.Abs(Math.Cos(s.Rot)), s.Size, s.Size * 0.45 * Math.Abs(Math.Cos(s.Rot)) + 0.5));
                    dc.Pop(); break;
                }
                case 3: case 5:
                    dc.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)(230 * (1 - k)), s.R, s.G, s.B)), null, new Point(s.X, s.Y), s.Size * 0.5, s.Size * 0.5); break;
                default:
                {
                    double a = Math.Pow(1 - k, 1.4);
                    if (s.Kind == 7 && k > 0.45) a *= ((int)(s.Age * 40 + s.Rot * 10)) % 3 == 0 ? 1 : 0.15;
                    if (s.Kind == 0 && k > 0.6) a *= 0.75 + 0.25 * Math.Sin(s.Age * 40 + s.Rot);
                    if (s.Kind == 6) a = (1 - k) * (0.7 + 0.3 * Math.Sin(s.Age * 30));
                    double sz = s.Size * (s.Kind == 1 ? 1 : 1 - 0.4 * k) * 0.5;
                    if (s.Kind == 0 || s.Kind == 7) dc.DrawEllipse(Glow(s.R, s.G, s.B, a * 0.45), null, new Point(s.X - s.VX * 0.035, s.Y - s.VY * 0.035), sz * 0.7, sz * 0.7);
                    dc.DrawEllipse(Glow(s.R, s.G, s.B, a), null, new Point(s.X, s.Y), sz, sz);
                    break;
                }
            }
        }
        foreach (var w in sim.Wishes)
        {
            double rise = Math.Min(1, w.Age / 0.9), e = 1 - Math.Pow(1 - rise, 3);
            double a = Math.Min(1, w.Age * 3) * (w.Age > 6 ? Math.Max(0, 1 - (w.Age - 6)) : 1);
            double half = TextGeo(w.Text, 30 * S).Bounds.Width / 2 + 10 * S;
            DrawText(dc, w.Text, 30 * S, Math.Clamp(w.X, half, sim.Width - half), w.Y - 60 * S * e, 0.8 + 0.2 * e, a);
        }
        DrawCountdown(dc, sim);
    }

    static ImageSource? Img(string n)
    {
        try { var b = new System.Windows.Media.Imaging.BitmapImage(); b.BeginInit(); b.UriSource = new Uri($"pack://application:,,,/Assets/festive/{n}.png"); b.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad; b.EndInit(); b.Freeze(); return b; }
        catch { return null; }
    }
    static readonly ImageSource? SleighImg = Img("sleigh"), Deer0 = Img("deer0"), Deer1 = Img("deer1");

    /// <summary>0.5.7 (owner: "Santa with a proper face, proper reindeer"): the painted art, the same as on the Mac.</summary>
    void DrawSleigh(DrawingContext dc, Simulation sim, double t)
    {
        if (sim.Sleigh is not { } s || sim.SleighPos is not { } p) return;
        if (SleighImg == null || Deer0 == null || Deer1 == null) { DrawSleighVector(dc, sim, t); return; }
        double k = 1.15 * sim.Scale, d = s.Dir;
        dc.PushTransform(new ScaleTransform(d, 1, p.x, 0));
        var harness = new List<Point> { new Point(p.x + 32 * k, p.y - 4 * k) };
        for (int i = 0; i < 4; i++)
        {
            double x = p.x + (72 + i * 47) * k, y = p.y - (8 + Math.Sin(t * 9 + i * 1.1) * 3 + i * 2) * k;
            double w = 58 * k, h = w * 0.75;
            harness.Add(new Point(x - w * 0.3, y + h * 0.04));
            dc.DrawImage(((int)(t * 9) + i) % 2 == 0 ? Deer0 : Deer1, new Rect(x - w / 2, y - h / 2, w, h));
            if (i == 3) { double nx = x + w * 0.402, ny = y - h * 0.197; dc.DrawEllipse(Glow(255, 50, 30, 0.8 + 0.2 * Math.Sin(t * 8)), null, new Point(nx, ny), 8 * k, 8 * k); }
        }
        var rein = new StreamGeometry();
        using (var c = rein.Open()) { c.BeginFigure(harness[0], false, false); for (int i = 1; i < harness.Count; i++) c.LineTo(harness[i], true, true); }
        rein.Freeze(); dc.DrawGeometry(null, new Pen(Fz(new SolidColorBrush(Color.FromRgb(140, 90, 40))), 1.4 * sim.Scale), rein);
        double sw = 110 * k, sh = sw * 0.692;
        dc.DrawImage(SleighImg, new Rect(p.x - sw / 2, p.y - sh / 2, sw, sh));
        dc.Pop();
    }

    void DrawSleighVector(DrawingContext dc, Simulation sim, double t)

    {
        if (sim.Sleigh is not { } s || sim.SleighPos is not { } p) return;
        double k = 1.55 * sim.Scale, d = s.Dir;
        dc.PushTransform(new ScaleTransform(d, 1, p.x, 0));     // drawn facing right, mirrored when flying left
        var harness = new List<Point>();
        for (int i = 0; i < 4; i++)
        {
            double x = p.x + (58 + i * 36) * k, y = p.y - (8 + Math.Sin(t * 9 + i * 1.1) * 2.5 + i * 2) * k;
            bool pose = ((int)(t * 9) + i) % 2 == 0;
            DrawDeer(dc, x, y, k, pose);
            harness.Add(new Point(x - 8 * k, y - 2 * k));
            if (i == 3) { dc.DrawEllipse(Glow(255, 50, 30, 0.8 + 0.2 * Math.Sin(t * 8)), null, new Point(x + 18 * k, y - 9 * k), 8 * k, 8 * k); dc.DrawEllipse(Fz(new SolidColorBrush(Color.FromRgb(255, 90, 80))), null, new Point(x + 18 * k, y - 9 * k), 1.6 * k, 1.6 * k); }
        }
        var rein = new StreamGeometry();
        using (var c = rein.Open()) { c.BeginFigure(new Point(p.x + 26 * k, p.y - 6 * k), false, false); foreach (var h in harness) c.LineTo(h, true, true); }
        rein.Freeze(); dc.DrawGeometry(null, new Pen(Fz(new SolidColorBrush(Color.FromRgb(140, 90, 40))), 1.4 * k / 1.55), rein);
        DrawSleighBody(dc, p.x, p.y, k);
        dc.Pop();
    }

    static void DrawDeer(DrawingContext dc, double x, double y, double k, bool pose)
    {
        // a 40×32 drawing centred on (x, y); local coordinates with y up like the Mac sprite
        Point P(double lx, double ly) => new(x + (lx - 20) * k, y - (ly - 16) * k);
        var leg = new Pen(FurDark, 2.2 * k) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        var legs = pose ? new[] { (11.0, 6.0, 3.0), (14.0, 9.0, 2.0), (25.0, 30.0, 3.0), (28.0, 34.0, 4.0) } : new[] { (11.0, 14.0, 2.0), (14.0, 18.0, 3.0), (25.0, 21.0, 3.0), (28.0, 24.0, 2.0) };
        foreach (var (lx, tx, ty) in legs) { dc.DrawLine(leg, P(lx, 13), P((lx + tx) / 2, 8)); dc.DrawLine(leg, P((lx + tx) / 2, 8), P(tx, ty)); }
        dc.DrawEllipse(Fur, null, P(19, 16), 12 * k, 5 * k);
        dc.DrawEllipse(FurLight, null, P(19, 13), 8 * k, 2 * k);
        var neck = new StreamGeometry();
        using (var c = neck.Open()) { c.BeginFigure(P(26, 18), true, true); c.LineTo(P(31, 25), true, true); c.LineTo(P(34, 23), true, true); c.LineTo(P(30, 14), true, true); }
        neck.Freeze(); dc.DrawGeometry(Fur, null, neck);
        dc.DrawEllipse(Fur, null, P(34.25, 24.25), 4.25 * k, 2.75 * k);
        dc.DrawEllipse(White, null, P(6.5, 19), 2 * k, 2 * k);
        dc.DrawEllipse(Coal, null, P(34.3, 24.8), 0.8 * k, 0.8 * k);
        var ant = new Pen(Fz(new SolidColorBrush(Color.FromRgb(217, 194, 154))), 1.3 * k) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        dc.DrawLine(ant, P(32, 26.5), P(29, 31)); dc.DrawLine(ant, P(30.4, 28.8), P(27, 29.5)); dc.DrawLine(ant, P(33.5, 26.5), P(35, 31.5)); dc.DrawLine(ant, P(34.3, 29.2), P(37.5, 30.5));
        dc.DrawLine(new Pen(SantaRed, 2 * k), P(27, 12), P(28, 21));
        dc.DrawEllipse(Gold, null, P(27.6, 15.8), 1.3 * k, 1.3 * k);
    }

    static void DrawSleighBody(DrawingContext dc, double x, double y, double k)
    {
        // a 74×50 drawing centred on (x, y), y up
        Point P(double lx, double ly) => new(x + (lx - 37) * k, y - (ly - 25) * k);
        var run = new Pen(Gold, 2.4 * k) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        var rg = new StreamGeometry();
        using (var c = rg.Open()) { c.BeginFigure(P(6, 5), false, false); c.LineTo(P(60, 5), true, true); c.BezierTo(P(68, 5), P(70, 12), P(66, 16), true, true); }
        rg.Freeze(); dc.DrawGeometry(null, run, rg);
        dc.DrawLine(run, P(18, 5), P(18, 12)); dc.DrawLine(run, P(46, 5), P(46, 12));
        dc.DrawEllipse(Sack, null, P(17, 29), 11 * k, 11 * k);
        dc.DrawEllipse(SantaRed, null, P(36, 25), 10 * k, 11 * k);
        dc.DrawEllipse(Skin, null, P(38, 37.5), 5 * k, 4.5 * k);
        dc.DrawEllipse(White, null, P(38, 32), 6.5 * k, 5 * k);
        var hat = new StreamGeometry();
        using (var c = hat.Open()) { c.BeginFigure(P(32, 40), true, true); c.LineTo(P(44, 40), true, true); c.LineTo(P(31, 48), true, true); }
        hat.Freeze(); dc.DrawGeometry(SantaRed, null, hat);
        dc.DrawRectangle(White, null, new Rect(P(31.5, 41.5), P(44.5, 38.5)));
        dc.DrawEllipse(White, null, P(30.75, 47.75), 2.25 * k, 2.25 * k);
        dc.DrawEllipse(Coal, null, P(40.3, 37.4), 0.8 * k, 0.8 * k);
        var body = new StreamGeometry();
        using (var c = body.Open())
        {
            c.BeginFigure(P(8, 10), true, true); c.LineTo(P(56, 10), true, true);
            c.BezierTo(P(62, 12), P(66, 24), P(64, 30), true, true); c.BezierTo(P(60, 30), P(56, 28), P(54, 24), true, true);
            c.LineTo(P(10, 24), true, true); c.BezierTo(P(6, 22), P(6, 14), P(8, 10), true, true);
        }
        body.Freeze(); dc.DrawGeometry(SleighRed, new Pen(Gold, 1.6 * k), body);
        dc.DrawLine(new Pen(Gold, 1.6 * k), P(12, 17), P(54, 17));
        dc.DrawRectangle(Fz(new SolidColorBrush(Color.FromRgb(47, 138, 76))), null, new Rect(P(47, 30), P(55, 23)));
    }

    Geometry TextGeo(string s, double size)
    {
        string key = s + "|" + size;
        if (textCache.TryGetValue(key, out var g)) return g;
        var ft = new FormattedText(s, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Black, FontStretches.Normal), size, Brushes.White, 1.0);
        var geo = ft.BuildGeometry(new Point(-ft.Width / 2, -ft.Height / 2)); geo.Freeze();
        textCache[key] = geo; return geo;
    }
    static readonly Pen[] TextGlow = { Fz(new Pen(Fz(new SolidColorBrush(Color.FromArgb(28, 255, 184, 64))), 26) { LineJoin = PenLineJoin.Round }), Fz(new Pen(Fz(new SolidColorBrush(Color.FromArgb(45, 255, 184, 64))), 15) { LineJoin = PenLineJoin.Round }), Fz(new Pen(Fz(new SolidColorBrush(Color.FromArgb(90, 255, 196, 90))), 7) { LineJoin = PenLineJoin.Round }) };
    void DrawText(DrawingContext dc, string s, double size, double x, double y, double scale, double alpha)
    {
        var g = TextGeo(s, size);
        dc.PushOpacity(alpha); dc.PushTransform(new MatrixTransform(scale, 0, 0, scale, x, y));
        foreach (var p in TextGlow) dc.DrawGeometry(null, p, g);
        dc.DrawGeometry(Brushes.White, null, g);
        dc.Pop(); dc.Pop();
    }
    void DrawCountdown(DrawingContext dc, Simulation sim)
    {
        double cx = sim.Width / 2, cy = sim.Height * 0.44, S = sim.Scale;
        if (sim.Countdown >= 0)
        {
            int n = (int)Math.Ceiling(sim.Countdown); if (n < 1) return;
            double f = n - sim.Countdown, pop = 1 + 0.45 * Math.Pow(Math.Max(0, 1 - f * 3), 3), a = f > 0.75 ? Math.Max(0, 1 - (f - 0.75) * 4) : 1;
            DrawText(dc, n.ToString(), 200 * S, cx, cy, pop, a);
        }
        else if (sim.Greet > 0)
        {
            double a = Math.Min(1, (8 - sim.Greet) * 2) * Math.Min(1, sim.Greet / 1.5), sc = 0.85 + 0.15 * Math.Min(1, (8 - sim.Greet) * 1.5);
            DrawText(dc, L.T("С Новым годом!", "Happy New Year!"), 92 * S, cx, cy - 50 * S, sc, a);
            DrawText(dc, sim.GreetYear.ToString(), 130 * S, cx, cy + 70 * S, sc, a);
        }
    }
}
