namespace PLPSeasons.Core;

public sealed class Ball
{
    public int Id; public double X, Y, R, VX, VY, Ang, Age; public int? On;
}
public sealed class Snowman
{
    public double X, Y, VX, VY, Spin, Born, Wave, LastWave = -99; public double[] R = new double[3]; public int Hat; public bool Held;
}
public sealed class Garland
{
    public long Id; public double X0, X1, Top; public double Amp, Ph;
}
public enum PlowState { Idle, Driving, Waiting, Reversing }

/// <summary>
/// Winter play, ported from the Mac app (Winter.swift) so it feels the same: scoop snow above the taskbar into a ball
/// and roll it — it grows and leaves a trench; stack three balls into a snowman; throw a ball at a snowman to knock
/// it down; drag and throw snowmen; scrape the snow off a window top with the mouse; garlands that swing; and a
/// snowplow that pushes all the snow into a growing heap and tips it over the end.
/// Coordinates are physical pixels of one screen, y down. Mac sizes (points) are multiplied by <see cref="Scale"/>.
/// </summary>
public sealed partial class Simulation
{
    public readonly List<Ball> Balls = new();
    public readonly List<Snowman> Snowmen = new();
    public readonly List<Garland> Garlands = new();
    public double Scale = 1;                  // physical pixels per point (Windows display scaling)
    public int GarlandMode = 1;               // 0 off, 1 coloured, 2 warm
    public bool PlowEnabled = true;
    public PlowState Plow = PlowState.Idle;
    public double PlowX, PlowDir = 1, PlowLoad, PlowCooldown = 25, PlowWheel, PlowWait;
    int plowRuns, nextBall = 1;
    enum Hold { None, Ball, Man, Scrape }
    Hold hold; int heldBall; Snowman? heldMan; long scrapeId; double scrapeX, downX, downY, spillAcc;
    double mx = -1, my = -1, mvx, mvy, mt;

    public double PlowW => 150 * Scale;
    const double PlowBlade = 0.36;
    double PileMax => 70 * Scale;
    double MaxBall => 46 * Scale;

    public Surface? Floor => Surfaces.Values.FirstOrDefault(s => s.Id < 0);
    public bool Holding => hold != Hold.None;
    public Ball? HeldBall => hold == Hold.Ball ? Balls.FirstOrDefault(b => b.Id == heldBall) : null;
    double SnowUnder(double x) { var f = Floor; return f == null ? 0 : f.Depth[f.ColAt(x)]; }
    /// <summary>The y of the snow surface above the taskbar (screen bottom if there is no floor).</summary>
    public double SnowTop(double x) { var f = Floor; return f == null ? Height : f.Rect.Top - f.Depth[f.ColAt(x)]; }
    double Base => Floor?.Rect.Top ?? Height;

    // ---------- the pointer ----------
    public bool IsInteractive(double x, double y)
    {
        if (Season != Season.Winter) return false;
        if (hold != Hold.None) return true;
        if (NearestBall(x, y) != null || SnowmanAt(x, y) != null) return true;
        var f = Floor;
        if (f != null && x >= f.Rect.Left && x < f.Rect.Right)
        {
            double d = SnowUnder(x);
            if (d >= 2 && y < Base - 0.5 && y > Base - d - 6 * Scale) return true;   // never on the taskbar itself
        }
        return CapUnder(x, y) != null;
    }

    Ball? NearestBall(double x, double y)
    {
        Ball? best = null; double bd = double.MaxValue;
        foreach (var b in Balls)
        {
            double d = Math.Sqrt((b.X - x) * (b.X - x) + (b.Y - y) * (b.Y - y));
            if (d < b.R + 8 * Scale && d - b.R < bd) { best = b; bd = d - b.R; }
        }
        return best;
    }
    public Ball? BallAt(double x, double y) => NearestBall(x, y);

