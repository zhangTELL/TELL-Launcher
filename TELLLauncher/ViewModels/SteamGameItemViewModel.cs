using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TELLLauncher.Services;

namespace TELLLauncher.ViewModels;

public sealed class SteamGameItemViewModel : ObservableObject
{
    private ImageSource? _capsuleImage;

    public SteamGameItemViewModel(
        SteamGameInfo model,
        CoverImageService? coverImageService = null)
    {
        Model = model;

        if (coverImageService is not null)
        {
            _ = LoadCapsuleAsync(coverImageService);
        }
    }

    public SteamGameInfo Model { get; }

    public string Name => Model.Name;

    public string AppId => Model.AppId;

    public string Initial => string.IsNullOrWhiteSpace(Name)
        ? "?"
        : Name[..1].ToUpperInvariant();

    public ImageSource? CapsuleImage
    {
        get => _capsuleImage;
        private set
        {
            if (SetProperty(ref _capsuleImage, value))
            {
                OnPropertyChanged(nameof(HasCapsuleImage));
                OnPropertyChanged(nameof(HasNoCapsuleImage));
            }
        }
    }

    public bool HasCapsuleImage => CapsuleImage is not null;

    public bool HasNoCapsuleImage => CapsuleImage is null;

    private async Task LoadCapsuleAsync(CoverImageService coverImageService)
    {
        var path = await coverImageService.GetCapsulePathAsync(AppId);
        if (path is null || !File.Exists(path))
        {
            return;
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path);
            image.EndInit();
            image.Freeze();
            CapsuleImage = image;
        }
        catch
        {
            // 封面文件损坏时回退到首字母显示
        }
    }
}
