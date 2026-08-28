using TELLLauncher.Services;

namespace TELLLauncher.Tests;

public class ProcessLauncherTests
{
    [Fact]
    public void Launch_ReturnsFailure_ForMissingPath()
    {
        var result = new ProcessLauncher().Launch(
            @"C:\TELLLauncher\Missing\App.exe");

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }

    [Fact]
    public void Launch_ReturnsSuccess_ForExecutableScript()
    {
        var directory = CreateTempDirectory();

        try
        {
            var scriptPath = Path.Combine(directory, "exit.cmd");
            File.WriteAllText(scriptPath, "@echo off\r\nexit /b 0\r\n");

            var result = new ProcessLauncher().Launch(scriptPath);

            Assert.True(result.Success);
            Assert.Equal(string.Empty, result.Message);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("steam://library")]
    [InlineData("steam://rungameid/12345")]
    public void IsUriTarget_ReturnsTrue_ForUriTargets(string path)
    {
        Assert.True(ProcessLauncher.IsUriTarget(path));
    }

    [Theory]
    [InlineData("com.epicgames.launcher://apps/fortnite")]
    [InlineData("ms-windows-store://pdp")]
    [InlineData("my-app://open")]
    [InlineData("app+special://launch")]
    public void IsUriTarget_ReturnsTrue_ForSchemesWithDotPlusHyphen(string path)
    {
        // RFC 3986 允许 scheme 含 + - . 。此前只判断"全是字母数字"，
        // 会把 com.epicgames.launcher:// 这类协议误判成文件路径而拒绝启动
        Assert.True(ProcessLauncher.IsUriTarget(path));
    }

    [Theory]
    [InlineData("1abc://scheme-must-start-with-letter")]
    [InlineData(@"/abs/prefix://not-a-scheme")]
    public void IsUriTarget_ReturnsFalse_ForInvalidSchemeStart(string path)
    {
        Assert.False(ProcessLauncher.IsUriTarget(path));
    }

    [Theory]
    [InlineData(@"C:\Games\App.exe")]
    [InlineData(@"D:\Steam\steam.exe")]
    [InlineData("relative\\path.exe")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsUriTarget_ReturnsFalse_ForFilePathsAndEmpty(string? path)
    {
        Assert.False(ProcessLauncher.IsUriTarget(path));
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"TELLLauncher.ProcessLauncher.{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