    public Snowman? SnowmanAt(double x, double y)
    {
        foreach (var m in Snowmen)
        {
            double h = (m.R[0] + m.R[1] + m.R[2]) * 1.9;
            if (Math.Abs(x - m.X) < m.R[0] + 6 * Scale && y < m.Y + 4 * Scale && y > m.Y - h) return m;
        }
        return null;
    }

    Surface? CapUnder(double x, double y)
    {
        foreach (var s in Surfaces.Values)
        {
            if (s.Id < 0 || x < s.Rect.Left || x >= s.Rect.Right) continue;
            double h = s.Depth[s.ColAt(x)];
            if (h >= 1.5 && y < s.Rect.Top - 0.5 && y > s.Rect.Top - h - 6 * Scale) return s;
        }
        return null;
    }

    public void MouseDown(double x, double y, int clicks)
    {
        if (Season != Season.Winter) return;
        mx = x; my = y; mvx = mvy = 0; mt = Time;
        if (clicks >= 2)
        {
            var sm = SnowmanAt(x, y); if (sm != null) { CrumbleSnowman(sm); hold = Hold.None; return; }
            var bb = NearestBall(x, y); if (bb != null) { CrumbleBall(bb); hold = Hold.None; return; }
        }
        var b = NearestBall(x, y);
        if (b != null)
        {
            foreach (var o in Balls.Where(o => o.On == b.Id)) o.On = null;   // whatever sat on it falls off
            b.On = null; hold = Hold.Ball; heldBall = b.Id; return;
        }
        var m = SnowmanAt(x, y);
        if (m != null) { m.Held = true; heldMan = m; hold = Hold.Man; return; }
        var cap = CapUnder(x, y);
        if (cap != null) { hold = Hold.Scrape; scrapeId = cap.Id; scrapeX = x; downX = x; downY = y; spillAcc = 0; return; }
        ScoopBall(x, y, null);
    }

    void ScoopBall(double x, double y, Surface? cap)
    {
        if (cap != null)
        {
            int i = cap.ColAt(x);
            for (int k = -3; k <= 3; k++) { int j = i + k; if (j >= 0 && j < cap.Depth.Length) cap.Depth[j] = Math.Max(0, cap.Depth[j] - 5 * Scale); }
        }
        else if (Floor is { } f)
        {
            int c = f.ColAt(x);
            for (int k = -3; k <= 3; k++) { int j = c + k; if (j >= 0 && j < f.Depth.Length) f.Depth[j] = Math.Max(0, f.Depth[j] - 4.5 * Scale * Math.Exp(-k * k / 4.5)); }
        }
        var b = new Ball { Id = nextBall++, X = x, Y = y, R = 7 * Scale };
        Balls.Add(b); hold = Hold.Ball; heldBall = b.Id;
    }

    public void MouseMove(double x, double y)
    {
        if (mx >= 0)
        {
            double dt = Math.Max(0.008, Time - mt);
            mvx = mvx * 0.5 + (x - mx) / dt * 0.5; mvy = mvy * 0.5 + (y - my) / dt * 0.5;
            double sp = Math.Sqrt(mvx * mvx + mvy * mvy);
            if (sp > 60) foreach (var g in Garlands) if (x > g.X0 - 10 && x < g.X1 + 10 && Math.Abs(y - (g.Top + 6)) < 16 * Scale) g.Amp = Math.Min(9, Math.Max(g.Amp, 2 + sp / 160));
            foreach (var m in Snowmen) if (Math.Abs(x - m.X) < 160 * Scale && Math.Abs(y - m.Y) < 220 * Scale && Time - m.LastWave > 7) { m.Wave = 2.2; m.LastWave = Time; }
        }
        mx = x; my = y; mt = Time;
        switch (hold)
        {
            case Hold.Ball:
            {
                var b = HeldBall; if (b == null) { hold = Hold.None; return; }
                double g = SnowTop(x), npy = y;
                if (npy + b.R > g - 3 * Scale) npy = g - b.R * 0.9;          // it can't go into the snow
                double dx = x - b.X;
                if (npy + b.R > g - 4 * Scale)
                {
                    // rolling on the snow: the ball picks it up and grows, leaving a trench
                    double front = x + (dx >= 0 ? 1 : -1) * (Math.Max(Surface.Col * 2, b.R * 0.8) + Surface.Col);
                    double depth = Math.Max(SnowUnder(x), SnowUnder(front));
                    if (depth > 0.5)
                    {
                        double gain = Math.Abs(dx) * Math.Min(depth, b.R) * 0.45;
                        b.R = Math.Min(MaxBall, Math.Sqrt(b.R * b.R + gain / Math.PI));
                        PressPath(b.X, x, b.R, 0.55);
                    }
                    b.Ang += dx / Math.Max(b.R, 1);
                }
                b.X = x; b.Y = npy;
                break;
            }
            case Hold.Man:
                if (heldMan == null) { hold = Hold.None; return; }
                heldMan.X = x; heldMan.Y = Math.Min(SnowTop(x) + 2 * Scale, y + heldMan.R[0]);
                break;
            case Hold.Scrape:
                // pulled straight up before scraping sideways: a snowball is scooped from this window's snow instead
                if (downY - y > 14 * Scale && Math.Abs(scrapeX - downX) < 6 && Math.Abs(x - downX) < 10 * Scale && Surfaces.TryGetValue(scrapeId, out var cs))
                { hold = Hold.None; ScoopBall(downX, downY, cs); MouseMove(x, y); return; }
                Scrape(x);
                break;
        }
    }

