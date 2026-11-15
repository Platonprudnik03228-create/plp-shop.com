namespace PLPSeasons.Core;

public enum Kind : byte { Flake, Drop, Leaf, Petal, Crumb, Splash }

public struct Particle
{
    public double X, Y, VX, VY, Size, Angle, Spin, Phase, Life;
    public Kind Kind;
    public byte Tint;      // leaf colour index
    public bool Alive;
}

/// <summary>
/// A surface snow can lie on: the top edge of an app window, the taskbar, or the bottom of the screen. Depth is kept
/// per 3-pixel column, so the snow follows the real outline; when a window moves or closes, its snow falls off.
/// </summary>
public sealed class Surface
{
    public const int Col = 3;
    public long Id;           // window handle or a fixed id
    public RectI Rect;        // top edge = Rect.Top, from Left to Right
    public double[] Depth;
    public bool Seen;         // still present in this update
    public double Corner;     // rounded window corners (Windows 11: 8 px)
    public Surface(long id, RectI r, double corner) { Id = id; Rect = r; Corner = corner; Depth = new double[Math.Max(1, r.Width / Col + 1)]; }
    public double MaxDepth => Rect.Height > 0 ? Math.Min(60, Rect.Width * 0.06 + 6) : 60;
    public int ColAt(double x) => (int)Math.Clamp((x - Rect.Left) / Col, 0, Depth.Length - 1);
    public double TopAt(double x) => Rect.Top - Depth[ColAt(x)];
    /// <summary>The corner curve: near the ends the snow has to be thinner so it does not hang over the rounded edge.</summary>
    public double Limit(int c)
    {
        double x = c * Col, fromEdge = Math.Min(x, Rect.Width - x);
        if (fromEdge >= Corner) return MaxDepth;
        double t = fromEdge / Math.Max(1, Corner);
        return MaxDepth * Math.Sqrt(Math.Max(0, t)) * 0.9;
    }
}

/// <summary>Everything that moves: particles of the season, snow on surfaces, melting, falling clumps.</summary>
public sealed class Simulation
{
    public readonly List<Particle> P = new();
    public readonly Dictionary<long, Surface> Surfaces = new();
    public Season Season = Season.Winter;
    public double Intensity = 0.6, Wind = 0.25;
    public double Width, Height;
    public double Time;
    public double Flash;                    // lightning 0..1
    public int SnowOnWindowsCap = 200;
    readonly Random rng;
    double spawnAcc, nextFlash = 6;

    public Simulation(double w, double h, int seed = 1) { Width = w; Height = h; rng = new Random(seed); }

    public int Target => (int)(Season switch
    {
        Season.Winter => 650, Season.Rain => 420, Season.Storm => 650, Season.Autumn => 46, Season.Spring => 70, _ => 18,
    } * Math.Clamp(Intensity, 0.05, 2) * Math.Clamp(Width * Height / (1920.0 * 1080), 0.5, 3));

    /// <summary>Replace the surfaces with the current windows (top first). Snow on a window that moved or disappeared falls off.</summary>
    public void SetSurfaces(IEnumerable<(long id, RectI rect, double corner)> list)
    {
        foreach (var s in Surfaces.Values) s.Seen = false;
        foreach (var (id, r, corner) in list)
        {
            if (Surfaces.TryGetValue(id, out var s))
            {
                if (s.Rect.Top != r.Top)
                {
                    ShedAll(s, moved: true);                       // the window moved: its snow falls off
                    Surfaces[id] = new Surface(id, r, corner) { Seen = true };
                    continue;
                }
                if (s.Rect.Left != r.Left || s.Rect.Right != r.Right)
                {
                    // same window, another window now covers a different part of its edge: keep the snow where it lies
                    var n = new Surface(id, r, corner) { Seen = true };
                    for (int c = 0; c < n.Depth.Length; c++)
                    {
                        double x = r.Left + c * Surface.Col;
                        if (x >= s.Rect.Left && x < s.Rect.Right) n.Depth[c] = Math.Min(n.Limit(c), s.Depth[s.ColAt(x)]);
                    }
                    Surfaces[id] = n; continue;
                }
                s.Seen = true;
            }
            else Surfaces[id] = new Surface(id, r, corner) { Seen = true };
        }
        foreach (var gone in Surfaces.Values.Where(s => !s.Seen).ToList()) { ShedAll(gone, moved: false); Surfaces.Remove(gone.Id); }
    }

