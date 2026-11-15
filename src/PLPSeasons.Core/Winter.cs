namespace PLPSeasons.Core;

public sealed class Ball
{
    public int Id; public double X, Y, R, VX, VY, Ang; public int? On; public bool Held, Rest;
}
public sealed class Snowman
{
    public double X, Y; public double[] R = new double[3]; public double Born; public int Hat; public double Wave;
}
public sealed class Garland
{
    public long Id; public double X0, X1, Top; public double Amp, Ph;
}
public enum PlowState { Idle, Driving, Leaving }

/// <summary>
/// Winter play, the same as on the Mac: scoop snow from the snow above the taskbar into a ball, roll it bigger,
/// stack three balls into a snowman, throw a ball at a snowman to knock it down, garlands on window tops that
/// swing when the mouse brushes them, and a snowplow that clears the snow off the taskbar.
/// All coordinates are physical pixels of one screen, y down.
/// </summary>
public sealed partial class Simulation
{
    public readonly List<Ball> Balls = new();
    public readonly List<Snowman> Snowmen = new();
    public readonly List<Garland> Garlands = new();
    public int GarlandMode = 1;              // 0 off, 1 coloured, 2 warm
    public bool PlowEnabled = true;
    public PlowState Plow = PlowState.Idle;
    public double PlowX, PlowDir = 1, PlowLoad, PlowCooldown = 20;
    int plowRuns, nextBall = 1;
    Ball? held;
    double mx = -1, my = -1, mvx, mvy, mt;

    public Surface? Floor => Surfaces.Values.FirstOrDefault(s => s.Id < 0);
    public bool Holding => held != null;
    double FloorTop(double x) { var f = Floor; return f == null ? Height : f.TopAt(x); }

    // ---------- the pointer ----------
    /// <summary>Should the overlay take the mouse here (instead of letting the click go to the window below)?</summary>
    public bool IsInteractive(double x, double y)
    {
        if (Season != Season.Winter) return false;
        if (held != null) return true;
        if (BallAt(x, y) != null || SnowmanAt(x, y) != null) return true;
        var f = Floor; if (f == null || x < f.Rect.Left || x >= f.Rect.Right) return false;
        double d = f.Depth[f.ColAt(x)];
        return d > 4 && y >= f.Rect.Top - d - 6 && y <= f.Rect.Top + 2;
    }

    public Ball? BallAt(double x, double y) =>
        Balls.Where(b => (b.X - x) * (b.X - x) + (b.Y - y) * (b.Y - y) <= (b.R + 4) * (b.R + 4)).OrderBy(b => b.R).FirstOrDefault();

    public Snowman? SnowmanAt(double x, double y)
    {
        foreach (var m in Snowmen)
        {
            double h = (m.R[0] + m.R[1] + m.R[2]) * 1.6 + m.R[2];
            if (Math.Abs(x - m.X) <= m.R[0] * 1.1 && y <= m.Y + 4 && y >= m.Y - h) return m;
        }
        return null;
    }

    public void MouseDown(double x, double y, int clicks)
    {
        if (Season != Season.Winter) return;
        mx = x; my = y; mvx = mvy = 0; mt = Time;
        if (clicks >= 2)
        {
            var sm = SnowmanAt(x, y); if (sm != null) { CrumbleSnowman(sm); return; }
            var bb = BallAt(x, y); if (bb != null) { CrumbleBall(bb); held = null; return; }
        }
        var b = BallAt(x, y);
        if (b != null && !Balls.Any(o => o.On == b.Id)) { b.Held = true; b.Rest = false; b.On = null; held = b; return; }
        var f = Floor; if (f == null) return;
        int c = f.ColAt(x);
        if (f.Depth[c] < 4) return;
        // scoop a little snow into a new ball
        double r = 7;
        Carve(f, x, r * 1.3, 3);
        held = new Ball { Id = nextBall++, X = x, Y = Math.Min(y, f.TopAt(x) - r), R = r, Held = true };
        Balls.Add(held);
    }

