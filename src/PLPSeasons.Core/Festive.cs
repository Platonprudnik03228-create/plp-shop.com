namespace PLPSeasons.Core;

/// <summary>0.5.4 (owner: "something fun and New-Year-ish"), the same as the Mac app's 0.4.91 (Festive.swift):
/// snowballs that splat on windows, a snowman raised by a double-click (it looks at the pointer; a snowball knocks its
/// head off), fireworks with a New Year countdown, icicles along the top of the screen, Santa's sleigh dropping gifts,
/// and grooves the pointer leaves in the drift. Physical pixels, y down.</summary>
public sealed class Splat { public RectI Win; public double X, Y, R, Age, Slide, Dir, Speed; public int Seed; }
public sealed class Spark { public double X, Y, VX, VY, Age, Life, Size, Grav, Drag, Rot, Spin; public byte R, G, B; public int Kind; }
public sealed class Rocket { public double X, Y, VX, VY; public int C, Pattern; }
public sealed class Icicle { public double X, Len, Max, W, Y, VY, Rot, Wob, Regrow; public bool Falling; }
public sealed class Sleigh { public double X, Y0, Dir, Speed, Age, AutoAt; public int Gifts; }
public sealed class Gift { public double X, Y, VX, VY, Age, Rot; public int C; public bool Landed; }

public sealed partial class Simulation
{
    public readonly List<Splat> Splats = new();
    public readonly List<Spark> Sparks = new();
    public readonly List<Rocket> Rockets = new();
    public readonly List<Icicle> Icicles = new();
    public readonly List<Gift> Gifts = new();
    public Sleigh? Sleigh;
    /// <summary>0.5.6 (owner): a New Year wish comes out of every gift. Russian / English, chosen by <see cref="Russian"/>.</summary>
    public readonly List<(double X, double Y, double Age, string Text)> Wishes = new();
    public static bool Russian = true;
    int lastWish = -1;
    static readonly (string ru, string en)[] WishList = {
        ("С наступающим Новым годом!", "Happy New Year to you!"), ("Одевайся теплее — шапку не забудь", "Dress warm — don't forget your hat"),
        ("Пусть в новом году всё получится", "May everything work out this year"), ("Загадай желание — сбудется!", "Make a wish — it will come true!"),
        ("Больше тепла, меньше дедлайнов", "More warmth, fewer deadlines"), ("Выпей какао и отдохни", "Have a cocoa and take a break"),
        ("Позвони близким — им будет приятно", "Call someone you love today"), ("Слепи снеговика — это полезно!", "Build a snowman — doctor's orders!"),
        ("Счастья, здоровья и денег побольше", "Health, happiness and a bit of luck"), ("Мандарины — лучший витамин C", "Tangerines: the best vitamin C"),
        ("Пусть снег будет только за окном", "May the snow stay outside the window"), ("Ты молодец. Правда.", "You're doing great. Really."),
        ("Выспись в праздники — заслужил", "Sleep in this holiday — you earned it"), ("Новый год — новые идеи!", "New year, new ideas!") };
    public double Countdown = -1, Greet, Show, FlashX, FlashY, FlashA; public byte FlashR = 255, FlashG = 255, FlashB = 255;
    double nextSleigh = 150, nextRocket, pendingShow, clockAcc; int newYearDone;
    readonly List<(double at, double x, double r)> drops = new();
    double trailX = double.NaN;

    public static readonly (byte r, byte g, byte b)[] FireColors = { (255, 72, 82), (90, 255, 128), (102, 158, 255), (255, 214, 90), (255, 140, 50), (230, 115, 255), (255, 255, 255) };
    double R(double a, double b) => a + (b - a) * rng.NextDouble();

    void StepFestive(double dt)
    {
        StepDrops();
        StepSplats(dt);
        StepIcicles(dt);
        StepSleigh(dt);
        StepGifts(dt);
        StepFireworks(dt);
        StepSparks(dt);
        for (int i = Wishes.Count - 1; i >= 0; i--) { var w = Wishes[i]; w.Age += dt; if (w.Age > 7) Wishes.RemoveAt(i); else Wishes[i] = w; }
        clockAcc += dt;
        if (clockAcc > 1) { clockAcc = 0; CheckNewYear(DateTime.Now); }
    }

