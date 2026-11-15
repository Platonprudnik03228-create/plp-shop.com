namespace PLPSeasons.Core;

public enum Season { Winter, Rain, Storm, Autumn, Spring, Summer }
public enum Phase { Morning, Day, Evening, Night }

/// <summary>A rectangle in screen pixels (y grows downwards, like Windows).</summary>
public readonly record struct RectI(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
    public bool IsEmpty => Width <= 0 || Height <= 0;
    public bool Contains(double x, double y) => x >= Left && x < Right && y >= Top && y < Bottom;
}

public static class Seasons
{
    /// <summary>The wallpaper world of each season - the same plates as the Mac app (forest collection).</summary>
    public static string World(Season s) => s switch
    {
        Season.Winter => "cabin", Season.Autumn => "park", Season.Spring => "blossom", Season.Summer => "meadow", _ => "city",
    };
    public static Season FromDate(DateTime d) => d.Month switch
    {
        12 or 1 or 2 => Season.Winter, 3 or 4 or 5 => Season.Spring, 6 or 7 or 8 => Season.Summer, _ => Season.Autumn,
    };
    /// <summary>Simple time of day: morning 5-10, day 10-17, evening 17-21, night otherwise.</summary>
    public static Phase PhaseAt(DateTime local) => local.Hour switch
    {
        >= 5 and < 10 => Phase.Morning, >= 10 and < 17 => Phase.Day, >= 17 and < 21 => Phase.Evening, _ => Phase.Night,
    };
    public static string Name(this Phase p) => p.ToString().ToLowerInvariant();
}

public sealed class AppSettings
{
    public Season Season { get; set; } = Seasons.FromDate(DateTime.Now);
    public bool AutoSeason { get; set; } = true;
    public double Intensity { get; set; } = 0.6;     // 0.1 .. 2
    public double Wind { get; set; } = 0.25;         // -1 .. 1
    public bool Paused { get; set; }
    public bool Wallpaper { get; set; } = true;
    public bool SnowOnWindows { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public string? OriginalWallpaper { get; set; }
    public bool Widgets { get; set; } = true;
    public int Garlands { get; set; } = 1;
    public bool AutoPlow { get; set; } = true;
    public string? City { get; set; }
    public double CityLat { get; set; }
    public double CityLon { get; set; }
    public string? CityTz { get; set; }
    public double WeatherX { get; set; } = double.NaN;
    public double WeatherY { get; set; } = double.NaN;
    public double RemoteX { get; set; } = double.NaN;
    public double RemoteY { get; set; } = double.NaN;
}
