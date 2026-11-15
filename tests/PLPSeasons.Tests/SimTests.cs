using PLPSeasons.Core;
using Xunit;

public class SimTests
{
    static Simulation Winter(int sec, params (long, RectI, double)[] surfaces)
    {
        var s = new Simulation(1920, 1080, 7) { Season = Season.Winter, Intensity = 1.5 };
        s.SetSurfaces(surfaces);
        for (int i = 0; i < sec * 60; i++) s.Step(1 / 60.0);
        return s;
    }

    [Fact] public void SnowFallsAndFillsTheScreen()
    {
        var s = Winter(5);
        Assert.InRange(s.P.Count(p => p.Alive && p.Kind == Kind.Flake), s.Target * 0.8, s.Target * 1.3);
        Assert.True(s.P.Where(p => p.Kind == Kind.Flake).Max(p => p.Y) > 800);
    }

    [Fact] public void SnowPilesOnWindowTopAndFloor()
    {
        var win = new RectI(400, 300, 1300, 900);
        var s = Winter(40, (1, win, 8), (-1, new RectI(0, 1032, 1920, 1081), 0));
        var top = s.Surfaces[1];
        Assert.True(top.Depth.Max() > 4, $"window snow {top.Depth.Max()}");
        Assert.True(s.Surfaces[-1].Depth.Max() > 4);
        Assert.True(top.Depth[0] <= top.Depth[top.Depth.Length / 2]);         // thinner at the rounded corner
        Assert.All(top.Depth, d => Assert.True(d <= top.MaxDepth + 0.01));
    }

    [Fact] public void SnowFallsInFrontOfWindows()
    {
        var s = Winter(10, (1, new RectI(200, 200, 1700, 1000), 8));
        Assert.True(s.P.Count(p => p.Alive && p.Kind == Kind.Flake && p.Y > 400 && p.Y < 900 && p.X > 300 && p.X < 1600) > 100);
    }

    [Fact] public void ClosingAWindowDropsItsSnow()
    {
        var s = Winter(30, (1, new RectI(400, 300, 1300, 900), 8));
        s.SetSurfaces(Array.Empty<(long, RectI, double)>());
        Assert.False(s.Surfaces.ContainsKey(1));
        Assert.True(s.P.Count(p => p.Kind == Kind.Crumb && p.Alive) > 20);
        for (int i = 0; i < 240; i++) s.Step(1 / 60.0);
        Assert.Equal(0, s.P.Count(p => p.Kind == Kind.Crumb && p.Alive));
    }

    [Fact] public void MovingAWindowDropsItsSnowButCoverChangeKeepsIt()
    {
        var s = Winter(30, (1, new RectI(400, 300, 1300, 900), 8));
        double before = s.Surfaces[1].Depth.Sum();
        s.SetSurfaces(new[] { (1L, new RectI(400, 300, 1000, 900), 8.0) });   // another window now covers the right part
        Assert.True(s.Surfaces[1].Depth.Sum() > before * 0.4);
        s.SetSurfaces(new[] { (1L, new RectI(400, 350, 1000, 950), 8.0) });   // dragged down
        Assert.True(s.Surfaces[1].Depth.Sum() < 1);
    }

    [Fact] public void SnowMeltsOutOfWinter()
    {
        var s = Winter(30, (1, new RectI(400, 300, 1300, 900), 8));
        s.Season = Season.Rain;
        for (int i = 0; i < 60 * 30; i++) s.Step(1 / 60.0);
        Assert.True(s.Surfaces[1].Depth.Max() < 0.5);
    }

    [Fact] public void VisibleTopsCutsCoveredParts()
    {
        var front = (2L, new RectI(800, 200, 1200, 700));
        var back = (1L, new RectI(400, 300, 1600, 900));
        var tops = Simulation.VisibleTops(new[] { front, back });
        Assert.Equal(2, tops.Count);
        var b = tops.Single(t => t.id == 1).rect;
        Assert.True(b.Right <= 800 || b.Left >= 1200);
    }

    [Fact] public void OtherSeasonsHaveParticles()
    {
        foreach (var season in new[] { Season.Rain, Season.Storm, Season.Autumn, Season.Spring, Season.Summer })
        {
            var s = new Simulation(1920, 1080, 3) { Season = season };
            for (int i = 0; i < 300; i++) s.Step(1 / 60.0);
            Assert.True(s.P.Count(p => p.Alive) > 5, season.ToString());
        }
    }

    [Fact] public void StormFlashes()
    {
        var s = new Simulation(1920, 1080, 3) { Season = Season.Storm };
        double max = 0;
        for (int i = 0; i < 60 * 25; i++) { s.Step(1 / 60.0); max = Math.Max(max, s.Flash); }
        Assert.True(max > 0.5);
    }

    [Fact] public void PhasesAndUpdates()
    {
        Assert.Equal(Phase.Night, Seasons.PhaseAt(new DateTime(2026, 1, 1, 23, 0, 0)));
        Assert.Equal(Phase.Morning, Seasons.PhaseAt(new DateTime(2026, 1, 1, 7, 0, 0)));
        Assert.Equal(Season.Winter, Seasons.FromDate(new DateTime(2026, 12, 5)));
        Assert.True(Updates.IsNewer("0.5.1", "0.5.0"));
        Assert.False(Updates.IsNewer("0.5.0", "0.5.0"));
        Assert.NotNull(Updates.Parse("{\"version\":\"0.6.0\",\"url\":\"https://plp-shop.com/download.html\"}"));
        Assert.Null(Updates.Parse("{\"version\":\"0.6.0\",\"url\":\"http://evil\"}"));
    }
}

public class WeatherTests
{
    [Fact] public void ParsesForecastAndSearch()
    {
        var w = WeatherApi.ParseForecast("{\"timezone\":\"Europe/Berlin\",\"current\":{\"temperature_2m\":-3.2,\"apparent_temperature\":-7.1,\"weather_code\":73,\"wind_speed_10m\":4.5,\"precipitation\":0.6},\"daily\":{\"temperature_2m_max\":[-1.0],\"temperature_2m_min\":[-6.5]}}");
        Assert.NotNull(w);
        Assert.Equal(-3.2, w!.Temp); Assert.Equal(Season.Winter, w.Effect); Assert.Equal("Europe/Berlin", w.TimeZone);
        Assert.Null(WeatherApi.ParseForecast("{}"));
        var p = WeatherApi.ParseSearch("{\"results\":[{\"name\":\"Berlin\",\"country\":\"Germany\",\"latitude\":52.52,\"longitude\":13.41,\"timezone\":\"Europe/Berlin\"}]}");
        Assert.Single(p); Assert.Equal("Berlin, Germany", p[0].Name);
        Assert.Empty(WeatherApi.ParseSearch("{\"generationtime_ms\":1}"));
        Assert.Equal(Season.Storm, new WeatherNow(20, 20, 95, 1, 1, 22, 15, "UTC", DateTime.UtcNow).Effect);
        Assert.Null(new WeatherNow(20, 20, 0, 1, 0, 22, 15, "UTC", DateTime.UtcNow).Effect);
    }
}
