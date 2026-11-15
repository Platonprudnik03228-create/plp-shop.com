using System.Text.Json;

namespace PLPSeasons.Core;

public sealed record UpdateInfo(string Version, string Url, string? Notes);

public static class Updates
{
    public const string Feed = "https://plp-shop.com/windows/version.json";

    public static UpdateInfo? Parse(string json)
    {
        try
        {
            using var d = JsonDocument.Parse(json);
            var r = d.RootElement;
            var v = r.GetProperty("version").GetString(); var u = r.GetProperty("url").GetString();
            if (string.IsNullOrWhiteSpace(v) || string.IsNullOrWhiteSpace(u) || !u.StartsWith("https://")) return null;
            return new UpdateInfo(v, u, r.TryGetProperty("notes", out var n) ? n.GetString() : null);
        }
        catch { return null; }
    }

    public static bool IsNewer(string remote, string local) =>
        Version.TryParse(remote, out var a) && Version.TryParse(local, out var b) && a > b;
}