    // ---------- snowman by double-click ----------
    /// <summary>Double-click: a snowman or ball falls apart; on the drift a snowman is raised there.</summary>
    public bool DoubleClick(double x, double y)
    {
        var sm = SnowmanAt(x, y); if (sm != null) { CrumbleSnowman(sm); hold = Hold.None; return true; }
        var bb = NearestBall(x, y);
        if (bb != null)
        {
            if (bb.Age < 0.8 && bb.R <= 7.6 * Scale && !bb.Face)
            {
                // the first click scooped a little ball: its snow goes back and a snowman rises here
                if (Floor is { } f) { int c = f.ColAt(bb.X); for (int k = -3; k <= 3; k++) { int j = c + k; if (j >= 0 && j < f.Depth.Length) f.Depth[j] += 4.5 * Scale * Math.Exp(-k * k / 4.5); } }
                Balls.Remove(bb); hold = Hold.None;
                RaiseSnowman(x); return true;
            }
            CrumbleBall(bb); hold = Hold.None; return true;
        }
        if (Season == Season.Winter && y > SnowTop(x) - 40 * Scale) { RaiseSnowman(x); return true; }
        return false;
    }
    public void RaiseSnowman(double x)
    {
        double xx = Math.Clamp(x, 40 * Scale, Width - 40 * Scale);
        drops.Add((Time, xx, 21 * Scale)); drops.Add((Time + 0.42, xx, 15 * Scale)); drops.Add((Time + 0.84, xx, 10.5 * Scale));
    }
    void StepDrops()
    {
        for (int i = drops.Count - 1; i >= 0; i--)
        {
            var d = drops[i]; if (d.at > Time) continue;
            drops.RemoveAt(i);
            double y = SnowTop(d.x) - 170 * Scale;
            Balls.Add(new Ball { Id = nextBall++, X = d.x, Y = y, R = d.r, VY = 150 * Scale, Age = 1 });
            for (int k = 0; k < 8; k++) AddSpark(d.x + R(-d.r, d.r), y + R(-d.r, d.r), R(-60, 60), R(-60, 30), R(0.4, 0.8), R(2, 4), (255, 255, 255), 5, 60, 0.2);
        }
    }

    // ---------- snowballs splat on windows ----------
    bool SplatCheck(Ball b)
    {
        if (b.Fly < 0.26 || b.Fly >= 0.31 || Math.Sqrt(b.VX * b.VX + b.VY * b.VY) < 260 * Scale) return false;
        foreach (var w in Windows)
        {
            if (!w.Contains(b.X, b.Y)) continue;
            // each splat is drawn anew: a packed clump and a spray thrown on along the flight (0.5.5)
            Splats.Add(new Splat { Win = w, X = b.X, Y = b.Y, R = Math.Max(30 * Scale, b.R * 3.4), Seed = rng.Next(), Dir = Math.Atan2(b.VY, b.VX), Speed = Math.Min(1, Math.Sqrt(b.VX * b.VX + b.VY * b.VY) / (900 * Scale)) });
            for (int k = 0; k < Math.Min(14, 5 + (int)(b.R / Scale)); k++)
            {
                double a = R(0, Math.PI * 2);
                AddSpark(b.X, b.Y, Math.Cos(a) * R(60, 200), -(Math.Sin(a) * R(60, 200) + 40), R(0.5, 0.9), R(2, 4.5), (255, 255, 255), 5, 700, 0.3);
            }
            if (Splats.Count > 24) Splats.RemoveAt(0);
            return true;
        }
        return false;
    }
    void StepSplats(double dt)
    {
        Splats.RemoveAll(s => s.Age > 14 || !Windows.Contains(s.Win));
        foreach (var s in Splats)
        {
            // it sticks; after a while the heavy clump creeps down a little, slower and slower; now and then a crumb drops off
            s.Age += dt; if (s.Age > 2.5) s.Slide += dt * Math.Max(0, 5 - (s.Age - 2.5) * 0.45) * Scale;
            if (s.Age < 9 && rng.NextDouble() < dt * (s.Age < 1 ? 3 : 0.7))
                Add(new Particle { Kind = Kind.Crumb, X = s.X + R(-0.3, 0.3) * s.R, Y = s.Y + s.Slide + s.R * 0.28, VX = R(-10, 10), VY = R(-5, 10), Size = R(0.9, 1.8) * Scale, Life = 3, Alive = true });
        }
    }

