using System;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using PLPSeasons.Core;

namespace PLPSeasons;

/// <summary>Once at start and then every 6 hours: if the site has a newer version, show a notification that opens the download.</summary>
static class UpdateChecker
{
    public static string Current => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public static async Task<UpdateInfo?> Check()
    {
        try
        {
            var json = await Http.GetStringAsync(Updates.Feed + "?v=" + Current);
            var u = Updates.Parse(json);
            return u != null && Updates.IsNewer(u.Version, Current) ? u : null;
        }
        catch { return null; }
    }
}
