using TELLLauncher.Services;

namespace TELLLauncher.Tests;

public class IconServiceTests
{
    [Fact]
    public void LoadIcon_ReturnsNull_ForMissingPath()
    {
        Assert.Null(IconService.LoadIcon(@"C:\TELLLauncher\Missing\App.exe"));
    }

    [Fact]
    public void LoadIcon_ReturnsImage_ForSystemExecutable()
    {
        var executable = Path.Combine(Environment.SystemDirectory, "cmd.exe");

        var icon = IconService.LoadIcon(executable);

        Assert.NotNull(icon);
        Assert.True(icon.IsFrozen);
    }
}