    // ---------- grooves in the drift ----------
    void Trail(double x, double y)
    {
        var f = Floor;
        if (Season != Season.Winter || hold != Hold.None || f == null) { trailX = double.NaN; return; }
        double d = SnowUnder(x), b = Base;
        if (d > 3 * Scale && y < b - 1 && y > b - d + 1)
        {
            if (!double.IsNaN(trailX) && Math.Abs(trailX - x) > 0.5) PressPath(trailX, x, 8 * Scale, 0.8);
            trailX = x;
        }
        else trailX = double.NaN;
    }

    // ---------- icicles along the top of the screen ----------
    void StepIcicles(double dt)
    {
        if (Season != Season.Winter) { Icicles.Clear(); return; }
        if (Icicles.Count == 0)
        {
            double x = R(20, 60) * Scale;
            while (x < Width - 20 * Scale)
            {
                bool big = rng.NextDouble() < 0.25;
                double mx = (big ? R(28, 44) : R(10, 24)) * Scale;
                Icicles.Add(new Icicle { X = x, Max = mx, Len = mx * R(0.5, 0.8), W = (big ? R(7, 10) : R(4.5, 6.5)) * Scale });
                x += R(22, 70) * Scale;
            }
        }
        double sp = Math.Sqrt(mvx * mvx + mvy * mvy);
        foreach (var c in Icicles)
        {
            if (c.Falling)
            {
                c.VY += 1500 * Scale * dt; c.Y += c.VY * dt; c.Rot += dt * (c.X % 2 > 1 ? 1.2 : -1.2);
                double tip = c.Y + c.Len / 2;
                bool hit = tip >= SnowTop(c.X);
                if (!hit) foreach (var s in Surfaces.Values) if (s.Id >= 0 && c.X > s.Rect.Left && c.X < s.Rect.Right && tip >= s.Rect.Top && tip < s.Rect.Top + 30 * Scale) { hit = true; break; }
                if (hit)
                {
                    for (int k = 0; k < (int)(6 + c.Len / Scale * 0.5); k++) AddSpark(c.X + R(-2, 2), tip - 2, R(-140, 140), -R(40, 240), R(0.4, 0.9), R(1.5, 3.5), (216, 242, 255), 3, 900, 0.4, R(-12, 12));
                    c.Falling = false; c.Len = 0; c.Regrow = 25;
                }
            }
            else
            {
                if (c.Regrow > 0) c.Regrow -= dt; else c.Len = Math.Min(c.Max, c.Len + dt * 0.05 * Scale);
                c.Wob *= Math.Pow(0.05, dt);
                if (mx >= 0 && c.Len > 5 * Scale && Math.Abs(mx - c.X) < c.W + 5 * Scale && my > -2 && my < c.Len + 3 * Scale)
                {
                    if (sp > 220 * Scale)
                    {
                        c.Falling = true; c.Y = c.Len / 2; c.VY = 0;
                        foreach (var o in Icicles) if (o != c && Math.Abs(o.X - c.X) < 120 * Scale) o.Wob = Math.Max(o.Wob, 0.12);
                    }
                    else if (sp > 40 * Scale) c.Wob = Math.Max(c.Wob, 0.08);
                }
            }
        }
    }