    public void MouseUp(double x, double y)
    {
        if (Time - mt > 0.1) mvx = mvy = 0;            // held still before letting go: it is put down, not thrown
        switch (hold)
        {
            case Hold.Ball:
                if (HeldBall is { } b) { b.VX = Math.Clamp(mvx * 0.5, -900 * Scale, 900 * Scale); b.VY = Math.Clamp(mvy * 0.5, -900 * Scale, 900 * Scale); }
                break;
            case Hold.Man:
                if (heldMan != null)
                {
                    heldMan.Held = false;
                    heldMan.VX = Math.Clamp(mvx * 0.6, -1600 * Scale, 1600 * Scale); heldMan.VY = Math.Clamp(mvy * 0.6, -1400 * Scale, 1200 * Scale);
                    heldMan.Spin = heldMan.VX * 0.004 / Scale;
                }
                break;
        }
        hold = Hold.None; heldMan = null; mx = -1;
    }

    /// <summary>A ball-shaped dip pressed into the snow (gentle cosine walls, never digs twice in one pass).</summary>
    void PressTrack(double x, double r, double dir, double factor)
    {
        var f = Floor; if (f == null) return;
        double w = Math.Max(Surface.Col * 2, r * 0.8);
        double ahead = SnowUnder(x + (dir >= 0 ? 1 : -1) * (w + Surface.Col));
        double rf = Math.Max(SnowUnder(x), ahead);
        double floorLevel = Math.Max(0, rf - Math.Max(0.6, Math.Min(rf, r) * factor));
        for (int c = f.ColAt(x - w); c <= f.ColAt(x + w); c++)
        {
            double u = (f.Rect.Left + c * Surface.Col - x) / w;
            if (Math.Abs(u) > 1) continue;
            double level = floorLevel + (rf - floorLevel) * (1 - Math.Cos(Math.PI * u)) / 2;
            if (f.Depth[c] > level) f.Depth[c] = level;
        }
    }
    void PressPath(double x0, double x1, double r, double factor)
    {
        double d = x1 - x0; if (Math.Abs(d) < 0.01) return;
        double stepLen = Math.Max(Surface.Col, Math.Max(Surface.Col * 2, r * 0.8) * 0.5);
        int n = Math.Max(1, (int)Math.Ceiling(Math.Abs(d) / stepLen));
        for (int k = 1; k <= n; k++) PressTrack(x0 + d * k / n, r, d, factor);
    }

