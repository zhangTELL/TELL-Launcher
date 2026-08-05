using TELLLauncher.Models;
using TELLLauncher.Services;

namespace TELLLauncher.Tests;

public class LauncherServiceTests
{
    [Fact]
    public void LoadOrCreate_FillsUnresolvedDefaultOfficePath()
    {
        var directory = CreateTempDirectory();
        var searchRoot = CreateTempDirectory();

        try
        {
            var executable = Path.Combine(searchRoot, "Microsoft VS Code", "Code.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(executable)!);
            File.WriteAllText(executable, string.Empty);

            var store = new ConfigStore(directory);
            store.Save(new LauncherConfig
            {
                DefaultsInitialized = true,
                Apps =
                {
                    new AppEntry
                    {
                        Name = "VS Code",
                        Group = AppGroup.Ide,
                        Order = 0
                    }
                }
            });

            var service = new LauncherService(
                store,
                new AppLocator(new[] { searchRoot }),
                new ShortcutScanner(Array.Empty<string>()));

            service.LoadOrCreate();

            var app = Assert.Single(store.Load().Apps);
            Assert.Equal(executable, app.TargetPath);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
            Directory.Delete(searchRoot, recursive: true);
        }
    }

    [Fact]
    public void LoadOrCreate_DoesNotOverrideResolvedOfficePath()
    {
        var directory = CreateTempDirectory();
        var searchRoot = CreateTempDirectory();

        try
        {
            var executable = Path.Combine(searchRoot, "Microsoft VS Code", "Code.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(executable)!);
            File.WriteAllText(executable, string.Empty);

            var store = new ConfigStore(directory);
            store.Save(new LauncherConfig
            {
                DefaultsInitialized = true,
                Apps =
                {
                    new AppEntry
                    {
                        Name = "VS Code",
                        TargetPath = @"C:\Custom\Code.exe",
                        IconPath = @"C:\Custom\Code.exe",
                        Group = AppGroup.Ide,
                        Order = 0
                    }
                }
            });

            var service = new LauncherService(
                store,
                new AppLocator(new[] { searchRoot }),
                new ShortcutScanner(Array.Empty<string>()));

            service.LoadOrCreate();

            var app = Assert.Single(store.Load().Apps);
            Assert.Equal(@"C:\Custom\Code.exe", app.TargetPath);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
            Directory.Delete(searchRoot, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"TELLLauncher.LauncherService.{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
