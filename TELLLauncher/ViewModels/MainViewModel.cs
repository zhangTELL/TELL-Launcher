using System.Collections.ObjectModel;
using TELLLauncher.Models;
using TELLLauncher.Services;

namespace TELLLauncher.ViewModels;

/// <summary>侧边栏导航分区。</summary>
public enum NavSection
{
    Ide,
    AiTool,
    Game,
    Recent
}

/// <summary>内容区展示状态。</summary>
/// <remarks>
/// 此前只有一个"当前集合是否为空"的布尔判断，导致首屏加载、真空分区、
/// 搜索无结果三种截然不同的情况共用一句"这里还空空如也"。
/// </remarks>
public enum ContentState
{
    /// <summary>正在加载或刷新，应显示骨架屏。</summary>
    Loading,

    /// <summary>有内容可展示。</summary>
    Ready,

    /// <summary>当前分区确实没有任何条目。</summary>
    Empty,

    /// <summary>搜索无匹配结果。</summary>
    NoSearchResult
}

/// <summary>通知级别，决定通知条的配色。</summary>
public enum NotificationKind
{
    Info,
    Success,
    Warning,
    Error
}

public sealed class MainViewModel : ObservableObject
{
    private readonly LauncherService _launcherService;
    private readonly IProcessLauncher _processLauncher;
    private readonly CoverImageService? _coverImageService;
    private readonly GameArtworkService? _gameArtworkService;
    private LauncherConfig _config = new();

    /// <summary>
    /// 标记 <see cref="_config"/> 是否已从磁盘加载。加载完成前 _config 仍是空配置，
    /// 此时写盘会用空白数据覆盖真实配置，因此所有保存入口都必须先经过此标志。
    /// </summary>
    private bool _isLoaded;

    /// <summary>
    /// 按 AppEntry.Id 缓存卡片视图模型。RefreshCollections 会被搜索框高频触发，
    /// 而 AppItemViewModel 的构造函数会同步提取两次图标（P/Invoke + 位图编解码），
    /// 每次重建都会把这份成本重复一遍，因此这里按 Id 复用实例。
    /// </summary>
    private readonly Dictionary<string, AppItemViewModel> _viewModels = new(StringComparer.Ordinal);

    private NavSection _selectedNav = NavSection.Ide;
    private string _statusText = string.Empty;
    private string _contentTitle = string.Empty;
    private string _contentSubtitle = string.Empty;
    private string _notificationText = string.Empty;
    private string _searchText = string.Empty;
    private bool _hasNotification;
    private bool _isSearching;
    private bool _isManaging;

    /// <summary>
    /// 初始即为 Loading：配置尚未从磁盘读出，此时若显示空状态文案，
    /// 用户会误以为程序没扫到任何东西。
    /// </summary>
    private ContentState _state = ContentState.Loading;

    private NotificationKind _notificationLevel = NotificationKind.Warning;

    public MainViewModel(
        LauncherService launcherService,
        IProcessLauncher? processLauncher = null,
        CoverImageService? coverImageService = null,
        GameArtworkService? gameArtworkService = null)
    {
        _launcherService = launcherService;
        _processLauncher = processLauncher ?? new ProcessLauncher();
        _coverImageService = coverImageService;
        _gameArtworkService = gameArtworkService;
        LaunchCommand = new RelayCommand(parameter => Launch(parameter as AppItemViewModel));
        OpenDetailCommand = new RelayCommand(parameter =>
        {
            if (parameter is AppItemViewModel item)
            {
                OpenDetailRequested?.Invoke(item);
            }
        });
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
        SelectNavCommand = new RelayCommand(parameter =>
        {
            if (parameter is NavSection nav)
            {
                SelectedNav = nav;
            }
        });
        ClearSearchCommand = new RelayCommand(_ => SearchText = string.Empty);
    }

    public event Action<AppItemViewModel>? EditRequested;

    public event Action<AppItemViewModel>? LocateRequested;

    public event Action<AppItemViewModel>? OpenDetailRequested;

    public ObservableCollection<AppItemViewModel> IdeApps { get; } = new();

    public ObservableCollection<AppItemViewModel> AiToolApps { get; } = new();

    public ObservableCollection<AppItemViewModel> GameApps { get; } = new();

    public ObservableCollection<AppItemViewModel> RecentApps { get; } = new();

    /// <summary>
    /// 界面实际绑定的集合：非搜索态为当前分区内容，搜索态为搜索结果。
    /// </summary>
    public ObservableCollection<AppItemViewModel> CurrentApps { get; } = new();

    public NavSection SelectedNav
    {
        get => _selectedNav;
        set
        {
            if (SetProperty(ref _selectedNav, value))
            {
                ApplyNavSelection();
            }
        }
    }

