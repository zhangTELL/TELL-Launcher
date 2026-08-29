using TELLLauncher.Models;
using TELLLauncher.ViewModels;

namespace TELLLauncher.Tests;

public class AppItemViewModelTests
{
    // 1x1 透明 PNG，用于构造真实可解码的图片文件
    private const string TinyPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    [Fact]
    public void TargetPathDisplay_TranslatesSteamUriToActionDescription()
    {
        var item = CreateItem(new AppEntry { TargetPath = "steam://rungameid/2458530" });

        Assert.Equal("通过 Steam 启动", item.TargetPathDisplay);
        Assert.True(item.HasTargetPath);
    }

    [Fact]
    public void TargetPathDisplay_TranslatesOtherUriSchemes()
    {
        var item = CreateItem(new AppEntry { TargetPath = "https://example.com" });

        Assert.Equal("通过系统默认程序启动", item.TargetPathDisplay);
    }

    [Fact]
    public void TargetPathDisplay_KeepsFilePathsVerbatim()
    {
        var path = @"C:\Program Files\app.exe";

        var item = CreateItem(new AppEntry { TargetPath = path });

        Assert.Equal(path, item.TargetPathDisplay);
    }

    [Fact]
    public void TargetPathDisplay_ShowsMissingForEmptyPath()
    {
        var item = CreateItem(new AppEntry { TargetPath = null });

        Assert.Equal("未定位", item.TargetPathDisplay);
        Assert.False(item.HasTargetPath);
    }

    [Fact]
    public void DetailImageSource_IsEmptyWhenNoCustomImageNoCapsuleNoIcon()
    {
        var item = CreateItem(new AppEntry { TargetPath = @"C:\not-exist\app.exe" });

        Assert.False(item.HasDetailImage);
        Assert.True(item.HasNoDetailImage);
        Assert.Null(item.DetailImageSource);
    }

    [Fact]
    public void DetailImageSource_LoadsCustomImageFile()
    {
        var directory = CreateTempDirectory();
        var imagePath = Path.Combine(directory, "cover.png");
        File.WriteAllBytes(imagePath, Convert.FromBase64String(TinyPngBase64));

        try
        {
            var item = CreateItem(new AppEntry
            {
                TargetPath = @"C:\not-exist\app.exe",
                DetailImagePath = imagePath
            });

            Assert.True(item.HasDetailImage);
            Assert.NotNull(item.DetailImageSource);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static AppItemViewModel CreateItem(AppEntry entry)
    {
        // 不注入封面服务：聚焦路径与大图回退的纯逻辑分支
        return new AppItemViewModel(entry);
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "TELLLauncherTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