    public void MouseMove(double x, double y)
    {
        if (mx >= 0)
        {
            double dt = Math.Max(0.008, Time - mt);
            mvx = mvx * 0.6 + (x - mx) / dt * 0.4; mvy = mvy * 0.6 + (y - my) / dt * 0.4;
            // brushing a garland makes it swing
            double sp = Math.Sqrt(mvx * mvx + mvy * mvy);
            if (sp > 60) foreach (var g in Garlands) if (x > g.X0 - 10 && x < g.X1 + 10 && Math.Abs(y - (g.Top + 8)) < 18) g.Amp = Math.Min(9, Math.Max(g.Amp, 2 + sp / 160));
        }
        mx = x; my = y; mt = Time;
        if (held == null) return;
        var f = Floor;
        double dx = x - held.X;
        held.X = x;
        double top = f == null ? Height : f.TopAt(x);
        if (f != null && y + held.R >= top - 2 && x >= f.Rect.Left && x < f.Rect.Right)
        {
            // rolling along the snow: the ball picks the snow up and grows
            held.Y = top - held.R + Math.Min(held.R * 0.25, 4);
            held.Ang += dx / held.R;
            double moved = Math.Abs(dx);
            if (moved > 0.5 && held.R < 90)
            {
                double got = Carve(f, x, held.R * 0.9, Math.Min(6, moved * 0.35));
                double area = Math.PI * held.R * held.R + got * Surface.Col * 1.4;
                held.R = Math.Min(90, Math.Sqrt(area / Math.PI));
            }
        }
        else held.Y = y;
    }

    public void MouseUp(double x, double y)
    {
        if (held != null)
        {
            held.Held = false; held.VX = Math.Clamp(mvx, -2500, 2500); held.VY = Math.Clamp(mvy, -2500, 2500);
            if (Math.Abs(held.VX) + Math.Abs(held.VY) < 200) held.VX = held.VY = 0;
            held = null;
        }
        mx = -1;
    }

    /// <summary>Remove snow under a ball (cosine dip); returns the amount removed (in depth·columns).</summary>
    double Carve(Surface f, double x, double r, double depth)
    {
        double got = 0;
        int c0 = f.ColAt(x - r), c1 = f.ColAt(x + r);
        for (int c = c0; c <= c1; c++)
        {
            double t = (f.Rect.Left + c * Surface.Col - x) / r;
            double take = Math.Min(f.Depth[c], depth * 0.5 * (1 + Math.Cos(Math.PI * Math.Clamp(t, -1, 1))));
            f.Depth[c] -= take; got += take;
        }
        return got;
    }

    public void CrumbleBall(Ball b)
    {
        Balls.Remove(b);
        foreach (var o in Balls.Where(o => o.On == b.Id)) { o.On = null; o.Rest = false; }
        Burst(b.X, b.Y, b.R);
    }

    public void CrumbleSnowman(Snowman m)
    {
        Snowmen.Remove(m);
        double y = m.Y;
        for (int k = 0; k < 3; k++) { double r = m.R[k]; y -= k == 0 ? r * 0.9 : r * 1.6; Burst(m.X, y, r); }
    }

    void Burst(double x, double y, double r)
    {
        int n = (int)Math.Clamp(r * 1.5, 12, 90);
        for (int i = 0; i < n; i++)
        {
            double a = rng.NextDouble() * Math.PI * 2, d = rng.NextDouble() * r;
            Add(new Particle { Kind = Kind.Crumb, X = x + Math.Cos(a) * d, Y = y + Math.Sin(a) * d, VX = Math.Cos(a) * (60 + rng.NextDouble() * 160),
                VY = -80 - rng.NextDouble() * 200, Size = 2 + rng.NextDouble() * Math.Min(7, r * 0.18), Life = 3, Alive = true });
        }
        var f = Floor; if (f != null) for (int i = 0; i < 6; i++) Deposit(f, x + (rng.NextDouble() - 0.5) * r * 2, r * 0.25);
    }

    // ---------- the world moving on its own ----------
    void StepWinter(double dt)
    {
        if (Season != Season.Winter)
        {
            // the snow melts: balls, snowmen and the plow go away
            if (Balls.Count > 0 || Snowmen.Count > 0) { foreach (var b in Balls.ToList()) CrumbleBall(b); foreach (var m in Snowmen.ToList()) CrumbleSnowman(m); held = null; }
            Plow = PlowState.Idle; Garlands.Clear();
            return;
        }
        StepBalls(dt);
        BuildSnowmen();
        StepGarlands(dt);
        StepPlow(dt);
        foreach (var m in Snowmen) m.Wave = Math.Max(0, m.Wave - dt);
    }

