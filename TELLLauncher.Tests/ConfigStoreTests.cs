using TELLLauncher.Models;
using TELLLauncher.Services;

namespace TELLLauncher.Tests;

public class ConfigStoreTests
{
    [Fact]
    public void Load_CreatesDefaultConfigAndFile_WhenMissing()
    {
        var directory = CreateTempDirectory();

        try
        {
            var store = new ConfigStore(directory);
            var config = store.Load();

            Assert.NotNull(config);
            Assert.Equal(1, config.Version);
            Assert.Empty(config.Apps);
            Assert.Empty(config.HiddenGamePaths);
            Assert.True(File.Exists(store.ConfigPath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void SaveThenLoad_RoundTripsAppEntries()
    {
        var directory = CreateTempDirectory();

        try
        {
            var store = new ConfigStore(directory);
            var config = new LauncherConfig();
            config.Apps.Add(new AppEntry
            {
                Name = "Test App",
                TargetPath = @"C:\Tools\Test.exe",
                DetailImagePath = @"C:\Images\Test.png",
                Details = "自定义详情",
                Group = AppGroup.Ide,
                Order = 2,
                IsManual = true
            });

            store.Save(config);
            var loaded = new ConfigStore(directory).Load();
            var entry = Assert.Single(loaded.Apps);

            Assert.Equal("Test App", entry.Name);
            Assert.Equal(@"C:\Tools\Test.exe", entry.TargetPath);
            Assert.Equal(@"C:\Images\Test.png", entry.DetailImagePath);
            Assert.Equal("自定义详情", entry.Details);
            Assert.Equal(AppGroup.Ide, entry.Group);
            Assert.Equal(2, entry.Order);
            Assert.True(entry.IsManual);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Load_BacksUpCorruptFileAndReturnsDefault()
    {
        var directory = CreateTempDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "config.json"), "{ not valid json");

            var config = new ConfigStore(directory).Load();

            Assert.Empty(config.Apps);
            Assert.Single(Directory.GetFiles(directory, "config.bak-*.json"));
            Assert.True(File.Exists(Path.Combine(directory, "config.json")));
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
            $"TELLLauncher.Tests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
