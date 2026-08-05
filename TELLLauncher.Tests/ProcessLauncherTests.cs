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

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"TELLLauncher.ProcessLauncher.{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
