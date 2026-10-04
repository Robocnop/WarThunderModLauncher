using System.Collections.Specialized;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WTModLauncher.ViewModels;

namespace WTModLauncher;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        _vm.LogLines.CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Add) LogScroll.ScrollToEnd();
        };
        Loaded += async (_, _) =>
        {
            await _vm.InitializeAsync();
            if (Environment.GetEnvironmentVariable("WTML_SCREENSHOT") is { Length: > 0 } shot) await SaveScreenshotsAndExit(shot);
        };
    }

    /// <summary>Dev aid: WTML_SCREENSHOT=dir renders each page to PNG then exits (used to review the UI headlessly).</summary>
    private async Task SaveScreenshotsAndExit(string dir)
    {
        Directory.CreateDirectory(dir);
        foreach (var page in new[] { "mods", "settings", "log", "about" })
        {
            _vm.CurrentPage = page;
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            var bmp = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(this);
            var png = new PngBitmapEncoder();
            png.Frames.Add(BitmapFrame.Create(bmp));
            await using var fs = File.Create(Path.Combine(dir, $"{page}-{_vm.Language}.png"));
            png.Save(fs);
        }
        Close();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