    /// <summary>The snow of a surface breaks into crumbs that fall (white, no blue tint).</summary>
    public void ShedAll(Surface s, bool moved)
    {
        for (int c = 0; c < s.Depth.Length; c += 2)
        {
            double d = s.Depth[c]; if (d < 1.2) continue;
            int n = Math.Min(3, (int)(d / 4) + 1);
            for (int k = 0; k < n; k++)
                Add(new Particle { Kind = Kind.Crumb, X = s.Rect.Left + c * Surface.Col + rng.NextDouble() * 3, Y = s.Rect.Top - rng.NextDouble() * d,
                    VX = (rng.NextDouble() - 0.5) * (moved ? 120 : 50), VY = rng.NextDouble() * -40, Size = 1.5 + rng.NextDouble() * Math.Min(5, d * 0.35), Alive = true, Life = 4 });
        }
    }

    static double Frac(double v) => v - Math.Floor(v);

    void Add(Particle p) { if (P.Count < 6000) P.Add(p); }

    Particle Spawn(bool anywhere)
    {
        var p = new Particle { Alive = true, X = rng.NextDouble() * (Width + 200) - 100, Y = anywhere ? rng.NextDouble() * Height : -20 - rng.NextDouble() * (Season is Season.Autumn or Season.Spring or Season.Summer ? Height * 0.6 : 140), Phase = rng.NextDouble() * 6.28 };
        switch (Season)
        {
            case Season.Winter:
                p.Kind = Kind.Flake; double depth = rng.NextDouble();
                p.Size = 1.6 + depth * depth * 5.4; p.VY = 34 + p.Size * 12; p.VX = 0; p.Spin = rng.NextDouble() * 2 - 1; break;
            case Season.Rain: case Season.Storm:
                p.Kind = Kind.Drop; p.Size = 0.8 + rng.NextDouble() * 1.2; p.VY = 900 + rng.NextDouble() * 500 + (Season == Season.Storm ? 300 : 0); break;
            case Season.Autumn:
                p.Kind = Kind.Leaf; p.Size = 10 + rng.NextDouble() * 12; p.VY = 50 + rng.NextDouble() * 40; p.Spin = (rng.NextDouble() - 0.5) * 4; p.Tint = (byte)rng.Next(4); break;
            case Season.Spring:
                p.Kind = Kind.Petal; p.Size = 6 + rng.NextDouble() * 6; p.VY = 35 + rng.NextDouble() * 30; p.Spin = (rng.NextDouble() - 0.5) * 3; break;
            default:
                p.Kind = Kind.Petal; p.Size = 2 + rng.NextDouble() * 2; p.VY = 8 + rng.NextDouble() * 10; p.Tint = 9; break;   // summer: floating pollen
        }
        return p;
    }

    Season lastSeason = (Season)(-1);
    public void Step(double dt)
    {
        dt = Math.Clamp(dt, 0, 0.05);
        if (Season != lastSeason)
        {
            bool first = (int)lastSeason == -1;
            lastSeason = Season;
            P.RemoveAll(x => x.Kind is not Kind.Crumb);
            int n = first ? Target : Target / 2;
            for (int i = 0; i < n; i++) Add(Spawn(true));
        }
        Time += dt;
        int alive = P.Count(x => x.Alive && x.Kind is not Kind.Crumb and not Kind.Splash);
        int target = Target;
        if (alive < target)
        {
            spawnAcc += (target - alive) * dt * 1.5 + (P.Count == 0 ? target : 0);
            while (spawnAcc >= 1) { Add(Spawn(P.Count < target / 3 && Time < 1)); spawnAcc -= 1; }
        }
        double wind = Wind * (Season is Season.Rain or Season.Storm ? 260 : 60) * (1 + 0.35 * Math.Sin(Time * 0.3));
        if (Season == Season.Storm)
        {
            nextFlash -= dt;
            if (nextFlash <= 0) { Flash = 1; nextFlash = 5 + rng.NextDouble() * 12; }
            Flash = Math.Max(0, Flash - dt * 2.5);
        }
        else Flash = 0;

        var span = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(P);
        for (int i = 0; i < span.Length; i++)
        {
            ref var p = ref span[i];
            if (!p.Alive) continue;
            switch (p.Kind)
            {
                case Kind.Flake:
                    p.X += (wind * (0.3 + p.Size / 6) + Math.Sin(Time * 1.3 + p.Phase) * 12) * dt; p.Y += p.VY * dt; p.Angle += p.Spin * dt; break;
                case Kind.Drop:
                    p.X += wind * dt; p.Y += p.VY * dt; break;
                case Kind.Leaf: case Kind.Petal:
                    p.X += (wind + Math.Sin(Time * 1.1 + p.Phase) * 40) * dt; p.Y += (p.VY + Math.Cos(Time * 2.1 + p.Phase) * 18) * dt; p.Angle += p.Spin * dt; break;
                case Kind.Crumb:
                    p.VY += 900 * dt; p.X += p.VX * dt; p.Y += p.VY * dt; p.Life -= dt; if (p.Life <= 0 || p.Y > Height + 20) p.Alive = false; continue;
                case Kind.Splash:
                    p.Life -= dt; if (p.Life <= 0) p.Alive = false; continue;
            }
            if (p.Y > Height + 30 || p.X < -150 || p.X > Width + 150) { p = Spawn(false); continue; }
            // landing
            if (p.Kind is Kind.Flake or Kind.Drop)
            {
                var s = SurfaceUnder(p.X, p.Y);
                // most flakes and drops fly past a window in front of it (the overlay is in front of the windows);
                // about one in six settles on its top edge. Everything settles on the taskbar / the bottom of the screen.
                if (s != null && s.Id >= 0 && Frac(p.Phase * 7.31) > 0.17) s = null;
                if (s != null)
                {
                    if (p.Kind == Kind.Flake && Season == Season.Winter) Deposit(s, p.X, p.Size * (s.Id >= 0 ? 1.4 : 0.5));
                    else if (p.Kind == Kind.Drop && rng.NextDouble() < 0.35) Add(new Particle { Kind = Kind.Splash, X = p.X, Y = s.TopAt(p.X), Size = 3 + rng.NextDouble() * 3, Life = 0.18, Alive = true });
                    p = Spawn(false);
                }
            }
        }
        if ((int)(Time * 4) != (int)((Time - dt) * 4)) P.RemoveAll(x => !x.Alive);
        // melt outside winter; settle so the snow has smooth slopes
        foreach (var s in Surfaces.Values)
        {
            if (Season != Season.Winter) for (int c = 0; c < s.Depth.Length; c++) s.Depth[c] = Math.Max(0, s.Depth[c] - dt * (Season is Season.Rain or Season.Storm ? 4 : 1.5));
            Relax(s);
        }
    }