    /// <summary>Scraping a window top: the snow is pushed along in front of the pointer and tumbles off the end / front.</summary>
    void Scrape(double px)
    {
        if (!Surfaces.TryGetValue(scrapeId, out var c) || c.Depth.Length < 3) { hold = Hold.None; return; }
        int n = c.Depth.Length;
        double x0 = scrapeX, x1 = Math.Clamp(px, c.Rect.Left - Surface.Col * 6, c.Rect.Right + Surface.Col * 6);
        double dir = x1 >= x0 ? 1 : -1;
        int steps = Math.Min(400, (int)(Math.Abs(x1 - x0) / Surface.Col) + 1);
        double stepLen = (x1 - x0) / steps, x = x0;
        for (int s = 0; s < steps; s++)
        {
            x += stepLen;
            int i = (int)Math.Round((x - c.Rect.Left) / Surface.Col);
            double load = 0;
            for (int k = -1; k <= 1; k++) { int j = i + k; if (j >= 0 && j < n && c.Depth[j] > 0.35) { load += c.Depth[j] - 0.35; c.Depth[j] = 0.35; } }
            if (load < 0.01) continue;
            if (rng.NextDouble() < Math.Min(1, load * 0.25))
                for (int q = 0; q < 1 + (int)Math.Min(3, load * 0.3); q++)
                    Add(new Particle { Kind = Kind.Crumb, X = x + dir * (2 + rng.NextDouble() * 6), Y = c.Rect.Top - rng.NextDouble() * Math.Max(2, load), VX = dir * (60 + rng.NextDouble() * 170), VY = -30 - rng.NextDouble() * 140, Size = 0.8 + rng.NextDouble() * 1.1, Life = 3, Alive = true });
            foreach (var (k, wgt) in new[] { (2, 0.45), (3, 0.35), (4, 0.2) })
            {
                int j = i + (int)dir * k;
                if (j >= 0 && j < n) c.Depth[j] += load * wgt;
                else Spill(dir > 0 ? c.Rect.Right + 3 : c.Rect.Left - 3, c.Rect.Top - 2, load * wgt * Surface.Col, dir * 60);
            }
        }
        for (int j = 0; j < n; j++)
            if (c.Depth[j] > c.MaxDepth + 6) { double extra = c.Depth[j] - (c.MaxDepth + 6); c.Depth[j] -= extra; Spill(c.Rect.Left + j * Surface.Col, c.Rect.Top - c.Depth[j], extra * Surface.Col, dir * 25); }
        scrapeX = x1;
    }

    void Spill(double x, double y, double area, double vx)
    {
        spillAcc += area; int guardN = 0;
        while (spillAcc > 8 && guardN < 10)
        {
            double s = guardN % 4 == 0 ? 3.5 + rng.NextDouble() * 2.5 : 1.6 + rng.NextDouble() * 1.6;
            Add(new Particle { Kind = Kind.Crumb, X = x + (rng.NextDouble() - 0.5) * 8, Y = y + (rng.NextDouble() - 0.5) * 4, VX = vx + (rng.NextDouble() - 0.5) * 60, VY = -rng.NextDouble() * 45 + 5, Size = s * Scale, Life = 4, Alive = true });
            spillAcc -= s * s * 0.45; guardN++;
        }
        spillAcc = Math.Min(spillAcc, 60);
    }

    public void CrumbleBall(Ball b)
    {
        foreach (var o in Balls.Where(o => o.On == b.Id)) o.On = null;
        if (hold == Hold.Ball && heldBall == b.Id) hold = Hold.None;
        int n = Math.Min(26, 6 + (int)(b.R / Scale * 0.5));
        for (int k = 0; k < n; k++)
        {
            double a = rng.NextDouble() * Math.PI * 2, d = rng.NextDouble() * b.R * 0.8;
            Add(new Particle { Kind = Kind.Crumb, X = b.X + Math.Cos(a) * d, Y = b.Y + Math.Sin(a) * d, Size = (1.5 + rng.NextDouble() * (1.5 + b.R / Scale * 0.09)) * Scale,
                VX = Math.Cos(a) * (40 + rng.NextDouble() * 120), VY = -(Math.Abs(Math.Sin(a)) * (60 + rng.NextDouble() * 160) + 40), Life = 3, Alive = true });
        }
        // most of the ball's snow goes back where it lies
        if (Floor is { } f)
        {
            int c = f.ColAt(b.X), w = Math.Max(2, (int)(b.R / Surface.Col));
            for (int k = -w; k <= w; k++) { int j = c + k; if (j >= 0 && j < f.Depth.Length) f.Depth[j] = Math.Min(f.MaxDepth, f.Depth[j] + b.R * 0.35 * Math.Exp(-(double)(k * k) / (w * w))); }
        }
        Balls.Remove(b);
    }

