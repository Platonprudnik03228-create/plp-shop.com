using System;
using System.IO;
using System.Text;
using PLPSeasons.Core;

namespace PLPSeasons;

/// <summary>Sets the same painted worlds as the Mac app, changing with the time of day. Restores the user's wallpaper when switched off.</summary>
sealed class WallpaperManager
{
    readonly AppSettings s;
    string? current;
    public WallpaperManager(AppSettings settings) { s = settings; }

    static string Folder => Path.Combine(AppContext.BaseDirectory, "Wallpapers");

    public static string? FileFor(Season season, Phase phase)
    {
        string world = season switch { Season.Winter => "cabin", Season.Autumn => "park", Season.Spring => "blossom", Season.Summer => "meadow", _ => "city" };
        foreach (var w in new[] { world, "cabin" })
        {
            var f = Path.Combine(Folder, $"{w}_{phase.Name()}.jpg");
            if (File.Exists(f)) return f;
        }
        return null;
    }

    public void Update(Season season)
    {
        if (!s.Wallpaper) return;
        var f = FileFor(season, Seasons.PhaseAt(DateTime.Now));
        if (f == null || f == current) return;
        if (s.OriginalWallpaper == null)
        {
            var sb = new StringBuilder(520);
            if (Native.SystemParametersInfo(Native.SPI_GETDESKWALLPAPER, (uint)sb.Capacity, sb, 0)) { s.OriginalWallpaper = sb.ToString(); Store.Save(s); }
        }
        // Windows keeps a cache of the wallpaper file; copy to a stable path under AppData so the original stays untouched
        try
        {
            Directory.CreateDirectory(Store.Dir);
            var dst = Path.Combine(Store.Dir, "wallpaper_" + Path.GetFileName(f));
            if (!File.Exists(dst)) File.Copy(f, dst, true);
            Native.SystemParametersInfo(Native.SPI_SETDESKWALLPAPER, 0, dst, Native.SPIF_UPDATEINIFILE | Native.SPIF_SENDCHANGE);
            current = f;
        }
        catch { }
    }

    public void Restore()
    {
        current = null;
        if (string.IsNullOrEmpty(s.OriginalWallpaper)) return;
        Native.SystemParametersInfo(Native.SPI_SETDESKWALLPAPER, 0, s.OriginalWallpaper, Native.SPIF_UPDATEINIFILE | Native.SPIF_SENDCHANGE);
        s.OriginalWallpaper = null; Store.Save(s);
    }
}
