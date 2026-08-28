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

            var savedApps = store.Load().Apps;
            var app = Assert.Single(savedApps.Where(item => item.Name == "VS Code"));
            Assert.Contains(savedApps, item => item.IsSteamLibrary);
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

            var savedApps = store.Load().Apps;
            var app = Assert.Single(savedApps.Where(item => item.Name == "VS Code"));
            Assert.Contains(savedApps, item => item.IsSteamLibrary);
            Assert.Equal(@"C:\Custom\Code.exe", app.TargetPath);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
            Directory.Delete(searchRoot, recursive: true);
        }
    }

    [Fact]
    public void LoadOrCreate_BackfillsDetailsForAlreadyLocatedDefaultApps()
    {
        var directory = CreateTempDirectory();

        try
        {
            // 模拟 Details 功能上线前保存的配置：路径已定位成功，但 Details 为空。
            // RefreshUnresolvedOfficePaths 要求 TargetPath 为空才匹配，
            // 所以这些条目永远不会走到写 Details 的分支。
            var store = new ConfigStore(directory);
            store.Save(new LauncherConfig
            {
                DefaultsInitialized = true,
                Version = 3,
                Apps =
                {
                    new AppEntry
                    {
                        Name = "VS Code",
                        TargetPath = @"C:\Users\me\AppData\Local\Programs\Microsoft VS Code\Code.exe",
                        Group = AppGroup.Ide,
                        Order = 0
                    },
                    new AppEntry
                    {
                        Name = "Claude",
                        TargetPath = @"C:\Users\me\Desktop\claude.lnk",
                        Group = AppGroup.AiTool,
                        Order = 1
                    }
                }
            });

            var service = new LauncherService(
                store,
                new AppLocator(Array.Empty<string>()),
                new ShortcutScanner(Array.Empty<string>()));

            var config = service.LoadOrCreate();

            var vsCode = Assert.Single(config.Apps, app => app.Name == "VS Code");
            var claude = Assert.Single(config.Apps, app => app.Name == "Claude");

            Assert.False(string.IsNullOrWhiteSpace(vsCode.Details));
            Assert.False(string.IsNullOrWhiteSpace(claude.Details));
            Assert.Equal(4, config.Version);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void LoadOrCreate_DoesNotOverwriteUserSuppliedDetails()
    {
        var directory = CreateTempDirectory();

        try
        {
            var store = new ConfigStore(directory);
            store.Save(new LauncherConfig
            {
                DefaultsInitialized = true,
                Version = 3,
                Apps =
                {
                    // 用户自己填过详情，迁移必须保留
                    new AppEntry
                    {
                        Name = "VS Code",
                        TargetPath = @"C:\Code.exe",
                        Details = "我自己的备注",
                        Group = AppGroup.Ide,
                        Order = 0
                    },
                    // 手动添加的条目即使名字与默认应用相同，也不应被回填
                    new AppEntry
                    {
                        Name = "PyCharm",
                        TargetPath = @"D:\JetBrains\pycharm64.exe",
                        Group = AppGroup.Ide,
                        Order = 1,
                        IsManual = true
                    }
                }
            });

            var service = new LauncherService(
                store,
                new AppLocator(Array.Empty<string>()),
                new ShortcutScanner(Array.Empty<string>()));

            var config = service.LoadOrCreate();

            var vsCode = Assert.Single(config.Apps, app => app.Name == "VS Code");
            var pycharm = Assert.Single(config.Apps, app => app.Name == "PyCharm");

            Assert.Equal("我自己的备注", vsCode.Details);
            Assert.True(string.IsNullOrWhiteSpace(pycharm.Details));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void LoadOrCreate_PrunesNonGameEntriesAndHidesTheirPaths()
    {
        var directory = CreateTempDirectory();

        try
        {
            var store = new ConfigStore(directory);
            store.Save(new LauncherConfig
            {
                DefaultsInitialized = true,
                Version = 2,
                Apps =
                {
                    // 早期"扫到什么算什么"阶段误收进来的普通软件
                    new AppEntry
                    {
                        Name = "Browser",
                        TargetPath = @"C:\Program Files\Browser\browser.exe",
                        Group = AppGroup.Game,
                        Order = 0
                    },
                    // 真正的游戏，应当保留
                    new AppEntry
                    {
                        Name = "CS2",
                        TargetPath = "steam://rungameid/730",
                        Group = AppGroup.Game,
                        Order = 1
                    },
                    // 用户手动添加的，即便识别不出也不应被清理
                    new AppEntry
                    {
                        Name = "My Tool",
                        TargetPath = @"D:\Tools\tool.exe",
                        Group = AppGroup.Game,
                        Order = 2,
                        IsManual = true
                    }
                }
            });

            var service = new LauncherService(
                store,
                new AppLocator(Array.Empty<string>()),
                new ShortcutScanner(Array.Empty<string>()));

            var config = service.LoadOrCreate();

            var names = config.Apps
                .Where(app => app.Group == AppGroup.Game && !app.IsSteamLibrary)
                .Select(app => app.Name)
                .ToArray();
            Assert.DoesNotContain("Browser", names);
            Assert.Contains("CS2", names);
            Assert.Contains("My Tool", names);

            // 被清理的路径必须写入隐藏列表，否则桌面上的同一个快捷方式
            // 会在下次扫描时被重新加回来
            Assert.Contains(
                @"C:\Program Files\Browser\browser.exe",
                config.HiddenGamePaths);
            Assert.Equal(4, config.Version);
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
            $"TELLLauncher.LauncherService.{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
