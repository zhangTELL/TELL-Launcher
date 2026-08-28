using System.Diagnostics;
using System.IO;
using System.Windows;
using TELLLauncher.Services;
using TELLLauncher.ViewModels;

namespace TELLLauncher.Views;

public partial class SteamLibraryWindow : Window
{
    private readonly SteamLibraryService _service;
    private readonly CoverImageService _coverService;
    private readonly SteamGridDbService _steamGridDbService;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public SteamLibraryWindow(SteamLibraryService? service = null)
    {
        InitializeComponent();
        _service = service ?? new SteamLibraryService();

        // 两个封面服务内部各自持有 HttpClient，必须提升为实例字段复用。
        // 若在每次刷新时新建，连接会不断累积并最终耗尽端口。
        var coverDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TELL Launcher",
            "covers");
        _coverService = new CoverImageService(coverDirectory);
        _steamGridDbService = new SteamGridDbService(
            Path.Combine(coverDirectory, "steamgriddb"));

        WindowHelper.EnableDarkTitleBar(this);
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        // 窗口打开时的自动刷新与手动点击可能并发，加锁保证同一时刻只有一次扫描在跑，
        // 也避免慢的那一次返回后覆盖掉新的结果
        await _refreshLock.WaitAsync();
        try
        {
            var games = await Task.Run(() => _service.ScanInstalledGames());
            GameList.ItemsSource = games
                .Select(game => new SteamGameItemViewModel(
                    game,
                    _coverService,
                    _steamGridDbService))
                .ToList();
            CountText.Text = $"已安装 {games.Count} 个游戏";
            EmptyText.Visibility = games.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            // 这里若让异常逃逸，会沿 async void 直接崩掉进程
            CountText.Text = "读取游戏库失败";
            MessageBox.Show(
                this,
                $"读取 Steam 游戏库失败：\n\n{ex.Message}",
                "读取失败",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private void GameCard_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not SteamGameItemViewModel game)
        {
            return;
        }

        var confirm = MessageBox.Show(
            this,
            $"确定要启动 {game.Name} 吗？",
            "确认启动",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = $"steam://rungameid/{game.AppId}",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"启动失败：{ex.Message}",
                "启动失败",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
