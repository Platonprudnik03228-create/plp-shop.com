using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using PLPSeasons.Core;
using Forms = System.Windows.Forms;

namespace PLPSeasons;

public partial class App : Application
{
    AppSettings set = new();
    readonly List<Overlay> overlays = new();
    WallpaperManager wall = null!;
    Forms.NotifyIcon tray = null!;
    readonly Stopwatch clock = Stopwatch.StartNew();
    double last, lastScan, lastWall, lastTop;
    Mutex? single;
    bool demo;
    UpdateInfo? update;
    WeatherCard? weatherCard;
    RemoteCard? remoteCard;
    System.Windows.Threading.DispatcherTimer? updTimer;
    double demoStart, demoLength = 80; bool demoMan, demoPlow, demoFill;
    int frames; double fpsT; readonly List<double> gaps = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        single = new Mutex(true, "PLP.Seasons.Single", out bool first);
        if (!first) { Shutdown(); return; }
        demo = e.Args.Contains("--demo");
        Overlay.Legacy = e.Args.Contains("--legacy");
        int ix = Array.IndexOf(e.Args, "--seconds"); if (ix >= 0 && ix + 1 < e.Args.Length) double.TryParse(e.Args[ix + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out demoLength);
        set = Store.Load();
        if (set.AutoSeason) set.Season = Seasons.FromDate(DateTime.Now);
        if (demo) { set.Season = Season.Winter; set.Intensity = 1.0; set.Paused = false; }
        wall = new WallpaperManager(set);
        Forms.Application.EnableVisualStyles();
        BuildOverlays();
        BuildTray();
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += (_, _) => Dispatcher.BeginInvoke(BuildOverlays);
        CompositionTarget.Rendering += OnFrame;
        if (set.Wallpaper && !set.Paused) UpdateWallpaper(set.Season);
        ShowWidgets(set.Widgets || demo);
        if (!demo) StartUpdateChecks();
        if (!demo && !set.Paused) tray.ShowBalloonTip(4000, "PLP Seasons", L.T("Работает. Настройки — в значке у часов.", "Running. Settings are in the tray icon next to the clock."), Forms.ToolTipIcon.None);
    }

    void BuildOverlays()
    {
        foreach (var o in overlays) o.Close();
        overlays.Clear();
        int seed = 1;
        foreach (var sc in Forms.Screen.AllScreens)
        {
            var b = sc.Bounds;
            var o = new Overlay(new RectI(b.Left, b.Top, b.Right, b.Bottom), seed++);
            Apply(o.Sim);
            if (!set.Paused) o.Show();
            overlays.Add(o);
        }
        lastScan = -1;
    }

    void Apply(Simulation s) { s.Season = set.Season; s.Intensity = set.Intensity; s.Wind = set.Wind; s.GarlandMode = set.Garlands; s.PlowEnabled = set.AutoPlow; }

    TimeSpan lastRender;
    void OnFrame(object? sender, EventArgs e)
    {
        // the time WPF will show this frame at - steadier than a stopwatch, so the flakes move without judder
        if (e is RenderingEventArgs re) { if (re.RenderingTime == lastRender) return; lastRender = re.RenderingTime; }
        double now = clock.Elapsed.TotalSeconds, dt = now - last; last = now;
        if (demo) FpsLog(now, dt);
        if (set.Paused) return;
        if (demo) DemoScript(now);
        if (now - lastScan > 0.2) { lastScan = now; ScanWindows(); }
        if (now - lastTop > 2) { lastTop = now; foreach (var o in overlays) o.KeepOnTop(); }
        if (now - lastWall > 60) { lastWall = now; UpdateWallpaper(set.Season); }
        PointerToSnow();
        foreach (var o in overlays) { o.Sim.Step(dt); o.Redraw(); }
    }

    /// <summary>Over snow you can play with, the overlay takes the mouse; everywhere else clicks go to the windows below.</summary>
    void PointerToSnow()
    {
        if (!Native.GetCursorPos(out var c)) return;
        foreach (var o in overlays)
        {
            var sc = o.Screen; double x = c.X - sc.Left, y = c.Y - sc.Top;
            bool inside = sc.Contains(c.X, c.Y);
            if (!o.Sim.Holding && inside) o.Sim.MouseMove(x, y);          // garlands feel the pointer passing by
            o.SetInteractive(o.Sim.Holding || (inside && o.Sim.IsInteractive(x, y) && !Forms.Control.IsKeyLocked(Forms.Keys.Scroll)));
        }
    }

