using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;
using PLPSeasons.Core;

namespace PLPSeasons;

static class Store
{
    public static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PLP Seasons");
    static string FilePath => Path.Combine(Dir, "settings.json");
    static readonly JsonSerializerOptions Opt = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    public static AppSettings Load()
    {
        try { if (File.Exists(FilePath)) return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Opt) ?? new(); } catch { }
        return new AppSettings();
    }
    public static void Save(AppSettings s)
    {
        try { Directory.CreateDirectory(Dir); File.WriteAllText(FilePath, JsonSerializer.Serialize(s, Opt)); } catch { }
    }

    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public static void SetAutostart(bool on)
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(RunKey, true) ?? Registry.CurrentUser.CreateSubKey(RunKey);
            if (on) k.SetValue("PLP Seasons", $"\"{Environment.ProcessPath}\""); else k.DeleteValue("PLP Seasons", false);
        }
        catch { }
    }
}

static class L
{
    public static readonly bool Ru = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName is "ru" or "uk" or "be" or "kk";
    public static string T(string ru, string en) => Ru ? ru : en;
    public static string Name(Season s) => s switch
    {
        Season.Winter => T("Зима — снег", "Winter — snow"),
        Season.Rain => T("Дождь", "Rain"),
        Season.Storm => T("Гроза", "Thunderstorm"),
        Season.Autumn => T("Осень — листья", "Autumn — leaves"),
        Season.Spring => T("Весна — сакура", "Spring — sakura"),
        _ => T("Лето", "Summer"),
    };
}
