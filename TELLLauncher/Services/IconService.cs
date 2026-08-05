using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TELLLauncher.Services;

public static class IconService
{
    public static ImageSource? LoadIcon(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        var iconPath = path;
        if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            var target = ShortcutScanner.ResolveShortcutTarget(path);
            if (!string.IsNullOrWhiteSpace(target) && File.Exists(target))
            {
                iconPath = target;
            }
        }

        try
        {
            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(iconPath);
            if (icon is null)
            {
                return null;
            }

            return ConvertToImageSource(icon, 48);
        }
        catch
        {
            return null;
        }
    }

    public static ImageSource? LoadLargeIcon(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        // 解析 .lnk 到目标文件，从目标提取大图标
        var iconPath = path;
        if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            var target = ShortcutScanner.ResolveShortcutTarget(path);
            if (!string.IsNullOrWhiteSpace(target) && File.Exists(target))
            {
                iconPath = target;
            }
        }

        try
        {
            // 尝试提取 256x256 的大图标资源（仅对 exe/dll 有效）
            using var icon = new System.Drawing.Icon(iconPath, 256, 256);
            if (icon is not null)
            {
                return ConvertToImageSource(icon, 256);
            }
        }
        catch
        {
            // 文件没有内嵌大图标资源，回退到 ExtractAssociatedIcon
        }

        try
        {
            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
            if (icon is not null)
            {
                return ConvertToImageSource(icon, 256);
            }
        }
        catch
        {
            // 彻底失败
        }

        return null;
    }

    private static ImageSource? ConvertToImageSource(System.Drawing.Icon icon, int decodeWidth)
    {
        using var bitmap = icon.ToBitmap();
        using var stream = new MemoryStream();
        bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
        stream.Position = 0;

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.DecodePixelWidth = decodeWidth;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
