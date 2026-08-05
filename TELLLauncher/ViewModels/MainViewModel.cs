using System.Collections.ObjectModel;
using TELLLauncher.Models;
using TELLLauncher.Services;

namespace TELLLauncher.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly LauncherService _launcherService;
    private readonly IProcessLauncher _processLauncher;
    private LauncherConfig _config = new();
    private int _selectedTabIndex;
    private string _statusText = string.Empty;
    private string _notificationText = string.Empty;
    private string _searchText = string.Empty;
    private bool _hasNotification;
    private bool _isSearching;
    private bool _isManaging;

    public MainViewModel(
        LauncherService launcherService,
        IProcessLauncher? processLauncher = null)
    {
        _launcherService = launcherService;
        _processLauncher = processLauncher ?? new ProcessLauncher();
        LaunchCommand = new RelayCommand(parameter => Launch(parameter as AppItemViewModel));
        EditCommand = new RelayCommand(parameter =>
        {
            if (parameter is AppItemViewModel item)
            {
                EditRequested?.Invoke(item);
            }
        });
        RemoveCommand = new RelayCommand(
            parameter => RemoveApp(parameter as AppItemViewModel));
        MoveToIdeCommand = new RelayCommand(
            parameter => MoveApp(parameter as AppItemViewModel, AppGroup.Ide));
        MoveToAiToolCommand = new RelayCommand(
            parameter => MoveApp(parameter as AppItemViewModel, AppGroup.AiTool));
        MoveToGameCommand = new RelayCommand(
            parameter => MoveApp(parameter as AppItemViewModel, AppGroup.Game));
        MoveUpCommand = new RelayCommand(
            parameter => MoveUp(parameter as AppItemViewModel));
        MoveDownCommand = new RelayCommand(
            parameter => MoveDown(parameter as AppItemViewModel));
    }

    public event Action<AppItemViewModel>? EditRequested;

    public event Action<AppItemViewModel>? LocateRequested;

    public ObservableCollection<AppItemViewModel> IdeApps { get; } = new();

    public ObservableCollection<AppItemViewModel> AiToolApps { get; } = new();

    public ObservableCollection<AppItemViewModel> GameApps { get; } = new();

    public ObservableCollection<AppItemViewModel> SearchResults { get; } = new();

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                RefreshCollections();
            }
        }
    }

    public bool IsSearching
    {
        get => _isSearching;
        private set
        {
            if (SetProperty(ref _isSearching, value))
            {
                OnPropertyChanged(nameof(IsNotSearching));
            }
        }
    }

    public bool IsNotSearching => !IsSearching;

    public bool IsManaging
    {
        get => _isManaging;
        set
        {
            if (SetProperty(ref _isManaging, value))
            {
                OnPropertyChanged(nameof(IsNotManaging));
            }
        }
    }

    public bool IsNotManaging => !IsManaging;

    public string NotificationText
    {
        get => _notificationText;
        private set => SetProperty(ref _notificationText, value);
    }

    public bool HasNotification
    {
        get => _hasNotification;
        private set => SetProperty(ref _hasNotification, value);
    }

    public RelayCommand LaunchCommand { get; }

    public RelayCommand EditCommand { get; }

    public RelayCommand RemoveCommand { get; }

    public RelayCommand MoveToIdeCommand { get; }

    public RelayCommand MoveToAiToolCommand { get; }

    public RelayCommand MoveToGameCommand { get; }

    public RelayCommand MoveUpCommand { get; }

    public RelayCommand MoveDownCommand { get; }

    public void Load()
    {
        _config = _launcherService.LoadOrCreate();
        RefreshCollections();
    }

    public void RefreshGames()
    {
        _launcherService.RefreshGames(_config);
        RefreshCollections();
    }

    public void Save()
    {
        _launcherService.Save(_config);
    }

    public void Launch(AppItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        if (item.IsMissing)
        {
            if (LocateRequested is null)
            {
                ShowNotification("未找到程序，请重新指定路径");
            }
            else
            {
                LocateRequested(item);
            }
            return;
        }

        var result = _processLauncher.Launch(item.TargetPath!);
        if (!result.Success)
        {
            ShowNotification($"启动失败：{result.Message}");
        }
    }

    public void ClearNotification()
    {
        NotificationText = string.Empty;
        HasNotification = false;
    }

    public void AddApp(AppEntry draft)
    {
        if (string.IsNullOrWhiteSpace(draft.Name))
        {
            return;
        }

        draft.Id = Guid.NewGuid().ToString("N");
        draft.IsManual = true;
        draft.Order = NextOrder(draft.Group);
        _config.Apps.Add(draft);
        Save();
        RefreshCollections();
    }

    public void UpdateApp(AppItemViewModel item, AppEntry draft)
    {
        var model = FindModel(item);
        if (model is null)
        {
            return;
        }

        model.Name = draft.Name;
        model.TargetPath = draft.TargetPath;
        model.IconPath = draft.IconPath;
        model.IsManual = draft.IsManual || model.IsManual;

        if (model.Group != draft.Group)
        {
            var nextOrder = NextOrder(draft.Group);
            model.Group = draft.Group;
            model.Order = nextOrder;
        }

        Save();
        RefreshCollections();
    }

    public void RemoveApp(AppItemViewModel? item)
    {
        var model = FindModel(item);
        if (model is null)
        {
            return;
        }

        if (model.Group == AppGroup.Game &&
            !string.IsNullOrWhiteSpace(model.TargetPath))
        {
            var key = model.TargetPath!;
            if (!_config.HiddenGamePaths.Contains(
                    key,
                    StringComparer.OrdinalIgnoreCase))
            {
                _config.HiddenGamePaths.Add(key);
            }
        }

        _config.Apps.Remove(model);
        Save();
        RefreshCollections();
    }

    public void MoveApp(AppItemViewModel? item, AppGroup targetGroup)
    {
        var model = FindModel(item);
        if (model is null || model.Group == targetGroup)
        {
            return;
        }

        var nextOrder = NextOrder(targetGroup);
        model.Group = targetGroup;
        model.Order = nextOrder;
        Save();
        RefreshCollections();
    }

    public void MoveUp(AppItemViewModel? item)
    {
        MoveByOffset(item, -1);
    }

    public void MoveDown(AppItemViewModel? item)
    {
        MoveByOffset(item, 1);
    }

    public void MoveBefore(AppItemViewModel source, AppItemViewModel target)
    {
        var sourceModel = FindModel(source);
        var targetModel = FindModel(target);
        if (sourceModel is null || targetModel is null ||
            sourceModel.Group != targetModel.Group)
        {
            return;
        }

        var ordered = _config.Apps
            .Where(app => app.Group == sourceModel.Group && !app.IsHidden)
            .OrderBy(app => app.Order)
            .ToList();
        var sourceIndex = ordered.FindIndex(app => app.Id == sourceModel.Id);
        if (sourceIndex < 0)
        {
            return;
        }

        ordered.RemoveAt(sourceIndex);
        var targetIndex = ordered.FindIndex(app => app.Id == targetModel.Id);
        if (targetIndex < 0)
        {
            targetIndex = ordered.Count;
        }

        ordered.Insert(targetIndex, sourceModel);
        for (var index = 0; index < ordered.Count; index++)
        {
            ordered[index].Order = index;
        }

        Save();
        RefreshCollections();
    }

    public void MoveToEnd(AppItemViewModel item)
    {
        var model = FindModel(item);
        if (model is null)
        {
            return;
        }

        var ordered = _config.Apps
            .Where(app => app.Group == model.Group && !app.IsHidden)
            .OrderBy(app => app.Order)
            .ToList();

        model.Order = ordered.Count == 0
            ? 0
            : ordered.Max(app => app.Order) + 1;
        NormalizeOrders(model.Group);

        Save();
        RefreshCollections();
    }

    public void ToggleManage()
    {
        IsManaging = !IsManaging;
    }

    private void ShowNotification(string message)
    {
        NotificationText = message;
        HasNotification = true;
    }

    private void RefreshCollections()
    {
        var searchText = SearchText?.Trim() ?? string.Empty;
        var isSearching = searchText.Length > 0;

        IsSearching = isSearching;
        IdeApps.Clear();
        AiToolApps.Clear();
        GameApps.Clear();
        SearchResults.Clear();

        if (isSearching)
        {
            var officeMatches = GetVisibleApps(AppGroup.Ide)
                .Concat(GetVisibleApps(AppGroup.AiTool))
                .Where(app => MatchesSearch(app, searchText))
                .OrderBy(app => app.Order);

            foreach (var app in officeMatches)
            {
                SearchResults.Add(new AppItemViewModel(app));
            }

            foreach (var app in GetVisibleApps(AppGroup.Game)
                         .Where(app => MatchesSearch(app, searchText)))
            {
                GameApps.Add(new AppItemViewModel(app));
            }

            StatusText = $"搜索结果 {SearchResults.Count + GameApps.Count} 个";
            return;
        }

        foreach (var app in GetVisibleApps(AppGroup.Ide))
        {
            IdeApps.Add(new AppItemViewModel(app));
        }

        foreach (var app in GetVisibleApps(AppGroup.AiTool))
        {
            AiToolApps.Add(new AppItemViewModel(app));
        }

        foreach (var app in GetVisibleApps(AppGroup.Game))
        {
            GameApps.Add(new AppItemViewModel(app));
        }

        StatusText = $"办公 {IdeApps.Count + AiToolApps.Count} 个 · 游戏 {GameApps.Count} 个";
    }

    private static bool MatchesSearch(AppEntry app, string searchText)
    {
        return app.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
               (app.TargetPath?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private void MoveByOffset(AppItemViewModel? item, int offset)
    {
        var model = FindModel(item);
        if (model is null)
        {
            return;
        }

        var ordered = _config.Apps
            .Where(app => app.Group == model.Group && !app.IsHidden)
            .OrderBy(app => app.Order)
            .ToList();
        var index = ordered.FindIndex(app => app.Id == model.Id);
        if (index < 0)
        {
            return;
        }

        var targetIndex = index + offset;
        if (targetIndex < 0 || targetIndex >= ordered.Count)
        {
            return;
        }

        (ordered[index].Order, ordered[targetIndex].Order) =
            (ordered[targetIndex].Order, ordered[index].Order);

        Save();
        RefreshCollections();
    }

    private AppEntry? FindModel(AppItemViewModel? item)
    {
        return item is null
            ? null
            : _config.Apps.FirstOrDefault(app => app.Id == item.Model.Id);
    }

    private int NextOrder(AppGroup group)
    {
        var maxOrder = _config.Apps
            .Where(app => app.Group == group && !app.IsHidden)
            .Select(app => app.Order)
            .DefaultIfEmpty(-1)
            .Max();

        return maxOrder + 1;
    }

    private void NormalizeOrders(AppGroup group)
    {
        var ordered = _config.Apps
            .Where(app => app.Group == group && !app.IsHidden)
            .OrderBy(app => app.Order)
            .ToList();

        for (var index = 0; index < ordered.Count; index++)
        {
            ordered[index].Order = index;
        }
    }

    private IEnumerable<AppEntry> GetVisibleApps(AppGroup group)
    {
        return _config.Apps
            .Where(app => app.Group == group && !app.IsHidden)
            .OrderBy(app => app.Order);
    }
}
