using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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

        _viewModel = new MainViewModel(
            new LauncherService(
                new ConfigStore(configDirectory),
                new AppLocator(),
                new ShortcutScanner()),
            coverImageService: new CoverImageService(
                Path.Combine(configDirectory, "covers")));

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

        WindowHelper.EnableDarkTitleBar(this);

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
        var isGameSection = _viewModel.SelectedNav is
            NavSection.Game or NavSection.Recent;
        var draft = new AppEntry
        {
            Name = "新应用",
            Group = isGameSection ? AppGroup.Game : AppGroup.Ide
        };

        var dialog = new EditAppDialog(draft, "添加应用", lockGroup: isGameSection)
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
        if (item.IsSteamLibrary)
        {
            var steamWindow = new SteamLibraryWindow
            {
                Owner = this
            };
            steamWindow.ShowDialog();
            return;
        }

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

    // ============ 拖拽排序（原 GameView/OfficeView 逻辑，侧边栏重构后收拢到主窗口） ============

    private ScrollViewer? _scrollViewer;
    private double _scrollTarget;
    private DispatcherTimer? _scrollTimer;

    private void ListBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ListBox listBox)
        {
            return;
        }

        var scrollViewer = FindAncestor<ScrollViewer>(listBox);
        if (scrollViewer is null)
        {
            return;
        }

        if (_scrollViewer != scrollViewer)
        {
            _scrollViewer = scrollViewer;
            _scrollTarget = scrollViewer.VerticalOffset;
            _scrollTimer?.Stop();
        }

        _scrollTarget = Math.Max(0,
            Math.Min(scrollViewer.ScrollableHeight,
                     _scrollTarget - e.Delta));

        if (_scrollTimer is null)
        {
            _scrollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _scrollTimer.Tick += OnScrollTick;
        }

        _scrollTimer.Start();
        e.Handled = true;
    }

    private void OnScrollTick(object? sender, EventArgs e)
    {
        if (_scrollViewer is null)
        {
            _scrollTimer?.Stop();
            return;
        }

        var diff = _scrollTarget - _scrollViewer.VerticalOffset;
        if (Math.Abs(diff) < 0.5)
        {
            _scrollViewer.ScrollToVerticalOffset(_scrollTarget);
            _scrollTimer?.Stop();
            return;
        }

        _scrollViewer.ScrollToVerticalOffset(
            _scrollViewer.VerticalOffset + diff * 0.2);
    }

    private Point _dragStartPoint;

    private void ListBox_PreviewMouseLeftButtonDown(
        object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
    }

    private void ListBox_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed ||
            sender is not ListBox listBox)
        {
            return;
        }

        var item = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (item?.DataContext is not AppItemViewModel)
        {
            return;
        }

        var currentPosition = e.GetPosition(null);
        if (Math.Abs(currentPosition.X - _dragStartPoint.X) <
                SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(currentPosition.Y - _dragStartPoint.Y) <
                SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        DragDrop.DoDragDrop(
            listBox,
            new DataObject(typeof(AppItemViewModel), item.DataContext),
            DragDropEffects.Move);
    }

    private void ListBox_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(AppItemViewModel))
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void ListBox_Drop(object sender, DragEventArgs e)
    {
        if (sender is not ListBox listBox ||
            !e.Data.GetDataPresent(typeof(AppItemViewModel)))
        {
            return;
        }

        var source = e.Data.GetData(typeof(AppItemViewModel)) as AppItemViewModel;
        var target = GetItemAtPoint(listBox, e.GetPosition(listBox))
            as AppItemViewModel;
        if (source is null)
        {
            return;
        }

        if (target is null)
        {
            _viewModel.MoveToEnd(source);
        }
        else
        {
            _viewModel.MoveBefore(source, target);
        }
    }

    private static object? GetItemAtPoint(ListBox listBox, Point point)
    {
        var hit = VisualTreeHelper.HitTest(listBox, point);
        return FindAncestor<ListBoxItem>(hit?.VisualHit)?.DataContext;
    }

    private static T? FindAncestor<T>(DependencyObject? current)
        where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