    void StepBalls(double dt)
    {
        foreach (var b in Balls)
        {
            if (b.Held) continue;
            if (b.On != null)
            {
                var below = Balls.FirstOrDefault(o => o.Id == b.On);
                if (below == null || below.Held) { b.On = null; b.Rest = false; }
                else { b.X = below.X; b.Y = below.Y - below.R - b.R * 0.8; continue; }
            }
            if (b.Rest) { b.Y = FloorTop(b.X) - b.R * 0.85; continue; }
            b.VY += 1400 * dt; b.X += b.VX * dt; b.Y += b.VY * dt; b.Ang += b.VX * dt / b.R;
            // a thrown ball knocks a snowman down
            var hit = Snowmen.FirstOrDefault(m => Math.Abs(b.X - m.X) < m.R[0] + b.R && b.Y > m.Y - (m.R[0] + m.R[1] + m.R[2]) * 1.7 && b.Y < m.Y + 4);
            if (hit != null && Math.Abs(b.VX) + Math.Abs(b.VY) > 300) { CrumbleSnowman(hit); b.R = 0; continue; }
            // landing on another ball (stacking)
            var on = Balls.FirstOrDefault(o => o != b && !o.Held && (o.Rest || o.On != null) && Math.Abs(o.X - b.X) < o.R * 0.7 && b.Y + b.R >= o.Y - o.R * 0.9 && b.Y < o.Y && b.VY > 0);
            if (on != null && b.R <= on.R * 1.05) { b.On = on.Id; b.VX = b.VY = 0; continue; }
            double top = FloorTop(b.X);
            if (b.Y + b.R * 0.85 >= top)
            {
                if (b.VY > 900 && b.R < 25) { b.R = 0; Burst(b.X, top, 10); continue; }   // a small ball thrown hard bursts
                b.Y = top - b.R * 0.85; b.VY = 0; b.VX *= 0.0; b.Rest = true;
            }
            if (b.X < -200 || b.X > Width + 200 || b.Y > Height + 200) b.R = 0;
        }
        foreach (var dead in Balls.Where(b => b.R <= 0).ToList()) Balls.Remove(dead);
    }

    /// <summary>Three stacked balls → a snowman (any order: the biggest goes to the bottom).</summary>
    void BuildSnowmen()
    {
        foreach (var top in Balls)
        {
            if (top.On == null || top.Held) continue;
            var mid = Balls.FirstOrDefault(o => o.Id == top.On); if (mid?.On == null || mid.Held) continue;
            var bot = Balls.FirstOrDefault(o => o.Id == mid.On); if (bot == null || bot.On != null || bot.Held || !bot.Rest) continue;
            var rs = new[] { bot.R, mid.R, top.R }.OrderByDescending(r => r).ToArray();
            double r0 = rs[0], r1 = Math.Min(rs[1], r0 * 0.9), r2 = Math.Min(rs[2], r1 * 0.9);
            if (r2 < 5) continue;
            Snowmen.Add(new Snowman { X = bot.X, Y = FloorTop(bot.X), R = new[] { r0, r1, r2 }, Born = Time, Hat = Snowmen.Count % 2, Wave = 2.6 });
            Balls.Remove(top); Balls.Remove(mid); Balls.Remove(bot);
            return;
        }
    }

    /// <summary>For demos and tests: put a ready snowman on the snow.</summary>
    public Snowman AddSnowman(double x, double r0)
    {
        var m = new Snowman { X = x, Y = FloorTop(x), R = new[] { r0, r0 * 0.75, r0 * 0.55 }, Born = Time, Hat = Snowmen.Count % 2, Wave = 2.6 };
        Snowmen.Add(m); return m;
    }

