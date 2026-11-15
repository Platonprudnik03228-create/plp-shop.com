using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using PLPSeasons.Core;

namespace PLPSeasons;

/// <summary>A glass card that lives on the desktop: draggable, under the other windows, not in the taskbar or Alt+Tab.</summary>
abstract class DesktopCard : Window
{
    public static readonly Color Accent = Color.FromRgb(0x61, 0xA3, 0xFF);
    protected static readonly Brush Text = new SolidColorBrush(Color.FromRgb(0xF4, 0xF6, 0xFA));
    protected static readonly Brush Dim = new SolidColorBrush(Color.FromArgb(0xA8, 0xE6, 0xEC, 0xF5));
    protected readonly StackPanel Body = new() { Margin = new Thickness(18, 16, 18, 16) };
    protected readonly AppSettings Set;

    protected DesktopCard(AppSettings set, double width)
    {
        Set = set;
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent; ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false; ShowActivated = false; Width = width; SizeToContent = SizeToContent.Height; Topmost = false;
        FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI"); Foreground = Text;
        var card = new Border
        {
            CornerRadius = new CornerRadius(22), Padding = new Thickness(0), Margin = new Thickness(14),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)), BorderThickness = new Thickness(1),
            Background = new LinearGradientBrush(Color.FromArgb(0xE6, 0x1B, 0x22, 0x33), Color.FromArgb(0xE6, 0x10, 0x13, 0x1C), 90),
            Effect = new DropShadowEffect { BlurRadius = 24, ShadowDepth = 4, Opacity = 0.45, Color = Colors.Black },
            Child = Body,
        };
        Content = card;
        MouseLeftButtonDown += (_, e) => { if (e.OriginalSource is not Slider and not Thumb and not ButtonBase and not TextBox) { try { DragMove(); } catch { } SavePos(); } };
        SourceInitialized += (_, _) =>
        {
            var h = new WindowInteropHelper(this).Handle;
            var ex = (long)Native.GetWindowLongPtr(h, Native.GWL_EXSTYLE);
            Native.SetWindowLongPtr(h, Native.GWL_EXSTYLE, new IntPtr((ex | Native.WS_EX_TOOLWINDOW) & ~(long)Native.WS_EX_APPWINDOW));
            ToBack();
        };
        Deactivated += (_, _) => ToBack();
    }
    void ToBack()
    {
        var h = new WindowInteropHelper(this).Handle;
        if (h != IntPtr.Zero) Native.SetWindowPos(h, new IntPtr(1) /*HWND_BOTTOM*/, 0, 0, 0, 0, 0x1 | 0x2 | Native.SWP_NOACTIVATE);
    }
    protected abstract void SavePos();
    protected static TextBlock T(string s, double size, Brush b, FontWeight? w = null) =>
        new() { Text = s, FontSize = size, Foreground = b, FontWeight = w ?? FontWeights.Normal, TextTrimming = TextTrimming.CharacterEllipsis };

    protected static Slider MakeSlider(double min, double max, double v)
    {
        var s = new Slider { Minimum = min, Maximum = max, Value = v, Margin = new Thickness(0, 4, 0, 10), IsMoveToPointEnabled = true,
            Foreground = new SolidColorBrush(Accent) };
        s.Resources[SystemColors.HighlightBrushKey] = new SolidColorBrush(Accent);
        return s;
    }
}

