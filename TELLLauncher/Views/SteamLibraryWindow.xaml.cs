using System.Diagnostics;
using System.IO;
using System.Windows;
using TELLLauncher.Services;
using TELLLauncher.ViewModels;

namespace TELLLauncher.Views;

public partial class SteamLibraryWindow : Window
{
    private readonly SteamLibraryService _service;

    public SteamLibraryWindow(SteamLibraryService? service = null)
    {
        InitializeComponent();
        _service = service ?? new SteamLibraryService();
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
        var games = await Task.Run(() => _service.ScanInstalledGames());
        var coverDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TELL Launcher",
            "covers");
        var coverService = new CoverImageService(coverDirectory);
        GameList.ItemsSource = games
            .Select(game => new SteamGameItemViewModel(game, coverService))
            .ToList();
        CountText.Text = $"已安装 {games.Count} 个游戏";
        EmptyText.Visibility = games.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
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