    public void CrumbleSnowman(Snowman m)
    {
        double y = m.Y;
        foreach (var r in m.R)
        {
            var b = new Ball { Id = nextBall++, X = m.X, Y = y - r, R = r };
            Balls.Add(b); CrumbleBall(b); y -= r * 1.8;
        }
        if (heldMan == m) { hold = Hold.None; heldMan = null; }
        Snowmen.Remove(m);
    }

    // ---------- the world moving on its own ----------
    void StepWinter(double dt)
    {
        if (Season != Season.Winter)
        {
            if (Balls.Count > 0 || Snowmen.Count > 0) { foreach (var b in Balls.ToList()) CrumbleBall(b); foreach (var m in Snowmen.ToList()) CrumbleSnowman(m); }
            hold = Hold.None; Plow = PlowState.Idle; Garlands.Clear();
            return;
        }
        StepBalls(dt);
        StepGarlands(dt);
        StepPlow(dt);
    }

    bool Rests(Ball o, int onId)
    {
        int? cur = o.On; int n = 0;
        while (cur != null && n < 6) { if (cur == onId) return true; var k = Balls.FirstOrDefault(b => b.Id == cur); if (k == null) return false; cur = k.On; n++; }
        return false;
    }

    void StepBalls(double dt)
    {
        var smash = new List<Ball>(); var hitMen = new HashSet<Snowman>();
        int heldId = hold == Hold.Ball ? heldBall : -1;
        foreach (var b in Balls)
        {
            b.Age += dt;
            if (b.Id == heldId) continue;
            if (b.On is int on && Balls.FirstOrDefault(o => o.Id == on) is { } bs)
            { b.X = bs.X; b.Y = bs.Y - bs.R - b.R * 0.82; b.VX = b.VY = 0; continue; }
            b.On = null;
            b.VY += 1500 * Scale * dt;
            double ox = b.X;
            b.X += b.VX * dt; b.Y += b.VY * dt;
            double g = SnowTop(b.X);
            if (b.Y + b.R * 0.9 >= g)
            {
                if (b.VY > 1100 * Scale) { smash.Add(b); continue; }           // thrown high, it smashes when it comes down hard
                b.Y = g - b.R * 0.9;
                b.VY = b.VY > 120 * Scale ? -b.VY * 0.18 : 0;
                double s = Math.Max(5 * Scale, b.R * 0.6);
                double slope = (SnowTop(b.X - s) - SnowTop(b.X + s)) / (2 * s);       // >0: the snow rises to the right
                if (Math.Abs(b.VX) < 15 * Scale && Math.Abs(slope) < 0.3) b.VX = 0;      // a sticky snowball stays put on a gentle slope
                else { b.VX -= slope * 700 * Scale * dt; b.VX *= Math.Pow(0.18, dt); }
                b.Ang += b.VX * dt / Math.Max(b.R, 1);
                if (Math.Abs(b.VX) > 25 * Scale)
                {
                    double depth = Math.Max(SnowUnder(b.X), SnowUnder(b.X + (b.VX >= 0 ? 1 : -1) * (Math.Max(Surface.Col * 2, b.R * 0.8) + Surface.Col)));
                    if (depth > 1)
                    {
                        double gain = Math.Abs(b.VX) * dt * Math.Min(depth, b.R) * 0.25;
                        b.R = Math.Min(MaxBall, Math.Sqrt(b.R * b.R + gain / Math.PI));
                        PressPath(ox, b.X, b.R, 0.35);
                    }
                }
            }
            if (Math.Abs(b.VX) < 350 * Scale)
            {
                if (b.X < b.R) { b.X = b.R; b.VX = Math.Abs(b.VX) * 0.3; }
                if (b.X > Width - b.R) { b.X = Width - b.R; b.VX = -Math.Abs(b.VX) * 0.3; }
            }
            // a flying snowball hits a snowman → both burst
            if (Math.Sqrt(b.VX * b.VX + b.VY * b.VY) > 320 * Scale && b.Age > 0.12)
            {
                var man = Snowmen.FirstOrDefault(m => !m.Held && Math.Abs(b.X - m.X) < m.R[0] + b.R * 0.8 && b.Y < m.Y + b.R && b.Y > m.Y - (m.R[0] + m.R[1] + m.R[2]) * 1.9 - b.R * 0.5);
                if (man != null) { hitMen.Add(man); smash.Add(b); continue; }
            }
            // lands on another ball → stack; otherwise they push apart
            foreach (var o in Balls)
            {
                if (o == b || o.Id == heldId || Rests(o, b.Id)) continue;
                double d = Math.Sqrt((b.X - o.X) * (b.X - o.X) + (b.Y - o.Y) * (b.Y - o.Y));
                if (d >= b.R + o.R * 0.95) continue;
                if (b.Y < o.Y - o.R * 0.4 && Math.Abs(b.X - o.X) < o.R * 0.7) { b.On = o.Id; b.X = o.X; b.Y = o.Y - o.R - b.R * 0.82; b.VX = b.VY = 0; break; }
                if (o.Y < b.Y - b.R * 0.4 && Math.Abs(o.X - b.X) < b.R * 0.7) continue;
                double push = (b.R + o.R * 0.95 - d) * (b.X < o.X ? -1 : 1);
                b.X += push; b.VX = -b.VX * 0.2;
            }
        }
        foreach (var b in smash) if (Balls.Contains(b)) CrumbleBall(b);
        foreach (var m in hitMen) if (Snowmen.Contains(m)) CrumbleSnowman(m);
        Balls.RemoveAll(b => b.Id != heldId && b.On == null && (b.X < -b.R * 3 || b.X > Width + b.R * 3));
        BuildSnowmen();
        foreach (var m in Snowmen)
        {
            if (m.Held) continue;
            m.X += m.VX * dt;
            double g = SnowTop(m.X) + 2 * Scale;
            if (m.Y < g || m.VY < 0) { m.VY += 1500 * Scale * dt; m.Y = Math.Min(g, m.Y + m.VY * dt); } else { m.Y = g; m.VY = 0; }
            if (m.Y >= g - 0.5) { m.VX *= Math.Pow(0.04, dt); m.Spin *= Math.Pow(0.02, dt); }
            if (Math.Abs(m.VX) < 8) m.VX = 0;
            m.Wave = Math.Max(0, m.Wave - dt);
        }
        if (hold != Hold.Man) Snowmen.RemoveAll(m => m.X < -120 * Scale || m.X > Width + 120 * Scale);
    }