    // ---------- Santa's sleigh ----------
    public void CallSleigh()
    {
        if (Sleigh != null) return;
        double dir = rng.NextDouble() < 0.5 ? 1 : -1;
        Sleigh = new Sleigh { X = dir > 0 ? -300 * Scale : Width + 300 * Scale, Y0 = Height * R(0.16, 0.3), Dir = dir, Speed = (Width + 600 * Scale) / 9, AutoAt = R(2.5, 6) };
    }
    public (double x, double y)? SleighPos => Sleigh is { } s ? (s.X, s.Y0 + Math.Sin(s.Age * 1.6) * 18 * Scale - s.Age * 4 * Scale) : null;
    void StepSleigh(double dt)
    {
        if (Sleigh == null)
        {
            if (Season != Season.Winter) return;
            nextSleigh -= dt; if (nextSleigh <= 0) { nextSleigh = R(300, 540); CallSleigh(); }
            return;
        }
        var s = Sleigh; s.Age += dt; s.X += s.Dir * s.Speed * dt;
        if (SleighPos is { } p)
        {
            if (rng.NextDouble() < dt * 40) AddSpark(p.x - s.Dir * 22 * Scale + R(-6, 6), p.y + 15 * Scale + R(-3, 3), -s.Dir * R(10, 50), R(-10, 40), R(0.8, 1.6), R(2, 4.5), rng.NextDouble() < 0.7 ? ((byte)255, (byte)216, (byte)102) : ((byte)255, (byte)255, (byte)255), 4, 40, 0.5);
            if (s.Gifts == 0 && s.Age > s.AutoAt && p.x > 60 * Scale && p.x < Width - 60 * Scale) DropGift();
        }
        if (s.Dir > 0 ? s.X > Width + 340 * Scale : s.X < -340 * Scale) Sleigh = null;
    }
    public bool SleighHit(double x, double y)
    {
        if (Sleigh is not { } s || SleighPos is not { } q) return false;
        double k = 1.15 * Scale;
        double left = s.Dir > 0 ? q.x - 70 * k : q.x - 300 * k;
        return x > left && x < left + 370 * k && y > q.y - 48 * k && y < q.y + 48 * k;
    }
    public void DropGift()
    {
        if (Sleigh is not { } s || SleighPos is not { } p || s.Gifts >= 4) return;
        s.Gifts++;
        Gifts.Add(new Gift { X = p.x - s.Dir * 10 * Scale, Y = p.y + 12 * Scale, VX = s.Dir * s.Speed * 0.25, VY = -40 * Scale, C = rng.Next(3), Rot = R(-0.3, 0.3) });
    }
    void StepGifts(double dt)
    {
        foreach (var g in Gifts)
        {
            g.Age += dt;
            double floor = SnowTop(g.X) - 12 * Scale;
            if (!g.Landed)
            {
                g.VY += 600 * Scale * dt; g.VX *= Math.Pow(0.6, dt);
                g.X = Math.Clamp(g.X + g.VX * dt, 14 * Scale, Width - 14 * Scale); g.Y += g.VY * dt; g.Rot += g.VX * dt * 0.004 / Scale;
                if (g.Y >= floor)
                {
                    g.Y = floor; g.Landed = true; g.Rot = 0; g.Age = 0;
                    for (int k = 0; k < 10; k++) AddSpark(g.X + R(-10, 10) * Scale, floor + 4 * Scale, R(-80, 80), -R(30, 120), R(0.5, 0.9), R(2, 4), (255, 255, 255), 5, 500, 0.3);
                }
            }
            else g.Y = floor;
        }
        Gifts.RemoveAll(g => g.Landed && g.Age > 120);
    }
    public Gift? GiftAt(double x, double y) => Gifts.FirstOrDefault(g => Math.Abs(g.X - x) < 22 * Scale && Math.Abs(g.Y - y) < 22 * Scale);
    public void OpenGift(Gift g)
    {
        Gifts.Remove(g);
        for (int k = 0; k < 60; k++)
        {
            double a = R(0.15 * Math.PI, 0.85 * Math.PI); var c = FireColors[k % FireColors.Length];
            AddSpark(g.X, g.Y - 4 * Scale, Math.Cos(a) * R(80, 330), -Math.Sin(a) * R(160, 520), R(1.6, 2.8), R(4, 7), c, 2, 420, 0.35, R(-14, 14));
        }
        for (int k = 0; k < 14; k++) AddSpark(g.X + R(-14, 14) * Scale, g.Y - R(-6, 18) * Scale, R(-40, 40), -R(20, 120), R(0.6, 1.2), R(5, 10), (255, 235, 153), 6, 30, 0.4);
        int wi = rng.Next(WishList.Length); if (wi == lastWish) wi = (wi + 1) % WishList.Length; lastWish = wi;
        Wishes.Add((g.X, g.Y - 26 * Scale, 0, Russian ? WishList[wi].ru : WishList[wi].en));
        if (Wishes.Count > 3) Wishes.RemoveAt(0);
        if (Season == Season.Winter && rng.NextDouble() < 0.35) RaiseSnowman(g.X);
    }