    public void CallPlow() { foreach (var o in overlays) o.Sim.CallPlow(); }

    void ScanWindows()
    {
        var own = overlays.Select(o => o.Handle).ToHashSet();
        var wins = WindowScanner.AppWindows(own);
        var bars = WindowScanner.Taskbars();
        double corner = WindowScanner.IsWindows11 ? 8 : 0;
        foreach (var o in overlays)
        {
            var sc = o.Screen;
            var list = new List<(long, RectI, double)>();
            if (set.SnowOnWindows)
            {
                // window tops visible on this screen, minus parts hidden under windows above them
                var tops = Simulation.VisibleTops(wins.Where(w => w.rect.Right > sc.Left && w.rect.Left < sc.Right && w.rect.Bottom > sc.Top && w.rect.Top < sc.Bottom).ToList());
                foreach (var (id, r) in tops)
                {
                    if (r.Top <= sc.Top + 2) continue;          // maximised windows: snow lies on the taskbar instead
                    list.Add((id, new RectI(r.Left - sc.Left, r.Top - sc.Top, r.Right - sc.Left, r.Bottom - sc.Top), corner));
                    if (list.Count > 24) break;
                }
            }
            // the taskbar (bottom) or else the bottom edge of the screen
            var bar = bars.FirstOrDefault(b => b.Top > sc.Top + sc.Height / 2 && b.Left < sc.Right && b.Right > sc.Left);
            int floor = bar.Width > 0 && !bar.IsEmpty ? bar.Top - sc.Top : sc.Height;
            if (bar.Width > 0 && IsCovered(wins, bar)) floor = sc.Height;   // full-screen app covers the taskbar
            list.Add((-1 - overlays.IndexOf(o), new RectI(0, floor, sc.Width, sc.Height + 1), 0));
            o.Sim.SetSurfaces(list);
        }
    }

    static bool IsCovered(List<(long id, RectI rect)> wins, RectI bar) =>
        wins.Count > 0 && wins[0].rect.Left <= bar.Left && wins[0].rect.Right >= bar.Right && wins[0].rect.Bottom >= bar.Bottom && wins[0].rect.Top <= bar.Top - 400;

    // ---------- tray ----------
    void BuildTray()
    {
        tray = new Forms.NotifyIcon { Text = "PLP Seasons", Visible = true };
        try
        {
            var st = GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"))?.Stream;
            tray.Icon = st != null ? new System.Drawing.Icon(st) : System.Drawing.SystemIcons.Application;
        }
        catch { tray.Icon = System.Drawing.SystemIcons.Application; }
        tray.ContextMenuStrip = new Forms.ContextMenuStrip();
        tray.ContextMenuStrip.Opening += (_, _) => FillMenu();
        tray.MouseClick += (_, a) => { if (a.Button == Forms.MouseButtons.Left) TogglePause(); };
        FillMenu();
    }