    /// <summary>Three stacked balls → a snowman (any order: the biggest goes to the bottom, each next ≤ 90 %).</summary>
    void BuildSnowmen()
    {
        foreach (var top in Balls)
        {
            if (top.On == null) continue;
            var mid = Balls.FirstOrDefault(o => o.Id == top.On); if (mid?.On == null) continue;
            var bot = Balls.FirstOrDefault(o => o.Id == mid.On); if (bot == null || bot.On != null) continue;
            if (hold == Hold.Ball && (heldBall == top.Id || heldBall == mid.Id || heldBall == bot.Id)) continue;
            var rs = new[] { bot.R, mid.R, top.R }.OrderByDescending(r => r).ToArray();
            double r0 = rs[0], r1 = Math.Min(rs[1], r0 * 0.9), r2 = Math.Min(rs[2], r1 * 0.9);
            if (r2 < 5 * Scale) continue;
            Snowmen.Add(new Snowman { X = bot.X, Y = bot.Y + bot.R * 0.9, R = new[] { r0, r1, r2 }, Born = Time, Wave = 2.6, LastWave = Time, Hat = Snowmen.Count % 2 });
            Balls.Remove(top); Balls.Remove(mid); Balls.Remove(bot);
            return;
        }
    }

    /// <summary>For demos and tests: put a ready snowman on the snow.</summary>
    public Snowman AddSnowman(double x, double r0)
    {
        var m = new Snowman { X = x, Y = SnowTop(x) + 2 * Scale, R = new[] { r0, r0 * 0.78, r0 * 0.6 }, Born = Time, Wave = 2.6, LastWave = Time, Hat = Snowmen.Count % 2 };
        Snowmen.Add(m); return m;
    }

