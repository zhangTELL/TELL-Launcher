using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using TELLLauncher.Models;
using TELLLauncher.Services;
using TELLLauncher.ViewModels;
using TELLLauncher.Views;

namespace TELLLauncher;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly DispatcherTimer _notificationTimer;

    public MainWindow()
    {
        InitializeComponent();

        var configDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TELL Launcher");

        _viewModel = new MainViewModel(new LauncherService(
            new ConfigStore(configDirectory),
            new AppLocator(),
            new ShortcutScanner()));

        DataContext = _viewModel;

        _notificationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(4)
        };
        _notificationTimer.Tick += OnNotificationTimerTick;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.EditRequested += OnEditRequested;
        _viewModel.LocateRequested += OnEditRequested;
        _viewModel.OpenDetailRequested += OnOpenDetailRequested;

        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoadAsync();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.Save();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.RefreshGamesAsync();
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        var isGameTab = _viewModel.SelectedTabIndex == 1;
        var draft = new AppEntry
        {
            Name = "新应用",
            Group = isGameTab ? AppGroup.Game : AppGroup.Ide
        };

        var dialog = new EditAppDialog(draft, "添加应用", lockGroup: isGameTab)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            _viewModel.AddApp(draft);
        }
    }

    private void ManageButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ToggleManage();
    }

    private void OnEditRequested(AppItemViewModel item)
    {
        var model = item.Model;
        var draft = new AppEntry
        {
            Id = model.Id,
            Name = model.Name,
            TargetPath = model.TargetPath,
            IconPath = model.IconPath,
            DetailImagePath = model.DetailImagePath,
            Details = model.Details,
            Group = model.Group,
            Order = model.Order,
            IsHidden = model.IsHidden,
            IsManual = model.IsManual
        };

        var dialog = new EditAppDialog(draft, "编辑应用")
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            _viewModel.UpdateApp(item, draft);
        }
    }

    private void OnOpenDetailRequested(AppItemViewModel item)
    {
        var detailWindow = new AppDetailWindow(_viewModel, item)
        {
            Owner = this
        };
        detailWindow.ShowDialog();
    }

    private void OnViewModelPropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if ((e.PropertyName == nameof(MainViewModel.NotificationText) ||
             e.PropertyName == nameof(MainViewModel.HasNotification)) &&
            _viewModel.HasNotification)
        {
            _notificationTimer.Stop();
            _notificationTimer.Start();
        }
    }

    private void OnNotificationTimerTick(object? sender, EventArgs e)
    {
        _notificationTimer.Stop();
        _viewModel.ClearNotification();
    }
}