    // ---------- fireworks and the New Year countdown ----------
    public void StartFireworks(double countdown, double show)
    {
        if (countdown > 0) { Countdown = countdown; Show = 0; pendingShow = show; }
        else { Show = show; nextRocket = 0; }
    }
    public void CheckNewYear(DateTime now)
    {
        if (now.Month == 12 && now.Day == 31 && now.Hour == 23 && now.Minute == 59 && now.Second >= 49 && newYearDone != now.Year)
        { newYearDone = now.Year; StartFireworks(60 - now.Second, 75); }
    }
    public int GreetYear { get { var n = DateTime.Now; return n.Year + (n.Month >= 7 ? 1 : 0); } }
    void StepFireworks(double dt)
    {
        if (Countdown >= 0)
        {
            Countdown -= dt;
            if (Countdown < 0) { Countdown = -1; Greet = 8; Show = Math.Max(Show, pendingShow); nextRocket = 0; for (int k = 0; k < 4; k++) Launch(Width * (0.2 + 0.2 * k)); }
        }
        if (Greet > 0) Greet -= dt;
        if (Show > 0)
        {
            Show -= dt; nextRocket -= dt;
            if (nextRocket <= 0) { nextRocket = R(0.35, 1.0); Launch(R(Width * 0.1, Width * 0.9)); if (rng.NextDouble() < 0.25) Launch(R(Width * 0.1, Width * 0.9)); }
        }
        for (int i = Rockets.Count - 1; i >= 0; i--)
        {
            var r = Rockets[i];
            r.VY += 380 * Scale * dt; r.X += r.VX * dt; r.Y += r.VY * dt;
            if (rng.NextDouble() < dt * 60) AddSpark(r.X + R(-1, 1), r.Y + 3, R(-15, 15), R(20, 60), R(0.3, 0.6), R(1.6, 3), (255, 204, 115), 1, 60, 0.4);
            if (r.VY > -70 * Scale || r.Y < Height * 0.1) { Explode(r); Rockets.RemoveAt(i); }
        }
        if (FlashA > 0) FlashA = Math.Max(0, FlashA - dt * 3);
    }
    void Launch(double x)
    {
        double k = Math.Sqrt(Math.Max(0.5, Height / Scale / 956));
        Rockets.Add(new Rocket { X = x, Y = SnowTop(x) - 2, VX = R(-40, 40) * Scale, VY = -R(560, 760) * k * Scale, C = rng.Next(FireColors.Length), Pattern = rng.Next(5) });
    }
    void Explode(Rocket r)
    {
        double S = R(170, 260) * Math.Sqrt(Math.Max(0.5, Height / Scale / 956)) * Scale;
        var c0 = FireColors[r.C]; var c1 = FireColors[(r.C + 3) % FireColors.Length];
        int n = r.Pattern == 1 ? 48 : (int)R(70, 100);
        for (int k = 0; k < n; k++)
        {
            double a = R(0, Math.PI * 2), sp = S * Math.Sqrt(rng.NextDouble());
            switch (r.Pattern)
            {
                case 1: { double a2 = (double)k / n * Math.PI * 2; AddSpark(r.X, r.Y, Math.Cos(a2) * S, Math.Sin(a2) * S * 0.7, R(1.3, 1.7), 12, c0, 0, 70, 0.35); break; }
                case 2: { double s2 = S * R(0.3, 0.85); AddSpark(r.X, r.Y, Math.Cos(a) * s2, Math.Sin(a) * s2, R(2.4, 3.4), 10, (255, 199, 97), 0, 150, 0.6); break; }
                case 3: AddSpark(r.X, r.Y, Math.Cos(a) * sp, Math.Sin(a) * sp, R(1.2, 1.9), 12, k % 2 == 0 ? c0 : c1, 0, 90, 0.4); break;
                case 4: AddSpark(r.X, r.Y, Math.Cos(a) * sp, Math.Sin(a) * sp, R(1.0, 1.6), 10, (255, 255, 235), 7, 100, 0.45); break;
                default: AddSpark(r.X, r.Y, Math.Cos(a) * sp, Math.Sin(a) * sp, R(1.2, 1.9), 12, c0, 0, 90, 0.4); break;
            }
        }
        FlashX = r.X; FlashY = r.Y; FlashA = 1; (FlashR, FlashG, FlashB) = r.Pattern == 2 ? ((byte)255, (byte)204, (byte)102) : c0;
    }

