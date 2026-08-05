using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TELLLauncher.Models;
using TELLLauncher.Services;

namespace TELLLauncher.ViewModels;

public sealed class AppItemViewModel : ObservableObject
{
    public AppItemViewModel(AppEntry model)
    {
        Model = model;
        Icon = IconService.LoadIcon(model.IconPath ?? model.TargetPath);
        LargeIcon = IconService.LoadLargeIcon(model.IconPath ?? model.TargetPath);
    }

    public AppEntry Model { get; }

    public string Name => Model.Name;

    public string? TargetPath => Model.TargetPath;

    public string Initial => string.IsNullOrWhiteSpace(Name)
        ? "?"
        : Name[..1].ToUpperInvariant();

    public ImageSource? Icon { get; private set; }

    public ImageSource? LargeIcon { get; private set; }

    public bool HasIcon => Icon is not null;

    public bool HasNoIcon => Icon is null;

    public bool IsMissing => string.IsNullOrWhiteSpace(TargetPath) || !File.Exists(TargetPath);

    public bool IsResolved => !IsMissing;

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
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(TargetPath));
        OnPropertyChanged(nameof(Initial));
        OnPropertyChanged(nameof(Icon));
        OnPropertyChanged(nameof(LargeIcon));
        OnPropertyChanged(nameof(HasIcon));
        OnPropertyChanged(nameof(HasNoIcon));
        OnPropertyChanged(nameof(IsMissing));
        OnPropertyChanged(nameof(IsResolved));
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