/// <summary>The weather card: city, temperature, sky, today's high/low, and the clock of that place. Free for everyone.</summary>
sealed class WeatherCard : DesktopCard
{
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    readonly TextBlock city = T("…", 15, Text, FontWeights.SemiBold), clock = T("", 13, Dim), temp = T("", 46, Text, FontWeights.Light),
        icon = T("", 38, Text), desc = T("", 14, Text), range = T("", 13, Dim), note = T("", 11, Dim);
    readonly TextBox search = new() { Visibility = Visibility.Collapsed, FontSize = 14, Padding = new Thickness(8, 5, 8, 5), Margin = new Thickness(0, 6, 0, 2),
        Background = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)), Foreground = Text, BorderThickness = new Thickness(0), CaretBrush = Text };
    readonly ListBox results = new() { Visibility = Visibility.Collapsed, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Text, MaxHeight = 160 };
    WeatherNow? now;
    readonly DispatcherTimer tick = new() { Interval = TimeSpan.FromSeconds(20) };
    DateTime lastFetch = DateTime.MinValue;
    public static bool Ru => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName is "ru" or "uk" or "be" or "kk";

    public WeatherCard(AppSettings set) : base(set, 300)
    {
        var top = new DockPanel();
        var change = new Button { Content = "⌕", FontSize = 15, Foreground = Dim, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
            ToolTip = L.T("Сменить город", "Change city") };
        change.Click += (_, _) => { bool show = search.Visibility != Visibility.Visible; search.Visibility = show ? Visibility.Visible : Visibility.Collapsed; results.Visibility = Visibility.Collapsed; if (show) { Activate(); search.Focus(); } };
        DockPanel.SetDock(change, Dock.Right); top.Children.Add(change); top.Children.Add(city);
        Body.Children.Add(top);
        Body.Children.Add(clock);
        Body.Children.Add(search); Body.Children.Add(results);
        var mid = new DockPanel { Margin = new Thickness(0, 6, 0, 0) };
        DockPanel.SetDock(icon, Dock.Right); icon.VerticalAlignment = VerticalAlignment.Center;
        mid.Children.Add(icon); mid.Children.Add(temp);
        Body.Children.Add(mid); Body.Children.Add(desc); Body.Children.Add(range); Body.Children.Add(note);
        search.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await Search(search.Text); if (e.Key == Key.Escape) { search.Visibility = Visibility.Collapsed; results.Visibility = Visibility.Collapsed; } };
        results.MouseLeftButtonUp += (_, _) => { if (results.SelectedItem is ListBoxItem { Tag: Place p }) Choose(p); };
        tick.Tick += async (_, _) => { if (DateTime.UtcNow - lastFetch > TimeSpan.FromMinutes(15)) await Fetch(); Draw(); };
        Loaded += async (_, _) => { PlaceWindow(); tick.Start(); await EnsureCity(); await Fetch(); Draw(); };
    }

    void PlaceWindow()
    {
        var wa = SystemParameters.WorkArea;
        Left = double.IsNaN(Set.WeatherX) ? wa.Right - Width - 10 : Set.WeatherX;
        Top = double.IsNaN(Set.WeatherY) ? wa.Top + 10 : Set.WeatherY;
    }
    protected override void SavePos() { Set.WeatherX = Left; Set.WeatherY = Top; Store.Save(Set); }

    async Task EnsureCity()
    {
        if (!string.IsNullOrEmpty(Set.City)) return;
        // first start: the capital-like city of the PC's time zone ("Europe/Berlin" → Berlin), the user can change it
        string guess = TimeZoneInfo.Local.Id;
        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(guess, out var iana)) guess = iana;
        var name = guess.Contains('/') ? guess[(guess.LastIndexOf('/') + 1)..].Replace('_', ' ') : "London";
        var list = await Lookup(name);
        if (list.Count > 0) Choose(list[0], fetch: false);
    }

    async Task<System.Collections.Generic.List<Place>> Lookup(string q)
    {
        try { return WeatherApi.ParseSearch(await Http.GetStringAsync(WeatherApi.SearchUrl(q, Ru ? "ru" : "en"))); } catch { return new(); }
    }

    async Task Search(string q)
    {
        if (string.IsNullOrWhiteSpace(q)) return;
        var list = await Lookup(q.Trim());
        results.Items.Clear();
        foreach (var p in list) results.Items.Add(new ListBoxItem { Content = p.Name, Tag = p, Foreground = Text, Padding = new Thickness(6, 4, 6, 4) });
        if (list.Count == 0) results.Items.Add(new ListBoxItem { Content = L.T("Не нашёл такой город", "City not found"), Foreground = Dim, IsEnabled = false });
        results.Visibility = Visibility.Visible;
    }

    async void Choose(Place p, bool fetch = true)
    {
        Set.City = p.Name; Set.CityLat = p.Lat; Set.CityLon = p.Lon; Set.CityTz = p.TimeZone; Store.Save(Set);
        search.Visibility = Visibility.Collapsed; results.Visibility = Visibility.Collapsed; search.Text = "";
        if (fetch) { await Fetch(); Draw(); }
    }

    async Task Fetch()
    {
        if (string.IsNullOrEmpty(Set.City)) return;
        try
        {
            var w = WeatherApi.ParseForecast(await Http.GetStringAsync(WeatherApi.ForecastUrl(new Place(Set.City, Set.CityLat, Set.CityLon, Set.CityTz ?? "UTC"))));
            if (w != null) { now = w; lastFetch = DateTime.UtcNow; }
        }
        catch { }
    }

    DateTime PlaceTime()
    {
        try { return TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, now?.TimeZone ?? Set.CityTz ?? "UTC"); } catch { return DateTime.Now; }
    }

    void Draw()
    {
        city.Text = Set.City?.Split(',')[0] ?? "…";
        var t = PlaceTime();
        clock.Text = t.ToString(Ru ? "HH:mm · dddd" : "h:mm tt · dddd", CultureInfo.CurrentUICulture);
        if (now == null) { temp.Text = "—"; desc.Text = L.T("Нет связи с погодой", "No weather connection"); return; }
        bool night = t.Hour < 6 || t.Hour >= 20;
        temp.Text = $"{Math.Round(now.Temp):0}°";
        icon.Text = WeatherApi.Icon(now.Code, night);
        desc.Text = WeatherApi.Describe(now.Code, Ru) + "  ·  " + L.T($"ощущается {Math.Round(now.Feels):0}°", $"feels {Math.Round(now.Feels):0}°");
        range.Text = L.T($"Макс {Math.Round(now.Max):0}°  Мин {Math.Round(now.Min):0}°  ·  ветер {now.Wind:0} м/с", $"H {Math.Round(now.Max):0}°  L {Math.Round(now.Min):0}°  ·  wind {now.Wind:0} m/s");
        int mins = (int)(DateTime.UtcNow - now.Fetched).TotalMinutes;
        note.Text = L.T(mins < 1 ? "обновлено только что" : $"обновлено {mins} мин назад", mins < 1 ? "updated just now" : $"updated {mins} min ago");
    }
}