    void StepGarlands(double dt)
    {
        var keep = new HashSet<long>();
        if (GarlandMode != 0)
            foreach (var s in Surfaces.Values.Where(s => s.Id >= 0 && s.Rect.Width > 220 * Scale).Take(3))
            {
                keep.Add(s.Id);
                var g = Garlands.FirstOrDefault(o => o.Id == s.Id);
                if (g == null) { g = new Garland { Id = s.Id, Amp = 7 }; Garlands.Add(g); }      // a new window: the garland swings in
                g.X0 = s.Rect.Left + 78 * Scale; g.X1 = s.Rect.Right - 150 * Scale; g.Top = s.Rect.Top + 2;   // keep the window buttons readable
                if (g.X1 - g.X0 < 80 * Scale) g.X1 = s.Rect.Right - 12 * Scale;
            }
        Garlands.RemoveAll(g => !keep.Contains(g.Id));
        foreach (var g in Garlands) { g.Amp *= Math.Exp(-1.6 * dt); g.Ph += dt * 9.5; }
    }

    // ---------- the snowplow ----------
    public void CallPlow() { if (Season == Season.Winter && Plow == PlowState.Idle && Floor != null) StartPlow(); }
    void StartPlow()
    {
        var f = Floor!; PlowDir = plowRuns++ % 2 == 0 ? 1 : -1; Plow = PlowState.Driving; PlowLoad = 0;
        PlowX = PlowDir > 0 ? f.Rect.Left - 70 * Scale : f.Rect.Right + 70 * Scale;
    }
    /// <summary>The heap in front of the blade: its size is the snow actually gathered (a little compacted).</summary>
    public double PlowPileH => Math.Sqrt(Math.Max(0, PlowLoad * Surface.Col * 0.75) / 1.1);
    public double PlowFront => PlowX + PlowDir * PlowW * PlowBlade;

