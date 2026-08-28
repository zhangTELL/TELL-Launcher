using TELLLauncher.Services;

namespace TELLLauncher.Tests;

public class GameShortcutRulesTests
{
    [Theory]
    // Steam 运行协议
    [InlineData("steam://rungameid/730")]
    [InlineData("steam://rungameid/1172470/")]
    // Steam 库目录（分别用反斜杠与正斜杠）
    [InlineData(@"D:\SteamLibrary\steamapps\common\Hades\Hades.exe")]
    [InlineData(@"D:/SteamLibrary/steamapps/common/Hades/Hades.exe")]
    // 已知厂商目录
    [InlineData(@"D:\Games\Genshin Impact\GenshinImpact.exe")]
    [InlineData(@"D:\Games\原神\YuanShen.exe")]
    [InlineData(@"C:\Program Files\Epic Games\Fortnite\Fortnite.exe")]
    public void IsGame_ReturnsTrue_ForRecognizableGameTargets(string targetPath)
    {
        Assert.True(GameShortcutRules.IsGame("Some Game", targetPath));
    }

    [Theory]
    [InlineData(@"C:\Program Files\Google\Chrome\chrome.exe")]
    [InlineData(@"C:\Windows\System32\notepad.exe")]
    [InlineData(@"C:\Program Files\SomeApp\unins000.exe")]
    [InlineData(@"C:\Users\TELL\Desktop\report.lnk")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsGame_ReturnsFalse_ForNonGameTargets(string? targetPath)
    {
        Assert.False(GameShortcutRules.IsGame("Some App", targetPath));
    }
}
