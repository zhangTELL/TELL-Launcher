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
        }
        catch
        {
            // 封面文件损坏时回退到大图标显示
        }
    }

    public bool HasIcon => Icon is not null;

    public bool HasNoIcon => Icon is null;

    public bool IsMissing => string.IsNullOrWhiteSpace(TargetPath) ||
        (!ProcessLauncher.IsUriTarget(TargetPath) && !File.Exists(TargetPath));

    public bool IsResolved => !IsMissing;

    public bool IsSteamLibrary => Model.IsSteamLibrary;

    public string? DetailImagePath => Model.DetailImagePath;

    public ImageSource? DetailImageSource
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(DetailImagePath) && File.Exists(DetailImagePath))
            {
                try
                {
                    return new BitmapImage(new Uri(DetailImagePath));
                }
                catch
                {
                    // 图片加载失败时回退到大图标
                }
            }

            return LargeIcon;
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

    public bool HasDetailImage =>
        (!string.IsNullOrWhiteSpace(DetailImagePath) && File.Exists(DetailImagePath))
        || LargeIcon is not null;

    public bool HasNoDetailImage => !HasDetailImage;

    public bool HasDetails => !string.IsNullOrWhiteSpace(Details);

    public bool HasNoDetails => !HasDetails;

    public void Refresh()
    {
        Icon = IconService.LoadIcon(Model.IconPath ?? Model.TargetPath);
        LargeIcon = IconService.LoadLargeIcon(Model.IconPath ?? Model.TargetPath);
        _capsuleLoadStarted = false;
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
        OnPropertyChanged(nameof(HasDetailImage));
        OnPropertyChanged(nameof(HasNoDetailImage));
        OnPropertyChanged(nameof(HasDetails));
        OnPropertyChanged(nameof(HasNoDetails));
    }
}