    // ---------- sparks ----------
    /// <summary>grav: downward acceleration in points/s² (negative = rises); kinds: 0 firework, 1 rocket trail, 2 confetti,
    /// 3 ice shard, 4 glitter, 5 powder snow, 6 star, 7 crackle.</summary>
    void AddSpark(double x, double y, double vx, double vy, double life, double size, (byte r, byte g, byte b) c, int kind, double grav, double drag, double spin = 0)
    {
        if (Sparks.Count > 1600) return;
        Sparks.Add(new Spark { X = x, Y = y, VX = vx * Scale, VY = vy * Scale, Life = life, Size = size * Scale, R = c.r, G = c.g, B = c.b, Kind = kind, Grav = grav * Scale, Drag = drag, Rot = R(0, 6), Spin = spin });
    }
    void StepSparks(double dt)
    {
        for (int i = Sparks.Count - 1; i >= 0; i--)
        {
            var s = Sparks[i];
            s.Age += dt; s.VY += s.Grav * dt;
            double dr = Math.Pow(s.Drag, dt); s.VX *= dr; s.VY *= dr;
            if (s.Kind == 2) s.VX += Math.Sin(s.Age * 7 + s.Rot) * 40 * Scale * dt;
            s.X += s.VX * dt; s.Y += s.VY * dt; s.Rot += s.Spin * dt;
            if ((s.Kind == 2 || s.Kind == 3) && s.Y > SnowTop(s.X) && s.VY > 0) { s.VY = 0; s.VX = 0; s.Grav = 0; s.Y = SnowTop(s.X) - 1; }
            if (s.Age >= s.Life || s.Y > Height + 20) Sparks.RemoveAt(i);
        }
    }

    // ---------- pointer ----------
    public bool FestInteractive(double x, double y) => SleighHit(x, y) || GiftAt(x, y) != null;
    public bool FestMouseDown(double x, double y)
    {
        if (GiftAt(x, y) is { } g) { OpenGift(g); return true; }
        if (SleighHit(x, y)) { DropGift(); return true; }
        return false;
    }
    public bool HeadlessHit(Snowman m, Ball b)
    {
        if (m.Headless) return false;
        double c0 = m.Y - m.R[0] * 0.9, c1 = c0 - m.R[0] * 0.78 - m.R[1] * 0.75, c2 = c1 - m.R[1] * 0.8 - m.R[2] * 0.75;
        Balls.Add(new Ball { Id = nextBall++, X = m.X, Y = c2, R = m.R[2], VX = b.VX * 0.55, VY = -(Math.Abs(b.VY) * 0.25 + 320 * Scale), Face = true });
        for (int k = 0; k < 12; k++) AddSpark(m.X + R(-6, 6) * Scale, c2 + m.R[2] * 0.6, R(-120, 120), -R(40, 200), R(0.6, 1.2), R(1.6, 3.4), (255, 255, 255), 5, 260, 0.15);
        m.Headless = true; m.Wave = 0;
        return true;
    }
}
