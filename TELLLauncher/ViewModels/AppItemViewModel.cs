using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TELLLauncher.Models;
using TELLLauncher.Services;

namespace TELLLauncher.ViewModels;

public sealed class AppItemViewModel : ObservableObject
{
    private readonly CoverImageService? _coverImageService;
    private readonly GameArtworkService? _gameArtworkService;
    private ImageSource? _capsuleImage;
    private bool _capsuleLoadStarted;
    private ImageSource? _detailImageSource;
    private bool? _isMissing;
    private bool? _hasDetailImage;

    public AppItemViewModel(
        AppEntry model,
        CoverImageService? coverImageService = null,
        GameArtworkService? gameArtworkService = null)
    {
        Model = model;
        _coverImageService = coverImageService;
        _gameArtworkService = gameArtworkService;
        Icon = IconService.LoadIcon(model.IconPath ?? model.TargetPath);
        LargeIcon = IconService.LoadLargeIcon(model.IconPath ?? model.TargetPath);
        _ = LoadCapsuleAsync();
    }

    public AppEntry Model { get; }

    public string Name => Model.Name;

    public string? TargetPath => Model.TargetPath;

    public string Initial => string.IsNullOrWhiteSpace(Name)
        ? "?"
        : Name[..1].ToUpperInvariant();

    public ImageSource? Icon { get; private set; }

    public ImageSource? LargeIcon { get; private set; }

    public bool IsGame => Model.Group == AppGroup.Game;

    public bool IsOffice => !IsGame;

    /// <summary>
    /// 强制使用横版卡片（用于"最近启动"等混合分区，避免游戏竖版胶囊与办公横版卡大小不一）。
    /// </summary>
    public bool ForceHorizontalCard { get; set; }

    public bool ShowAsGameCard => IsGame && !ForceHorizontalCard;

    public bool ShowAsOfficeCard => !ShowAsGameCard;

    public string? SteamAppId => CoverImageService.ExtractSteamAppId(Model.TargetPath);

    public ImageSource? CapsuleImage
    {
        get => _capsuleImage;
        private set => SetProperty(ref _capsuleImage, value);
    }

    public bool HasCapsuleImage => CapsuleImage is not null;

    public bool HasNoCapsuleImage => CapsuleImage is null;

    public async Task LoadCapsuleAsync()
    {
        if (_capsuleLoadStarted)
        {
            return;
        }

        _capsuleLoadStarted = true;

        string? path = null;
        try
        {
            if (_gameArtworkService is not null)
            {
                path = await _gameArtworkService.GetCapsulePathAsync(Model);
            }
            else if (_coverImageService is not null)
            {
                var appId = SteamAppId;
                if (appId is not null)
                {
                    path = await _coverImageService.GetCapsulePathAsync(appId);
                }
            }
        }
        catch
        {
            // 封面解析失败（目录遍历异常、网络异常等）不应影响卡片显示，
            // 也不应成为逃逸到 fire-and-forget 调用处的未观察异常
            return;
        }

        if (path is null || !File.Exists(path))
        {
            return;
        }

        try
        {
            ImageSource? image;
            if (path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
            {
                image = IconService.LoadLargeIcon(path);
            }
            else
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(path);
                bitmap.EndInit();
                bitmap.Freeze();
                image = bitmap;
            }

            if (image is null)
            {
                return;
            }

            CapsuleImage = image;
            OnPropertyChanged(nameof(HasCapsuleImage));
            OnPropertyChanged(nameof(HasNoCapsuleImage));

            // 封面是异步加载的，详情页可能在封面到位前就已打开；
            // 大图回退链（自定义图 → 封面 → 图标）需要重新求值
            _hasDetailImage = null;
            OnPropertyChanged(nameof(DetailImageSource));
            OnPropertyChanged(nameof(HasDetailImage));
            OnPropertyChanged(nameof(HasNoDetailImage));
        }
        catch
        {
            // 封面文件损坏时回退到大图标显示
        }
    }

    public bool HasIcon => Icon is not null;

    public bool HasNoIcon => Icon is null;

    /// <summary>
    /// 目标是否已失效。界面在同一张卡片上多处绑定该属性，每次求值都会访问磁盘，
    /// 因此缓存结果，由 <see cref="Refresh"/> 清空后重新计算。
    /// </summary>
    public bool IsMissing => _isMissing ??= ComputeIsMissing();

    public bool IsResolved => !IsMissing;

    private bool ComputeIsMissing()
    {
        return string.IsNullOrWhiteSpace(TargetPath) ||
               (!ProcessLauncher.IsUriTarget(TargetPath) && !File.Exists(TargetPath));
    }

