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
            iconPath = ResolveShortcutIconSource(path);
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
            iconPath = ResolveShortcutIconSource(path);
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
            // 必须用已解析的 iconPath：上面已把 .lnk 换成真实目标，这里若仍用原始 path，
            // 取到的是快捷方式自身的图标，而不是目标程序的大图标
            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(iconPath);
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

    /// <summary>
    /// 决定 .lnk 的图标提取来源。聚合启动器（米哈游 HYP 等）为每个游戏生成指向同一个
    /// launcher.exe 的快捷方式，只有快捷方式自带的 IconLocation 才是游戏本体图标，
    /// 因此存在且有效时优先于解析后的目标 exe。
    /// </summary>
    private static string ResolveShortcutIconSource(string shortcutPath)
    {
        var iconLocation = ShortcutScanner.ResolveShortcutIconLocation(shortcutPath);
        if (!string.IsNullOrWhiteSpace(iconLocation) && File.Exists(iconLocation))
        {
            return iconLocation;
        }

        var target = ShortcutScanner.ResolveShortcutTarget(shortcutPath);
        return !string.IsNullOrWhiteSpace(target) && File.Exists(target)
            ? target
            : shortcutPath;
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
