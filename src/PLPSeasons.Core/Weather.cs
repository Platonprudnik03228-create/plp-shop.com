using System.Globalization;
using System.Text.Json;

namespace PLPSeasons.Core;

public sealed record Place(string Name, double Lat, double Lon, string TimeZone);
public sealed record WeatherNow(double Temp, double Feels, int Code, double Wind, double Precip, double Max, double Min, string TimeZone, DateTime Fetched)
{
    /// <summary>WMO weather code → the season effect that fits it (snow, rain, storm) or null when it is dry.</summary>
    public Season? Effect => Code switch
    {
        71 or 73 or 75 or 77 or 85 or 86 => Season.Winter,
        95 or 96 or 99 => Season.Storm,
        51 or 53 or 55 or 56 or 57 or 61 or 63 or 65 or 66 or 67 or 80 or 81 or 82 => Season.Rain,
        _ => null,
    };
}

/// <summary>Free weather from Open-Meteo (no key). Parsing is kept here so it can be tested without a network.</summary>
public static class WeatherApi
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    public static string ForecastUrl(Place p) =>
        $"https://api.open-meteo.com/v1/forecast?latitude={p.Lat.ToString(Inv)}&longitude={p.Lon.ToString(Inv)}" +
        "&current=temperature_2m,apparent_temperature,weather_code,wind_speed_10m,precipitation&daily=temperature_2m_max,temperature_2m_min&timezone=auto&forecast_days=1&wind_speed_unit=ms";
    public static string SearchUrl(string name, string lang) =>
        $"https://geocoding-api.open-meteo.com/v1/search?count=5&language={lang}&name={Uri.EscapeDataString(name)}";

    public static WeatherNow? ParseForecast(string json)
    {
        try
        {
            using var d = JsonDocument.Parse(json); var r = d.RootElement; var c = r.GetProperty("current"); var day = r.GetProperty("daily");
            return new WeatherNow(c.GetProperty("temperature_2m").GetDouble(), c.GetProperty("apparent_temperature").GetDouble(),
                c.GetProperty("weather_code").GetInt32(), c.GetProperty("wind_speed_10m").GetDouble(), c.GetProperty("precipitation").GetDouble(),
                day.GetProperty("temperature_2m_max")[0].GetDouble(), day.GetProperty("temperature_2m_min")[0].GetDouble(),
                r.TryGetProperty("timezone", out var tz) ? tz.GetString() ?? "UTC" : "UTC", DateTime.UtcNow);
        }
        catch { return null; }
    }

    public static List<Place> ParseSearch(string json)
    {
        var res = new List<Place>();
        try
        {
            using var d = JsonDocument.Parse(json);
            if (!d.RootElement.TryGetProperty("results", out var arr)) return res;
            foreach (var e in arr.EnumerateArray())
            {
                var name = e.GetProperty("name").GetString() ?? "";
                if (e.TryGetProperty("country", out var co)) name += ", " + co.GetString();
                res.Add(new Place(name, e.GetProperty("latitude").GetDouble(), e.GetProperty("longitude").GetDouble(),
                    e.TryGetProperty("timezone", out var tz) ? tz.GetString() ?? "UTC" : "UTC"));
            }
        }
        catch { }
        return res;
    }

    public static string Icon(int code, bool night) => code switch
    {
        0 => night ? "🌙" : "☀️", 1 or 2 => night ? "☁️" : "⛅", 3 => "☁️", 45 or 48 => "🌫️",
        >= 51 and <= 67 or >= 80 and <= 82 => "🌧️", >= 71 and <= 77 or 85 or 86 => "🌨️", >= 95 => "⛈️", _ => "🌡️",
    };

    public static string Describe(int code, bool ru) => code switch
    {
        0 => ru ? "Ясно" : "Clear", 1 => ru ? "Почти ясно" : "Mostly clear", 2 => ru ? "Переменная облачность" : "Partly cloudy",
        3 => ru ? "Пасмурно" : "Overcast", 45 or 48 => ru ? "Туман" : "Fog",
        51 or 53 or 55 or 56 or 57 => ru ? "Морось" : "Drizzle", 61 or 63 or 80 or 81 => ru ? "Дождь" : "Rain",
        65 or 82 => ru ? "Ливень" : "Heavy rain", 66 or 67 => ru ? "Ледяной дождь" : "Freezing rain",
        71 or 73 or 77 or 85 => ru ? "Снег" : "Snow", 75 or 86 => ru ? "Сильный снег" : "Heavy snow",
        >= 95 => ru ? "Гроза" : "Thunderstorm", _ => "",
    };
}