    public string ContentTitle
    {
        get => _contentTitle;
        private set => SetProperty(ref _contentTitle, value);
    }

    public string ContentSubtitle
    {
        get => _contentSubtitle;
        private set => SetProperty(ref _contentSubtitle, value);
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
                OnPropertyChanged(nameof(IsSearchEmpty));
                OnPropertyChanged(nameof(IsNotSearchEmpty));
                RefreshCollections();
            }
        }
    }

    public bool IsSearchEmpty => string.IsNullOrWhiteSpace(SearchText);

    /// <summary>搜索框有内容，用于显示清除按钮。</summary>
    public bool IsNotSearchEmpty => !IsSearchEmpty;

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

    /// <summary>内容区当前状态，决定显示骨架屏、列表还是空状态。</summary>
    public ContentState State
    {
        get => _state;
        private set
        {
            if (SetProperty(ref _state, value))
            {
                OnPropertyChanged(nameof(IsLoading));
                OnPropertyChanged(nameof(IsReady));
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(IsNoSearchResult));
            }
        }
    }

    public bool IsLoading => State == ContentState.Loading;

    public bool IsReady => State == ContentState.Ready;

    public bool IsEmpty => State == ContentState.Empty;

    public bool IsNoSearchResult => State == ContentState.NoSearchResult;

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

    /// <summary>通知级别。XAML 据此选择通知条配色。</summary>
    public NotificationKind NotificationLevel
    {
        get => _notificationLevel;
        private set
        {
            if (SetProperty(ref _notificationLevel, value))
            {
                OnPropertyChanged(nameof(IsInfoNotification));
                OnPropertyChanged(nameof(IsSuccessNotification));
                OnPropertyChanged(nameof(IsWarningNotification));
                OnPropertyChanged(nameof(IsErrorNotification));
            }
        }
    }

    public bool IsInfoNotification => NotificationLevel == NotificationKind.Info;

    public bool IsSuccessNotification => NotificationLevel == NotificationKind.Success;

    public bool IsWarningNotification => NotificationLevel == NotificationKind.Warning;

    public bool IsErrorNotification => NotificationLevel == NotificationKind.Error;

    public RelayCommand LaunchCommand { get; }

    public RelayCommand OpenDetailCommand { get; }

    public RelayCommand EditCommand { get; }

    public RelayCommand RemoveCommand { get; }

    public RelayCommand MoveToIdeCommand { get; }

    public RelayCommand MoveToAiToolCommand { get; }

    public RelayCommand MoveToGameCommand { get; }

    public RelayCommand MoveUpCommand { get; }

    public RelayCommand MoveDownCommand { get; }

    public RelayCommand SelectNavCommand { get; }

    /// <summary>清空搜索框，供 × 按钮与 Esc 键使用。</summary>
    public RelayCommand ClearSearchCommand { get; }

    public async Task LoadAsync()
    {
        State = ContentState.Loading;
        try
        {
            _config = await _launcherService.LoadOrCreateAsync();
            _isLoaded = true;
        }
        finally
        {
            // 加载失败也必须退出骨架屏，否则界面会永远停在 Loading。
            // 异常继续向上抛，由 App 的全局异常处理接管。
            RefreshCollections();
        }
    }

    public async Task RefreshGamesAsync()
    {
        if (!_isLoaded)
        {
            return;
        }

        State = ContentState.Loading;
        try
        {
            await _launcherService.RefreshGamesAsync(_config);
        }
        finally
        {
            RefreshCollections();
        }
    }

    public void Save()
    {
        // 加载完成前 _config 仍是初始的空配置，此时写盘会清空用户数据
        if (!_isLoaded)
        {
            return;
        }

        NormalizeAllOrders();
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
            ShowNotification($"启动失败：{result.Message}", NotificationKind.Error);
            return;
        }

        var model = FindModel(item);
        if (model is not null)
        {
            model.LastLaunchedAt = DateTime.Now;
        }

        Save();

        // 启动成功后无条件刷新集合，确保"最近启动"分区与最新记录同步
        RefreshCollections();
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
        model.DetailImagePath = draft.DetailImagePath;
        model.Details = draft.Details;
        model.IsManual = draft.IsManual;

        if (model.Group != draft.Group)
        {
            var nextOrder = NextOrder(draft.Group);
            model.Group = draft.Group;
            model.Order = nextOrder;
        }

        Save();

        // 视图模型按 Id 复用，实例在刷新后依然有效，这里刷新其缓存的图标与派生属性，
        // 使改名/换路径后的结果立即反映到界面（详情窗口与主窗口都会走到这里）
        item.Refresh();
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

        // 拖到自己身上时直接忽略。若继续往下走，source 会先从列表移除，
        // 随后找不到 target 而落到末尾，表现为条目莫名坠底。
        if (sourceModel.Id == targetModel.Id)
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

    private void ShowNotification(
        string message,
        NotificationKind kind = NotificationKind.Warning)
    {
        NotificationText = message;
        NotificationLevel = kind;
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
        RecentApps.Clear();
        CurrentApps.Clear();

        PruneViewModelCache();

        if (isSearching)
        {
            var matches = GetVisibleApps(AppGroup.Ide)
                .Concat(GetVisibleApps(AppGroup.AiTool))
                .Concat(GetVisibleApps(AppGroup.Game))
                .Where(app => MatchesSearch(app, searchText))
                // Order 只在各分区内部编号，跨分区比较没有意义，因此先按分区聚合
                .OrderBy(app => app.Group)
                .ThenBy(app => app.Order)
                .ToList();

            foreach (var app in matches)
            {
                CurrentApps.Add(GetOrCreateViewModel(app));
            }

            ContentTitle = "搜索结果";
            ContentSubtitle = $"{matches.Count} 个";
            StatusText = $"搜索结果 {matches.Count} 个";
            State = matches.Count == 0
                ? ContentState.NoSearchResult
                : ContentState.Ready;
            return;
        }

        foreach (var app in GetVisibleApps(AppGroup.Ide))
        {
            IdeApps.Add(GetOrCreateViewModel(app));
        }

        foreach (var app in GetVisibleApps(AppGroup.AiTool))
        {
            AiToolApps.Add(GetOrCreateViewModel(app));
        }

        foreach (var app in GetVisibleApps(AppGroup.Game))
        {
            GameApps.Add(GetOrCreateViewModel(app));
        }

        foreach (var app in _config.Apps
                     .Where(app => !app.IsHidden && app.LastLaunchedAt is not null)
                     .OrderByDescending(app => app.LastLaunchedAt)
                     .Take(10))
        {
            // 最近启动为混合分区，统一使用横版卡片，避免大小不一
            RecentApps.Add(new AppItemViewModel(app, _coverImageService, _gameArtworkService)
            {
                ForceHorizontalCard = true
            });
        }

        StatusText = $"办公 {IdeApps.Count + AiToolApps.Count} 个 · 游戏 {GameApps.Count} 个";

        // ApplyNavSelection 会在填充 CurrentApps 后计算状态
        ApplyNavSelection();
    }

    /// <summary>
    /// 取（或在首次出现时创建）与条目对应的卡片视图模型，使刷新与搜索都能复用已有实例。
    /// </summary>
    private AppItemViewModel GetOrCreateViewModel(AppEntry app)
    {
        if (_viewModels.TryGetValue(app.Id, out var existing))
        {
            return existing;
        }

        var created = new AppItemViewModel(app, _coverImageService, _gameArtworkService);
        _viewModels[app.Id] = created;
        return created;
    }

    /// <summary>
    /// 移除已不在配置中的条目所对应的缓存，避免条目删除后视图模型长期驻留。
    /// </summary>
    private void PruneViewModelCache()
    {
        var alive = _config.Apps
            .Select(app => app.Id)
            .ToHashSet(StringComparer.Ordinal);

        List<string>? stale = null;
        foreach (var id in _viewModels.Keys)
        {
            if (!alive.Contains(id))
            {
                (stale ??= new List<string>()).Add(id);
            }
        }

        if (stale is null)
        {
            return;
        }

        foreach (var id in stale)
        {
            _viewModels.Remove(id);
        }
    }

    private void ApplyNavSelection()
    {
        if (IsSearching)
        {
            return;
        }

        CurrentApps.Clear();

        var (source, title) = SelectedNav switch
        {
            NavSection.AiTool => (AiToolApps, "AI 工具"),
            NavSection.Game => (GameApps, "游戏"),
            NavSection.Recent => (RecentApps, "最近启动"),
            _ => (IdeApps, "IDE")
        };

        foreach (var app in source)
        {
            CurrentApps.Add(app);
        }

        ContentTitle = title;
        ContentSubtitle = SelectedNav == NavSection.Recent
            ? (source.Count == 0 ? "还没有启动记录" : $"{source.Count} 个")
            : $"{source.Count} 个";

        // 状态随 CurrentApps 一起更新。放在这里而不是 RefreshCollections 末尾，
        // 是因为切换导航分区时只有本方法被调用，状态同样需要重算。
        State = CurrentApps.Count == 0
            ? ContentState.Empty
            : ContentState.Ready;
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

    private void NormalizeAllOrders()
    {
        foreach (var group in new[] { AppGroup.Ide, AppGroup.AiTool, AppGroup.Game })
        {
            NormalizeOrders(group);
        }
    }

    private IEnumerable<AppEntry> GetVisibleApps(AppGroup group)
    {
        return _config.Apps
            .Where(app => app.Group == group && !app.IsHidden)
            .OrderBy(app => app.Order);
    }
}