    void StepPlow(double dt)
    {
        PlowCooldown = Math.Max(0, PlowCooldown - dt);
        var f = Floor; if (f == null) { Plow = PlowState.Idle; return; }
        double x0 = f.Rect.Left, x1 = f.Rect.Right, speed = 46 * Scale, dir = PlowDir;
        switch (Plow)
        {
            case PlowState.Idle:
            {
                if (!PlowEnabled || PlowCooldown > 0) return;
                double sum = 0, mxd = 0; int n = 0;
                for (int c = 0; c < f.Depth.Length; c += 4) { sum += f.Depth[c]; mxd = Math.Max(mxd, f.Depth[c]); n++; }
                if (n > 0 && (sum / n > 18 * Scale || mxd > 34 * Scale)) StartPlow();
                return;
            }
            case PlowState.Driving:
            {
                PlowX += dir * speed * dt; PlowWheel += speed * dt;
                double front = PlowFront, pileFront = front + dir * PlowPileH * 1.8;
                // a snowman in the way: stop, wait, back off (the heap stays where it is)
                if (Snowmen.Any(m => { double e = m.X - dir * m.R[0]; return dir * (e - front) > -2 && dir * (e - pileFront) < 14 * Scale; }))
                { Plow = PlowState.Waiting; PlowWait = 2.5; LeaveHeap(front); return; }
                // a snowball in the way is scooped into the heap
                foreach (var b in Balls.ToList())
                    if (b.On == null && dir * (b.X - dir * b.R - pileFront) < 2 && dir * (b.X - front) > -10 && b.Y + b.R > SnowTop(b.X) - 8 * Scale)
                    {
                        PlowLoad += Math.PI * b.R * b.R / (Surface.Col * 0.75);
                        foreach (var o in Balls.Where(o => o.On == b.Id)) o.On = null;
                        if (hold == Hold.Ball && heldBall == b.Id) hold = Hold.None;
                        Balls.Remove(b);
                    }
                double end = dir > 0 ? x1 : x0;
                if (front > x0 && front < x1)
                {
                    // everything the blade passes over is gathered — the heap keeps growing with it
                    int c0 = f.ColAt(Math.Min(front, front - dir * 6 * Scale)), c1 = f.ColAt(Math.Max(front, front - dir * 6 * Scale));
                    for (int c = c0; c <= c1; c++) if (f.Depth[c] > 1.2) { PlowLoad += f.Depth[c] - 1.2; f.Depth[c] = 1.2; }
                    double h = PlowPileH;
                    if (h > PileMax)
                    {
                        double keep = PileMax * PileMax * 1.1 / (Surface.Col * 0.75), extra = PlowLoad - keep;
                        PlowLoad = keep;
                        if (rng.NextDouble() < Math.Min(1, extra * 0.08))
                            Add(new Particle { Kind = Kind.Crumb, X = front + dir * h * 0.5, Y = Base - h * 0.9, Size = (5 + rng.NextDouble() * 5) * Scale, VX = dir * (speed + 10 + rng.NextDouble() * 50), VY = -10 - rng.NextDouble() * 40, Life = 3, Alive = true });
                    }
                }
                if (dir * (front + dir * PlowPileH * 0.9 - end) >= -1)
                {
                    // past the end the heap tips over the edge — lumps that fall and are gone
                    int n = 0;
                    while (PlowLoad > 25 && n < 30)
                    {
                        Add(new Particle { Kind = Kind.Crumb, X = end + dir * (rng.NextDouble() * 14 - 4), Y = Base - rng.NextDouble() * Math.Max(8, PlowPileH), Size = (4 + rng.NextDouble() * 6) * Scale, VX = dir * (30 + rng.NextDouble() * 80), VY = -rng.NextDouble() * 60, Life = 3, Alive = true });
                        PlowLoad -= 40; n++;
                    }
                    PlowLoad = Math.Max(0, PlowLoad);
                }
                if ((dir > 0 && PlowX > Width + 90 * Scale) || (dir < 0 && PlowX < -90 * Scale)) { Plow = PlowState.Idle; PlowCooldown = 60; PlowLoad = 0; }
                break;
            }
            case PlowState.Waiting:
                PlowWait -= dt; if (PlowWait <= 0) Plow = PlowState.Reversing; break;
            case PlowState.Reversing:
                PlowX -= dir * speed * 1.2 * dt; PlowWheel -= speed * 1.2 * dt;
                if ((dir > 0 && PlowX < x0 - 90 * Scale) || (dir < 0 && PlowX > x1 + 90 * Scale)) { Plow = PlowState.Idle; PlowCooldown = 45; }
                break;
        }
    }

    void LeaveHeap(double x)
    {
        var f = Floor!; double h = PlowPileH; PlowLoad = 0;
        if (h <= 1) return;
        double w = Math.Max(6, h * 1.8);
        for (int c = f.ColAt(Math.Min(x, x + PlowDir * w)); c < f.Depth.Length && f.Rect.Left + c * Surface.Col < Math.Max(x, x + PlowDir * w); c++)
        {
            double u = Math.Abs(f.Rect.Left + c * Surface.Col - x) / w;
            f.Depth[c] = Math.Min(f.MaxDepth, Math.Max(f.Depth[c], 1.2 + h * (1 - Math.Pow(u, 1.6))));
        }
    }
}