    void FillMenu()
    {
        var m = tray.ContextMenuStrip!; m.Items.Clear();
        m.Items.Add(new Forms.ToolStripLabel("PLP Seasons") { Font = new System.Drawing.Font(m.Font, System.Drawing.FontStyle.Bold) });
        m.Items.Add(new Forms.ToolStripSeparator());
        var auto = new Forms.ToolStripMenuItem(L.T("По календарю", "Follow the calendar")) { Checked = set.AutoSeason };
        auto.Click += (_, _) => { set.AutoSeason = !set.AutoSeason; if (set.AutoSeason) ChangeSeason(Seasons.FromDate(DateTime.Now), keepAuto: true); Save(); };
        m.Items.Add(auto);
        foreach (Season s in Enum.GetValues<Season>())
        {
            var it = new Forms.ToolStripMenuItem(L.Name(s)) { Checked = set.Season == s };
            it.Click += (_, _) => ChangeSeason(s, keepAuto: false);
            m.Items.Add(it);
        }
        m.Items.Add(new Forms.ToolStripSeparator());
        var inten = new Forms.ToolStripMenuItem(L.T("Сила", "Intensity"));
        foreach (var (name, v) in new[] { (L.T("Лёгкая", "Light"), 0.3), (L.T("Обычная", "Normal"), 0.6), (L.T("Сильная", "Heavy"), 1.0), (L.T("Метель", "Blizzard"), 1.7) })
        {
            var it = new Forms.ToolStripMenuItem(name) { Checked = Math.Abs(set.Intensity - v) < 0.01 };
            it.Click += (_, _) => { set.Intensity = v; ApplyAll(); Save(); remoteCard?.Refresh(); };
            inten.DropDownItems.Add(it);
        }
        m.Items.Add(inten);
        var wind = new Forms.ToolStripMenuItem(L.T("Ветер", "Wind"));
        foreach (var (name, v) in new[] { (L.T("← Влево", "← Left"), -0.7), (L.T("Нет", "None"), 0.0), (L.T("Слабый →", "Light →"), 0.25), (L.T("Вправо →", "Right →"), 0.7) })
        {
            var it = new Forms.ToolStripMenuItem(name) { Checked = Math.Abs(set.Wind - v) < 0.01 };
            it.Click += (_, _) => { set.Wind = v; ApplyAll(); Save(); remoteCard?.Refresh(); };
            wind.DropDownItems.Add(it);
        }
        m.Items.Add(wind);
        var onWin = new Forms.ToolStripMenuItem(L.T("Снег на окнах", "Snow on windows")) { Checked = set.SnowOnWindows };
        onWin.Click += (_, _) => { set.SnowOnWindows = !set.SnowOnWindows; Save(); };
        m.Items.Add(onWin);
        var plow = new Forms.ToolStripMenuItem(L.T("🚜 Вызвать снегоуборщик", "🚜 Call the snowplow")) { Enabled = set.Season == Season.Winter };
        plow.Click += (_, _) => CallPlow();
        m.Items.Add(plow);
        var gar = new Forms.ToolStripMenuItem(L.T("Гирлянды", "Garlands"));
        foreach (var (name, v) in new[] { (L.T("Цветные", "Coloured"), 1), (L.T("Тёплые", "Warm"), 2), (L.T("Выключить", "Off"), 0) })
        {
            var it = new Forms.ToolStripMenuItem(name) { Checked = set.Garlands == v };
            it.Click += (_, _) => { set.Garlands = v; ApplyAll(); Save(); };
            gar.DropDownItems.Add(it);
        }
        m.Items.Add(gar);
        var clear = new Forms.ToolStripMenuItem(L.T("Сломать снеговиков и снежки", "Knock down snowmen and balls"));
        clear.Click += (_, _) => { foreach (var o in overlays) { foreach (var sm in o.Sim.Snowmen.ToList()) o.Sim.CrumbleSnowman(sm); foreach (var b in o.Sim.Balls.ToList()) o.Sim.CrumbleBall(b); } };
        m.Items.Add(clear);
        var wg = new Forms.ToolStripMenuItem(L.T("Виджеты на рабочем столе", "Desktop widgets")) { Checked = set.Widgets };
        wg.Click += (_, _) => { set.Widgets = !set.Widgets; ShowWidgets(set.Widgets); Save(); };
        m.Items.Add(wg);
        var wp = new Forms.ToolStripMenuItem(L.T("Живые обои по времени суток", "Wallpaper by time of day")) { Checked = set.Wallpaper };
        wp.Click += (_, _) => { set.Wallpaper = !set.Wallpaper; if (set.Wallpaper) UpdateWallpaper(set.Season); else wall.Restore(); Save(); };
        m.Items.Add(wp);
        m.Items.Add(new Forms.ToolStripSeparator());
        var pause = new Forms.ToolStripMenuItem(set.Paused ? L.T("▶ Включить", "▶ Resume") : L.T("⏸ Пауза", "⏸ Pause"));
        pause.Click += (_, _) => TogglePause();
        m.Items.Add(pause);
        var start = new Forms.ToolStripMenuItem(L.T("Запускать вместе с Windows", "Start with Windows")) { Checked = set.StartWithWindows };
        start.Click += (_, _) => { set.StartWithWindows = !set.StartWithWindows; Store.SetAutostart(set.StartWithWindows); Save(); };
        m.Items.Add(start);
        if (update != null)
        {
            var up = new Forms.ToolStripMenuItem(L.T("⬇ Скачать новую версию ", "⬇ Download new version ") + update.Version) { Font = new System.Drawing.Font(m.Font, System.Drawing.FontStyle.Bold) };
            up.Click += (_, _) => OpenUrl(update.Url);
            m.Items.Add(up);
        }
        var site = new Forms.ToolStripMenuItem("plp-shop.com  ·  v" + UpdateChecker.Current);
        site.Click += (_, _) => OpenUrl("https://plp-shop.com");
        m.Items.Add(site);
        var quit = new Forms.ToolStripMenuItem(L.T("Выйти", "Quit"));
        quit.Click += (_, _) => Quit();
        m.Items.Add(quit);
    }