    void StepGarlands(double dt)
    {
        var keep = new HashSet<long>();
        if (GarlandMode != 0)
            foreach (var s in Surfaces.Values.Where(s => s.Id >= 0 && s.Rect.Width > 220).Take(3))
            {
                keep.Add(s.Id);
                var g = Garlands.FirstOrDefault(o => o.Id == s.Id);
                if (g == null) { g = new Garland { Id = s.Id, Amp = 7 }; Garlands.Add(g); }      // a new window: the garland swings in
                g.X0 = s.Rect.Left + 78; g.X1 = s.Rect.Right - 140; g.Top = s.Rect.Top + 2;      // keep the window buttons readable
                if (g.X1 - g.X0 < 80) g.X1 = s.Rect.Right - 12;
            }
        Garlands.RemoveAll(g => !keep.Contains(g.Id));
        foreach (var g in Garlands) { g.Amp *= Math.Exp(-1.6 * dt); g.Ph += dt * 9.5; }
    }

    // ---------- the snowplow ----------
    public void CallPlow() { if (Season == Season.Winter && Plow == PlowState.Idle && Floor != null) StartPlow(); }
    void StartPlow()
    {
        var f = Floor!; PlowDir = plowRuns++ % 2 == 0 ? 1 : -1; Plow = PlowState.Driving; PlowLoad = 0;
        PlowX = PlowDir > 0 ? f.Rect.Left - 90 : f.Rect.Right + 90;
    }
    public double PlowPileH => Math.Sqrt(Math.Max(0, PlowLoad * Surface.Col * 0.75) / 1.1);

    void StepPlow(double dt)
    {
        PlowCooldown = Math.Max(0, PlowCooldown - dt);
        var f = Floor; if (f == null) { Plow = PlowState.Idle; return; }
        switch (Plow)
        {
            case PlowState.Idle:
                if (!PlowEnabled || PlowCooldown > 0) return;
                double sum = 0, mxd = 0; int n = 0;
                for (int c = 0; c < f.Depth.Length; c += 4) { sum += f.Depth[c]; mxd = Math.Max(mxd, f.Depth[c]); n++; }
                if (n > 0 && (sum / n > 22 || mxd > 40)) StartPlow();
                return;
            case PlowState.Driving: case PlowState.Leaving:
                PlowX += PlowDir * 70 * dt;
                double blade = PlowX + PlowDir * 46;
                if (Plow == PlowState.Driving)
                {
                    // a snowman in the way: leave the heap in front of it and turn back
                    var sm = Snowmen.FirstOrDefault(m => Math.Abs(m.X - (blade + PlowDir * PlowPileH)) < m.R[0] + 6);
                    if (sm != null) { LeaveHeap(blade); Plow = PlowState.Leaving; PlowDir = -PlowDir; break; }
                    // balls get pushed along
                    foreach (var b in Balls.Where(b => !b.Held && b.On == null && Math.Abs(b.X - blade) < b.R + PlowPileH + 4))
                        b.X = blade + PlowDir * (b.R + PlowPileH + 2);
                    if (blade > f.Rect.Left && blade < f.Rect.Right)
                    {
                        // the blade and the whole plow behind it keep the road clear (otherwise the snow ahead slides back in)
                        int c = f.ColAt(blade), back = (int)(70 / Surface.Col);
                        for (int k = -back; k <= 1; k++) { int j = c + k * (int)PlowDir; if (j < 0 || j >= f.Depth.Length) continue; double take = Math.Max(0, f.Depth[j] - 1.2); f.Depth[j] -= take; PlowLoad += take; }
                    }
                    if ((PlowDir > 0 && blade > f.Rect.Right + 10) || (PlowDir < 0 && blade < f.Rect.Left - 10))
                    { Burst(PlowDir > 0 ? f.Rect.Right - 5 : f.Rect.Left + 5, f.Rect.Top - 10, Math.Min(40, PlowPileH)); PlowLoad = 0; Plow = PlowState.Leaving; }
                }
                if (PlowX < f.Rect.Left - 200 || PlowX > f.Rect.Right + 200) { Plow = PlowState.Idle; PlowCooldown = 90; }
                break;
        }
    }

    void LeaveHeap(double x)
    {
        var f = Floor!; double h = PlowPileH;
        for (int c = f.ColAt(x - h * 1.4); c <= f.ColAt(x + h * 1.4); c++)
        {
            double t = (f.Rect.Left + c * Surface.Col - x) / (h * 1.4);
            f.Depth[c] = Math.Min(f.MaxDepth, f.Depth[c] + h * 0.5 * (1 + Math.Cos(Math.PI * Math.Clamp(t, -1, 1))));
        }
        PlowLoad = 0;
    }
}
