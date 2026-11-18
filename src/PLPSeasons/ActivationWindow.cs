using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PLPSeasons.Core;

namespace PLPSeasons;

/// 0.5.3: PLP Seasons is bought once ($4.99, Mac + Windows, forever). Without a key this window comes up instead of the effects.
sealed class ActivationWindow : Window
{
    public const string BuyUrl = "https://plp-shop.com/?buy=1";
    public string? Key { get; private set; }
    readonly TextBox box;
    readonly TextBlock err;

    public ActivationWindow()
    {
        Title = "PLP Seasons"; Width = 520; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen; Topmost = true;
        Background = new SolidColorBrush(Color.FromRgb(0x15, 0x1B, 0x26));
        var gold = new SolidColorBrush(Color.FromRgb(0xF2, 0xD3, 0x8C));
        var sp = new StackPanel { Margin = new Thickness(36, 28, 36, 26) };
        sp.Children.Add(new TextBlock { Text = "❄  PLP Seasons", FontSize = 28, FontWeight = FontWeights.Bold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center });
        sp.Children.Add(new TextBlock { Text = L.T("Активируй приложение — один раз и навсегда", "Activate the app — once and for good"), FontSize = 15, Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 18) });
        foreach (var t in new[] {
            L.T("Живая погода: дождь, снег, гроза и туман — как на улице", "Live weather: rain, snow, storms and fog as they happen"),
            L.T("Все сезоны, снег на окнах, гирлянды и снеговики", "All seasons, snow on windows, garlands and snowmen"),
            L.T("Обои для каждого времени суток", "Wallpapers for every time of day"),
            L.T("Один ключ — для Mac и Windows", "One key for Mac and Windows") })
            sp.Children.Add(new TextBlock { Text = "✦  " + t, FontSize = 14, Foreground = Brushes.White, Margin = new Thickness(8, 3, 0, 3), TextWrapping = TextWrapping.Wrap });
        sp.Children.Add(new TextBlock { Text = L.T("$4.99 один раз · навсегда", "$4.99 once · forever"), FontSize = 14, Foreground = gold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 16, 0, 12) });

        var buy = new Button { Content = L.T("Купить — $4.99", "Buy — $4.99"), FontSize = 16, FontWeight = FontWeights.SemiBold, Padding = new Thickness(18, 10, 18, 10), Background = new SolidColorBrush(Color.FromRgb(0xF2, 0xAD, 0x33)), Foreground = Brushes.Black, BorderThickness = new Thickness(0) };
        buy.Click += (_, _) => { try { Process.Start(new ProcessStartInfo(BuyUrl) { UseShellExecute = true }); } catch { } };
        sp.Children.Add(buy);

        sp.Children.Add(new TextBlock { Text = L.T("Уже купил? Вставь ключ (он всегда есть на plp-shop.com → «Мой ключ»):", "Already bought? Paste your key (always on plp-shop.com → “My key”):"), FontSize = 12, Foreground = new SolidColorBrush(Color.FromArgb(190, 255, 255, 255)), Margin = new Thickness(0, 20, 0, 6), TextWrapping = TextWrapping.Wrap });
        box = new TextBox { FontFamily = new FontFamily("Consolas"), FontSize = 12, Height = 60, TextWrapping = TextWrapping.Wrap, AcceptsReturn = false, Padding = new Thickness(6) };
        sp.Children.Add(box);
        err = new TextBlock { Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x8A, 0x80)), FontSize = 12, Margin = new Thickness(0, 4, 0, 0) };
        sp.Children.Add(err);
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        var quit = new Button { Content = L.T("Выйти", "Quit"), Padding = new Thickness(14, 6, 14, 6), Margin = new Thickness(0, 0, 8, 0) };
        quit.Click += (_, _) => { DialogResult = false; };
        var ok = new Button { Content = L.T("Активировать", "Activate"), Padding = new Thickness(14, 6, 14, 6), IsDefault = true };
        ok.Click += (_, _) => Activate_();
        row.Children.Add(quit); row.Children.Add(ok);
        sp.Children.Add(row);
        Content = sp;
    }

    void Activate_()
    {
        var k = License.Clean(box.Text);
        if (!License.Valid(k)) { err.Text = L.T("Ключ не подошёл. Скопируй его целиком, начиная с PLP-", "That key did not work. Copy all of it, starting with PLP-"); return; }
        Key = k; DialogResult = true;
    }

    public static void ShowKey(string key)
    {
        var w = new Window { Title = L.T("Мой ключ PLP Seasons", "My PLP Seasons key"), Width = 520, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterScreen, Topmost = true };
        var sp = new StackPanel { Margin = new Thickness(20) };
        sp.Children.Add(new TextBlock { Text = L.T("Этот же ключ подходит для Mac и Windows. Он также есть на plp-shop.com → «Мой ключ».", "The same key works on Mac and Windows. It is also on plp-shop.com → “My key”."), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) });
        var tb = new TextBox { Text = "PLP-" + key, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Consolas"), Height = 60 };
        sp.Children.Add(tb);
        var copy = new Button { Content = L.T("Скопировать", "Copy"), Padding = new Thickness(14, 6, 14, 6), Margin = new Thickness(0, 10, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };
        copy.Click += (_, _) => { try { Clipboard.SetText(tb.Text); copy.Content = L.T("Скопировано ✓", "Copied ✓"); } catch { } };
        sp.Children.Add(copy);
        w.Content = sp; w.ShowDialog();
    }
}