    void StartUpdateChecks()
    {
        tray.BalloonTipClicked += (_, _) => { if (update != null) OpenUrl(update.Url); };
        updTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        updTimer.Tick += async (_, _) =>
        {
            updTimer.Interval = TimeSpan.FromHours(6);
            var u = await UpdateChecker.Check();
            if (u == null || u.Version == update?.Version) return;
            update = u;
            tray.ShowBalloonTip(10000, L.T("Вышла новая версия PLP Seasons", "A new PLP Seasons is out") + " " + u.Version,
                (u.Notes ?? "") + " " + L.T("Нажми, чтобы скачать.", "Click to download."), Forms.ToolTipIcon.Info);
        };
        updTimer.Start();
    }
    static void OpenUrl(string url) { try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { } }

    void ChangeSeason(Season s, bool keepAuto)
    {
        if (!keepAuto) set.AutoSeason = false;
        set.Season = s; ApplyAll(); UpdateWallpaper(s); Save(); remoteCard?.Refresh();
    }
    public void SetSeason(Season s) => ChangeSeason(s, keepAuto: false);
    public void SetIntensity(double v) { set.Intensity = Math.Round(v, 2); ApplyAll(); Save(); }
    public void SetWind(double v) { set.Wind = Math.Round(v, 2); ApplyAll(); Save(); }

    void ShowWidgets(bool on)
    {
        if (on)
        {
            weatherCard ??= new WeatherCard(set); remoteCard ??= new RemoteCard(this, set);
            weatherCard.Show(); remoteCard.Show();
        }
        else { weatherCard?.Hide(); remoteCard?.Hide(); }
    }
    // Setting the Windows wallpaper takes ~0.2 s; off the UI thread so the snow never stutters.
    System.Threading.Tasks.Task wallTask = System.Threading.Tasks.Task.CompletedTask;
    void UpdateWallpaper(Season s) { var prev = wallTask; wallTask = System.Threading.Tasks.Task.Run(async () => { await prev; wall.Update(s); }); }
    void ApplyAll() { foreach (var o in overlays) Apply(o.Sim); }
    void Save() => Store.Save(set);

    public void TogglePause()
    {
        set.Paused = !set.Paused; Save(); remoteCard?.Refresh();
        foreach (var o in overlays) { if (set.Paused) o.Hide(); else o.Show(); }
    }

    void Quit()
    {
        tray.Visible = false; tray.Dispose();
        Save();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e) { try { tray?.Dispose(); } catch { } base.OnExit(e); }

    // ---------- demo for the test video: winter, rain, storm, autumn, spring, back to winter ----------
    void FpsLog(double now, double dt)
    {
        frames++; gaps.Add(dt);
        if (now - fpsT < 5) return;
        gaps.Sort();
        double p95 = gaps[(int)(gaps.Count * 0.95)], max = gaps[^1];
        try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plp_fps.txt"),
            $"{(Overlay.Legacy ? "legacy" : "fast")} t={now:0} fps={frames / (now - fpsT):0.0} p95={p95 * 1000:0.0}ms max={max * 1000:0.0}ms season={set.Season}\n"); } catch { }
        frames = 0; gaps.Clear(); fpsT = now;
    }

    void DemoScript(double now)
    {
        if (demoStart == 0) demoStart = now;
        double t = now - demoStart;
        if (t > 2 && !demoFill && overlays.Count > 0 && overlays[0].Sim.Floor is { } fl) { demoFill = true; for (int c = 0; c < fl.Depth.Length; c++) fl.Depth[c] = 22 * overlays[0].Sim.Scale; }
        if (t > 3 && !demoMan && overlays.Count > 0) { demoMan = true; var sim0 = overlays[0].Sim; if (sim0.Floor != null) sim0.AddSnowman(sim0.Width * 0.62, 34 * sim0.Scale); }
        if (t > 4 && !demoPlow) { demoPlow = true; CallPlow(); }
        Season want = t switch { < 50 => Season.Winter, < 55 => Season.Rain, < 60 => Season.Storm, < 66 => Season.Autumn, < 71 => Season.Spring, _ => Season.Winter };
        if (want != set.Season) { set.Season = want; ApplyAll(); UpdateWallpaper(want); remoteCard?.Refresh(); }
        if (t > demoLength) Quit();
    }
}