    /// <summary>The highest surface whose snow top a falling particle at (x,y) has just reached.</summary>
    public Surface? SurfaceUnder(double x, double y)
    {
        foreach (var s in Surfaces.Values)
        {
            if (x < s.Rect.Left || x >= s.Rect.Right) continue;
            double top = s.TopAt(x);
            if (y >= top && y <= s.Rect.Top + 6) return s;
        }
        return null;
    }

    public void Deposit(Surface s, double x, double amount)
    {
        int c = s.ColAt(x);
        for (int k = -2; k <= 2; k++)
        {
            int j = c + k; if (j < 0 || j >= s.Depth.Length) continue;
            double add = amount * (1 - Math.Abs(k) * 0.3);
            s.Depth[j] = Math.Min(s.Limit(j), s.Depth[j] + add);
        }
    }

    static void Relax(Surface s)
    {
        var d = s.Depth;
        for (int c = 1; c < d.Length; c++)
        {
            double diff = d[c] - d[c - 1];
            if (Math.Abs(diff) > 2.2) { double m = (Math.Abs(diff) - 2.2) * 0.25 * Math.Sign(diff); d[c] -= m; d[c - 1] += m; }
        }
        for (int c = 0; c < d.Length; c++) d[c] = Math.Min(d[c], s.Limit(c));
    }

    /// <summary>The parts of each window's top edge that are not covered by a window above it (list is top-most first).</summary>
    public static List<(long id, RectI rect)> VisibleTops(IReadOnlyList<(long id, RectI rect)> zOrdered)
    {
        var res = new List<(long, RectI)>();
        for (int i = 0; i < zOrdered.Count; i++)
        {
            var (id, r) = zOrdered[i];
            var spans = new List<(int a, int b)> { (r.Left, r.Right) };
            for (int j = 0; j < i && spans.Count > 0; j++)
            {
                var o = zOrdered[j].rect;
                if (r.Top < o.Top - 2 || r.Top >= o.Bottom) continue;          // the window above does not cover this edge
                var next = new List<(int, int)>();
                foreach (var (a, b) in spans)
                {
                    if (o.Right <= a || o.Left >= b) { next.Add((a, b)); continue; }
                    if (o.Left > a) next.Add((a, o.Left));
                    if (o.Right < b) next.Add((o.Right, b));
                }
                spans = next;
            }
            // the longest visible piece carries the snow (keeps it simple and stable)
            if (spans.Count == 0) continue;
            var best = spans.OrderByDescending(s => s.b - s.a).First();
            if (best.b - best.a >= 60) res.Add((id, new RectI(best.a, r.Top, best.b, r.Bottom)));
        }
        return res;
    }
}