    public bool IsSteamLibrary => Model.IsSteamLibrary;

    public string? DetailImagePath => Model.DetailImagePath;

    public ImageSource? DetailImageSource
    {
        get
        {
            if (_detailImageSource is not null)
            {
                return _detailImageSource;
            }

            if (!string.IsNullOrWhiteSpace(DetailImagePath) && File.Exists(DetailImagePath))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    // OnLoad 在读取完成后立即关闭文件句柄。默认的 OnDemand 会一直占用该文件，
                    // 使用户无法删除或替换这张图片
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(DetailImagePath);
                    bitmap.EndInit();
                    bitmap.Freeze();
                    _detailImageSource = bitmap;
                    return bitmap;
                }
                catch
                {
                    // 图片加载失败时继续走回退链
                }
            }

            // 封面优先于图标：卡片上用户看到哪张图，详情页就展示哪张图
            return CapsuleImage ?? LargeIcon;
        }
    }

    public string? Details => Model.Details;

    public string GroupDisplay => Model.Group switch
    {
        AppGroup.Ide => "IDE",
        AppGroup.AiTool => "AI 工具",
        AppGroup.Game => "游戏",
        _ => string.Empty
    };

    /// <summary>详情页横幅里的启动历史文案，精确到分钟。</summary>
    public string LastLaunchedDisplay => Model.LastLaunchedAt is null
        ? "尚未启动过"
        : $"上次启动 {Model.LastLaunchedAt:yyyy-MM-dd HH:mm}";

    public bool HasDetailImage => _hasDetailImage ??=
        (!string.IsNullOrWhiteSpace(DetailImagePath) && File.Exists(DetailImagePath))
        || CapsuleImage is not null
        || LargeIcon is not null;

    public bool HasNoDetailImage => !HasDetailImage;

    public bool HasDetails => !string.IsNullOrWhiteSpace(Details);

    public bool HasNoDetails => !HasDetails;

    public bool HasTargetPath => !string.IsNullOrWhiteSpace(TargetPath);

    /// <summary>
    /// 详情页展示用的路径描述。steam:// 等协议地址对用户是噪音，翻译成动作说明；
    /// 原始值仍通过路径行的工具提示与"复制路径"按钮提供。
    /// </summary>
    public string TargetPathDisplay
    {
        get
        {
            if (string.IsNullOrWhiteSpace(TargetPath))
            {
                return "未定位";
            }

            if (!ProcessLauncher.IsUriTarget(TargetPath))
            {
                return TargetPath;
            }

            return TargetPath.StartsWith("steam://", StringComparison.OrdinalIgnoreCase)
                ? "通过 Steam 启动"
                : "通过系统默认程序启动";
        }
    }

    public void Refresh()
    {
        Icon = IconService.LoadIcon(Model.IconPath ?? Model.TargetPath);
        LargeIcon = IconService.LoadLargeIcon(Model.IconPath ?? Model.TargetPath);
        _capsuleLoadStarted = false;

        // 清空依赖磁盘状态的缓存，让随后发出的通知携带重新计算的结果
        _detailImageSource = null;
        _isMissing = null;
        _hasDetailImage = null;

        _ = LoadCapsuleAsync();
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(TargetPath));
        OnPropertyChanged(nameof(Initial));
        OnPropertyChanged(nameof(Icon));
        OnPropertyChanged(nameof(LargeIcon));
        OnPropertyChanged(nameof(HasIcon));
        OnPropertyChanged(nameof(HasNoIcon));
        OnPropertyChanged(nameof(IsMissing));
        OnPropertyChanged(nameof(IsResolved));
        OnPropertyChanged(nameof(IsSteamLibrary));
        OnPropertyChanged(nameof(IsGame));
        OnPropertyChanged(nameof(IsOffice));
        OnPropertyChanged(nameof(ShowAsGameCard));
        OnPropertyChanged(nameof(ShowAsOfficeCard));
        OnPropertyChanged(nameof(SteamAppId));
        OnPropertyChanged(nameof(DetailImagePath));
        OnPropertyChanged(nameof(DetailImageSource));
        OnPropertyChanged(nameof(Details));
        OnPropertyChanged(nameof(GroupDisplay));
        OnPropertyChanged(nameof(LastLaunchedDisplay));
        OnPropertyChanged(nameof(TargetPathDisplay));
        OnPropertyChanged(nameof(HasTargetPath));
        OnPropertyChanged(nameof(HasDetailImage));
        OnPropertyChanged(nameof(HasNoDetailImage));
        OnPropertyChanged(nameof(HasDetails));
        OnPropertyChanged(nameof(HasNoDetails));
    }
}