/// <summary>The remote: season, intensity and wind sliders, pause. Changes apply at once.</summary>
sealed class RemoteCard : DesktopCard
{
    readonly App app;
    readonly Slider intensity, wind;
    readonly TextBlock intensityPct = T("", 13, Dim), windLbl = T("", 13, Dim);
    readonly Button pause;
    readonly WrapPanel seasons = new() { Margin = new Thickness(0, 10, 0, 12) };
    bool updating;

    public RemoteCard(App app, AppSettings set) : base(set, 300)
    {
        this.app = app;
        Body.Children.Add(T("PLP Seasons", 15, Text, FontWeights.SemiBold));
        Body.Children.Add(seasons);
        foreach (var (s, ic) in new[] { (Season.Winter, "❄"), (Season.Rain, "🌧"), (Season.Storm, "⛈"), (Season.Autumn, "🍂"), (Season.Spring, "🌸"), (Season.Summer, "☀") })
        {
            var b = new Button { Content = ic, Tag = s, FontSize = 17, Width = 38, Height = 38, Margin = new Thickness(0, 0, 6, 6), Cursor = Cursors.Hand, ToolTip = L.Name(s),
                Foreground = Text, BorderThickness = new Thickness(2), Template = PillTemplate() };
            b.Click += (_, _) => app.SetSeason(s);
            seasons.Children.Add(b);
        }
        var row1 = new DockPanel(); DockPanel.SetDock(intensityPct, Dock.Right); row1.Children.Add(intensityPct); row1.Children.Add(T(L.T("Сила", "Intensity"), 13, Text));
        Body.Children.Add(row1);
        intensity = MakeSlider(0.1, 2.0, set.Intensity); Body.Children.Add(intensity);
        var row2 = new DockPanel(); DockPanel.SetDock(windLbl, Dock.Right); row2.Children.Add(windLbl); row2.Children.Add(T(L.T("Ветер  ← · →", "Wind  ← · →"), 13, Text));
        Body.Children.Add(row2);
        wind = MakeSlider(-1, 1, set.Wind); Body.Children.Add(wind);
        intensity.ValueChanged += (_, e) => { if (!updating) app.SetIntensity(e.NewValue); Labels(); };
        wind.ValueChanged += (_, e) => { if (!updating) app.SetWind(Math.Abs(e.NewValue) < 0.06 ? 0 : e.NewValue); Labels(); };
        pause = new Button { Height = 34, FontSize = 13, Cursor = Cursors.Hand, Foreground = Text, BorderThickness = new Thickness(2), Template = PillTemplate() };
        pause.Click += (_, _) => app.TogglePause();
        Body.Children.Add(pause);
        Loaded += (_, _) =>
        {
            var wa = SystemParameters.WorkArea;
            Left = double.IsNaN(Set.RemoteX) ? wa.Right - Width - 10 : Set.RemoteX;
            Top = double.IsNaN(Set.RemoteY) ? wa.Top + 250 : Set.RemoteY;
            Refresh();
        };
    }
    protected override void SavePos() { Set.RemoteX = Left; Set.RemoteY = Top; Store.Save(Set); }

    /// <summary>A rounded pill whose whole area is the button, outlined in blue when selected.</summary>
    static ControlTemplate PillTemplate()
    {
        var t = new ControlTemplate(typeof(Button));
        var bd = new FrameworkElementFactory(typeof(Border));
        bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(12));
        bd.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        bd.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        bd.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        cp.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center); cp.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        bd.AppendChild(cp); t.VisualTree = bd;
        return t;
    }

    void Labels()
    {
        intensityPct.Text = $"{Math.Round(intensity.Value * 100):0}%";
        double w = wind.Value;
        windLbl.Text = Math.Abs(w) < 0.06 ? L.T("прямо вниз", "straight down") : (w < 0 ? "← " : "") + $"{Math.Round(Math.Abs(w) * 100):0}%" + (w > 0 ? " →" : "");
    }

    public void Refresh()
    {
        updating = true;
        intensity.Value = Set.Intensity; wind.Value = Set.Wind;
        updating = false;
        Labels();
        var on = new SolidColorBrush(Color.FromArgb(0x55, Accent.R, Accent.G, Accent.B));
        var off = new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF));
        foreach (Button b in seasons.Children)
        {
            bool sel = (Season)b.Tag == Set.Season;
            b.Background = sel ? on : off;
            b.BorderBrush = sel ? new SolidColorBrush(Accent) : Brushes.Transparent;
        }
        pause.Content = Set.Paused ? L.T("▶  Включить", "▶  Resume") : L.T("⏸  Пауза", "⏸  Pause");
        pause.Background = off; pause.BorderBrush = new SolidColorBrush(Color.FromArgb(0x60, Accent.R, Accent.G, Accent.B));
    }
}
