using TELLLauncher.Services;
using TELLLauncher.ViewModels;
using TELLLauncher.Models;

namespace TELLLauncher.Tests;

public class MainViewModelTests
{
    [Fact]
    public async Task Load_PopulatesOfficeAndGameCollections()
    {
        var directory = CreateTempDirectory();

        try
        {
            var service = new LauncherService(
                new ConfigStore(directory),
                new AppLocator(Array.Empty<string>()),
                new ShortcutScanner(Array.Empty<string>()));
            var viewModel = new MainViewModel(service);

            await viewModel.LoadAsync();

            Assert.Equal(4, viewModel.IdeApps.Count);
            Assert.Equal(6, viewModel.AiToolApps.Count);
            Assert.Single(viewModel.GameApps);
            Assert.True(viewModel.GameApps[0].IsSteamLibrary);
            Assert.Contains("办公 10", viewModel.StatusText);
            Assert.Contains("游戏 1", viewModel.StatusText);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Launch_Success_UpdatesLastLaunchedAt()
    {
        var directory = CreateTempDirectory();

        try
        {
            var store = new ConfigStore(directory);
            var entry = new AppEntry
            {
                Name = "Test",
                TargetPath = Path.Combine(directory, "app.exe"),
                Group = AppGroup.Ide,
                Order = 0
            };
            File.WriteAllText(entry.TargetPath!, string.Empty);
            store.Save(new LauncherConfig
            {
                DefaultsInitialized = true,
                Version = 2,
                Apps = { entry }
            });

            var service = new LauncherService(
                store,
                new AppLocator(Array.Empty<string>()),
                new ShortcutScanner(Array.Empty<string>()));
            var fakeLauncher = new FakeProcessLauncher(
                new LaunchResult(true, string.Empty));
            var viewModel = new MainViewModel(service, fakeLauncher);
            await viewModel.LoadAsync();
            var now = DateTime.Now;

            var loadedItem = viewModel.IdeApps.Single(app => app.Name == "Test");
            viewModel.Launch(loadedItem);

            Assert.False(viewModel.HasNotification);
            Assert.Equal(entry.TargetPath, fakeLauncher.LastPath);
            // 验证 LastLaunchedAt 已更新
            var savedConfig = store.Load();
            var savedEntry = Assert.Single(
                savedConfig.Apps.Where(app => app.Name == "Test"));
            Assert.NotNull(savedEntry.LastLaunchedAt);
            Assert.True(savedEntry.LastLaunchedAt >= now);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task OpenDetailCommand_RaisesOpenDetailRequested()
    {
        var directory = CreateTempDirectory();

        try
        {
            var viewModel = CreateViewModel(directory);
            await viewModel.LoadAsync();
            var item = viewModel.IdeApps[0];
            AppItemViewModel? received = null;
            viewModel.OpenDetailRequested += value => received = value;

            viewModel.OpenDetailCommand.Execute(item);

            Assert.Same(item, received);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Launch_Failure_ShowsNotification()
    {
        var directory = CreateTempDirectory();

        try
        {
            var service = new LauncherService(
                new ConfigStore(directory),
                new AppLocator(Array.Empty<string>()),
                new ShortcutScanner(Array.Empty<string>()));
            var fakeLauncher = new FakeProcessLauncher(
                new LaunchResult(false, "launch blocked"));
            var viewModel = new MainViewModel(service, fakeLauncher);
            var executable = Path.Combine(directory, "app.exe");
            File.WriteAllText(executable, string.Empty);

            viewModel.Launch(new AppItemViewModel(new AppEntry
            {
                Name = "Test",
                TargetPath = executable,
                Group = AppGroup.Ide
            }));

            Assert.True(viewModel.HasNotification);
            Assert.Contains("launch blocked", viewModel.NotificationText);
            // 启动失败是错误级别，通知条应显示红色而非通用的警告色
            Assert.Equal(NotificationKind.Error, viewModel.NotificationLevel);
            Assert.True(viewModel.IsErrorNotification);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void State_BeforeLoad_IsLoading()
    {
        var directory = CreateTempDirectory();

        try
        {
            var viewModel = CreateViewModel(directory);

            // 配置尚未读出时必须显示骨架屏。此前会落到"集合为空"分支，
            // 首屏先闪一句"这里还空空如也"，用户会误以为没扫到东西。
            Assert.Equal(ContentState.Loading, viewModel.State);
            Assert.True(viewModel.IsLoading);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_TransitionsOutOfLoading()
    {
        var directory = CreateTempDirectory();

        try
        {
            var viewModel = CreateViewModel(directory);

            await viewModel.LoadAsync();

            Assert.False(viewModel.IsLoading);
            Assert.Equal(ContentState.Ready, viewModel.State);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task RecentSection_WithNoLaunchHistory_IsEmpty()
    {
        var directory = CreateTempDirectory();

        try
        {
            var viewModel = CreateViewModel(directory);
            await viewModel.LoadAsync();

            // "最近启动"只收集有 LastLaunchedAt 的条目，全新配置下必然为空
            viewModel.SelectedNav = NavSection.Recent;

            Assert.Equal(ContentState.Empty, viewModel.State);
            Assert.True(viewModel.IsEmpty);
            Assert.False(viewModel.IsNoSearchResult);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Search_WithNoMatch_IsNoSearchResultNotEmpty()
    {
        var directory = CreateTempDirectory();

        try
        {
            var viewModel = CreateViewModel(directory);
            await viewModel.LoadAsync();

            viewModel.SearchText = "zzz-不存在的东西";

            // 搜索无果与"分区为空"是两回事：前者要引导清除搜索，后者要引导添加应用
            Assert.Equal(ContentState.NoSearchResult, viewModel.State);
            Assert.True(viewModel.IsNoSearchResult);
            Assert.False(viewModel.IsEmpty);
            Assert.True(viewModel.IsNotSearchEmpty);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ClearSearchCommand_ResetsSearchTextAndState()
    {
        var directory = CreateTempDirectory();

        try
        {
            var viewModel = CreateViewModel(directory);
            await viewModel.LoadAsync();

            viewModel.SearchText = "zzz-不存在的东西";
            Assert.True(viewModel.IsNoSearchResult);

            viewModel.ClearSearchCommand.Execute(null);

            Assert.Equal(string.Empty, viewModel.SearchText);
            Assert.True(viewModel.IsSearchEmpty);
            Assert.False(viewModel.IsNoSearchResult);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Search_FiltersOfficeIntoCombinedResults()
    {
        var directory = CreateTempDirectory();

        try
        {
            var store = new ConfigStore(directory);
            store.Save(new LauncherConfig
            {
                DefaultsInitialized = true,
                Apps =
                {
                    new AppEntry
                    {
                        Name = "VS Code",
                        TargetPath = @"C:\Tools\Code.exe",
                        Group = AppGroup.Ide,
                        Order = 0
                    },
                    new AppEntry
                    {
                        Name = "ChatGPT",
                        Group = AppGroup.AiTool,
                        Order = 1
                    }
                }
            });

            var service = new LauncherService(
                store,
                new AppLocator(Array.Empty<string>()),
                new ShortcutScanner(Array.Empty<string>()));
            var viewModel = new MainViewModel(service);
            await viewModel.LoadAsync();

            viewModel.SearchText = "code";

            Assert.True(viewModel.IsSearching);
            Assert.False(viewModel.IsSearchEmpty);
            Assert.Single(viewModel.CurrentApps);
            Assert.Equal("VS Code", viewModel.CurrentApps[0].Name);
            Assert.Empty(viewModel.IdeApps);
            Assert.Empty(viewModel.AiToolApps);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ClearSearch_RestoresGroupedViews()
    {
        var directory = CreateTempDirectory();

        try
        {
            var store = new ConfigStore(directory);
            store.Save(new LauncherConfig
            {
                DefaultsInitialized = true,
                Apps =
                {
                    new AppEntry
                    {
                        Name = "VS Code",
                        TargetPath = @"C:\Tools\Code.exe",
                        Group = AppGroup.Ide,
                        Order = 0
                    },
                    new AppEntry
                    {
                        Name = "ChatGPT",
                        Group = AppGroup.AiTool,
                        Order = 1
                    }
                }
            });

            var service = new LauncherService(
                store,
                new AppLocator(Array.Empty<string>()),
                new ShortcutScanner(Array.Empty<string>()));
            var viewModel = new MainViewModel(service);
            await viewModel.LoadAsync();
            viewModel.SearchText = "code";

            viewModel.SearchText = string.Empty;

            Assert.False(viewModel.IsSearching);
            Assert.True(viewModel.IsSearchEmpty);
            // 退出搜索后回到当前分区（IDE）的内容
            Assert.Single(viewModel.CurrentApps);
            Assert.Single(viewModel.IdeApps);
            Assert.Single(viewModel.AiToolApps);
            Assert.Equal("VS Code", viewModel.IdeApps[0].Name);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task AddApp_AddsManualEntryToSelectedGroup()
    {
        var directory = CreateTempDirectory();

        try
        {
            var viewModel = CreateViewModel(directory);
            await viewModel.LoadAsync();

            viewModel.AddApp(new AppEntry
            {
                Name = "New Tool",
                TargetPath = @"C:\Tools\New.exe",
                Group = AppGroup.AiTool
            });

            var added = viewModel.AiToolApps.Single(app => app.Name == "New Tool");
            Assert.True(added.Model.IsManual);
            Assert.Equal(@"C:\Tools\New.exe", added.Model.TargetPath);
            Assert.Equal(7, viewModel.AiToolApps.Count);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task UpdateApp_ChangesNamePathAndGroup()
    {
        var directory = CreateTempDirectory();

        try
        {
            var viewModel = CreateViewModel(directory);
            await viewModel.LoadAsync();
            var item = viewModel.IdeApps[0];

            viewModel.UpdateApp(item, new AppEntry
            {
                Name = "Renamed Tool",
                TargetPath = @"C:\Tools\Renamed.exe",
                DetailImagePath = @"C:\Images\Renamed.png",
                Details = "重命名后的详情",
                Group = AppGroup.AiTool
            });

            Assert.DoesNotContain(viewModel.IdeApps, app => app.Model.Id == item.Model.Id);
            var updated = viewModel.AiToolApps.Single(app => app.Name == "Renamed Tool");
            Assert.Equal(@"C:\Tools\Renamed.exe", updated.TargetPath);
            Assert.Equal(@"C:\Images\Renamed.png", updated.Model.DetailImagePath);
            Assert.Equal("重命名后的详情", updated.Model.Details);
            Assert.Equal(3, viewModel.IdeApps.Count);
            Assert.Equal(7, viewModel.AiToolApps.Count);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task RemoveGame_AddsHiddenPathAndRemovesFromList()
    {
        var directory = CreateTempDirectory();

        try
        {
            var store = new ConfigStore(directory);
            store.Save(new LauncherConfig
            {
                DefaultsInitialized = true,
                Apps =
                {
                    new AppEntry
                    {
                        Name = "Test Game",
                        // 用 Steam 库路径，确保条目能通过严格识别存活到被移除；
                        // 换成普通目录路径会在 v3 迁移里被当成误收条目清理掉
                        TargetPath = @"D:\SteamLibrary\steamapps\common\Test\Test.exe",
                        Group = AppGroup.Game,
                        Order = 0
                    }
                }
            });
            var viewModel = new MainViewModel(new LauncherService(
                store,
                new AppLocator(Array.Empty<string>()),
                new ShortcutScanner(Array.Empty<string>())));
            await viewModel.LoadAsync();
            var game = viewModel.GameApps.Single(app => app.Name == "Test Game");

            viewModel.RemoveApp(game);

            Assert.DoesNotContain(viewModel.GameApps, app => app.Name == "Test Game");
            Assert.Contains(viewModel.GameApps, app => app.IsSteamLibrary);
            var saved = store.Load();
            Assert.Contains(
                @"D:\SteamLibrary\steamapps\common\Test\Test.exe",
                saved.HiddenGamePaths);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task MoveUpAndDown_ChangeOrderWithinGroup()
    {
        var directory = CreateTempDirectory();

        try
        {
            var viewModel = CreateViewModel(directory);
            await viewModel.LoadAsync();
            var items = viewModel.IdeApps.ToList();

            viewModel.MoveUp(items[1]);

            Assert.Equal(0, viewModel.IdeApps.First(app => app.Model.Id == items[1].Model.Id).Model.Order);
            Assert.Equal(1, viewModel.IdeApps.First(app => app.Model.Id == items[0].Model.Id).Model.Order);

            viewModel.MoveDown(items[0]);

            Assert.Equal(2, viewModel.IdeApps.First(app => app.Model.Id == items[0].Model.Id).Model.Order);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task MoveBefore_ReordersWithinGroup()
    {
        var directory = CreateTempDirectory();

        try
        {
            var viewModel = CreateViewModel(directory);
            await viewModel.LoadAsync();
            var items = viewModel.IdeApps.ToList();

            viewModel.MoveBefore(items[3], items[0]);

            Assert.Equal(items[3].Model.Id, viewModel.IdeApps[0].Model.Id);
            Assert.Equal(0, viewModel.IdeApps[0].Model.Order);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task MoveToEnd_AppendsWithinGroup()
    {
        var directory = CreateTempDirectory();

        try
        {
            var viewModel = CreateViewModel(directory);
            await viewModel.LoadAsync();
            var first = viewModel.IdeApps[0];

            viewModel.MoveToEnd(first);

            Assert.Equal(3, first.Model.Order);
            Assert.Equal(first.Model.Id, viewModel.IdeApps[3].Model.Id);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task MoveApp_ChangesGroupAndAppendsOrder()
    {
        var directory = CreateTempDirectory();

        try
        {
            var viewModel = CreateViewModel(directory);
            await viewModel.LoadAsync();
            var item = viewModel.IdeApps[0];

            viewModel.MoveApp(item, AppGroup.Game);

            Assert.DoesNotContain(viewModel.IdeApps, app => app.Model.Id == item.Model.Id);
            Assert.Contains(viewModel.GameApps, app => app.Model.Id == item.Model.Id);
            Assert.Equal(1, viewModel.GameApps.First(app => app.Model.Id == item.Model.Id).Model.Order);
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
            $"TELLLauncher.MainViewModel.{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    [Fact]
    public async Task Launch_WithUriTarget_BypassesMissingCheckAndLaunches()
    {
        var directory = CreateTempDirectory();

        try
        {
            var store = new ConfigStore(directory);
            var entry = new AppEntry
            {
                Name = "Url Game",
                TargetPath = "steam://rungameid/12345",
                Group = AppGroup.Game,
                Order = 0
            };
            store.Save(new LauncherConfig
            {
                DefaultsInitialized = true,
                Version = 2,
                Apps = { entry }
            });

            var service = new LauncherService(
                store,
                new AppLocator(Array.Empty<string>()),
                new ShortcutScanner(Array.Empty<string>()));
            var fakeLauncher = new FakeProcessLauncher(
                new LaunchResult(true, string.Empty));
            var viewModel = new MainViewModel(service, fakeLauncher);
            await viewModel.LoadAsync();
            AppItemViewModel? locateRequested = null;
            viewModel.LocateRequested += item => locateRequested = item;

            var loadedItem = viewModel.GameApps.Single(app => app.Name == "Url Game");
            Assert.False(loadedItem.IsMissing);
            viewModel.Launch(loadedItem);

            Assert.Null(locateRequested);
            Assert.False(viewModel.HasNotification);
            Assert.Equal(entry.TargetPath, fakeLauncher.LastPath);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task SelectedNav_SwitchesCurrentAppsAndTitle()
    {
        var directory = CreateTempDirectory();

        try
        {
            var viewModel = CreateViewModel(directory);
            await viewModel.LoadAsync();

            Assert.Equal(NavSection.Ide, viewModel.SelectedNav);
            Assert.Equal("IDE", viewModel.ContentTitle);
            Assert.Equal(viewModel.IdeApps.Count, viewModel.CurrentApps.Count);

            viewModel.SelectNavCommand.Execute(NavSection.Game);

            Assert.Equal("游戏", viewModel.ContentTitle);
            Assert.Equal(viewModel.GameApps.Count, viewModel.CurrentApps.Count);
            Assert.Contains("个", viewModel.ContentSubtitle);

            viewModel.SelectNavCommand.Execute(NavSection.AiTool);

            Assert.Equal("AI 工具", viewModel.ContentTitle);
            Assert.Equal(viewModel.AiToolApps.Count, viewModel.CurrentApps.Count);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task RecentApps_ListsLaunchedApps_AfterLaunch()
    {
        var directory = CreateTempDirectory();

        try
        {
            var store = new ConfigStore(directory);
            var entry = new AppEntry
            {
                Name = "Recent Test",
                TargetPath = Path.Combine(directory, "app.exe"),
                Group = AppGroup.Ide,
                Order = 0
            };
            File.WriteAllText(entry.TargetPath!, string.Empty);
            store.Save(new LauncherConfig
            {
                DefaultsInitialized = true,
                Version = 2,
                Apps = { entry }
            });

            var service = new LauncherService(
                store,
                new AppLocator(Array.Empty<string>()),
                new ShortcutScanner(Array.Empty<string>()));
            var fakeLauncher = new FakeProcessLauncher(
                new LaunchResult(true, string.Empty));
            var viewModel = new MainViewModel(service, fakeLauncher);
            await viewModel.LoadAsync();
            viewModel.SelectNavCommand.Execute(NavSection.Recent);

            Assert.Empty(viewModel.CurrentApps);
            Assert.Equal("还没有启动记录", viewModel.ContentSubtitle);

            var item = viewModel.IdeApps.Single(app => app.Name == "Recent Test");
            viewModel.Launch(item);

            Assert.Single(viewModel.CurrentApps);
            Assert.Equal("Recent Test", viewModel.CurrentApps[0].Name);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Launch_FromOtherSection_PopulatesRecentApps()
    {
        var directory = CreateTempDirectory();

        try
        {
            var store = new ConfigStore(directory);
            var entry = new AppEntry
            {
                Name = "Recent Test",
                TargetPath = Path.Combine(directory, "app.exe"),
                Group = AppGroup.Ide,
                Order = 0
            };
            File.WriteAllText(entry.TargetPath!, string.Empty);
            store.Save(new LauncherConfig
            {
                DefaultsInitialized = true,
                Version = 2,
                Apps = { entry }
            });

            var service = new LauncherService(
                store,
                new AppLocator(Array.Empty<string>()),
                new ShortcutScanner(Array.Empty<string>()));
            var fakeLauncher = new FakeProcessLauncher(
                new LaunchResult(true, string.Empty));
            var viewModel = new MainViewModel(service, fakeLauncher);
            await viewModel.LoadAsync();

            // 在 IDE 页启动（不是"最近启动"页）
            var item = viewModel.IdeApps.Single(app => app.Name == "Recent Test");
            viewModel.Launch(item);

            // 切到"最近启动"页应能看到记录
            viewModel.SelectNavCommand.Execute(NavSection.Recent);
            Assert.Single(viewModel.CurrentApps);
            Assert.Equal("Recent Test", viewModel.CurrentApps[0].Name);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Save_BeforeLoad_DoesNotOverwriteConfigOnDisk()
    {
        var directory = CreateTempDirectory();

        try
        {
            var store = new ConfigStore(directory);
            store.Save(CreatePersistedConfig());

            var viewModel = CreateViewModel(directory);

            // 模拟窗口在 LoadAsync 完成前关闭：此时 _config 仍是初始的空配置
            viewModel.Save();

            var reloaded = new ConfigStore(directory).Load();
            Assert.Contains(reloaded.Apps, app => app.Id == "persisted");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task RefreshGames_BeforeLoad_DoesNotOverwriteConfigOnDisk()
    {
        var directory = CreateTempDirectory();

        try
        {
            var store = new ConfigStore(directory);
            store.Save(CreatePersistedConfig());

            var viewModel = CreateViewModel(directory);

            // 加载完成前点击"刷新"同样不得写盘
            await viewModel.RefreshGamesAsync();

            var reloaded = new ConfigStore(directory).Load();
            Assert.Contains(reloaded.Apps, app => app.Id == "persisted");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Save_AfterLoad_PersistsChanges()
    {
        var directory = CreateTempDirectory();

        try
        {
            var store = new ConfigStore(directory);
            store.Save(CreatePersistedConfig());

            var viewModel = CreateViewModel(directory);
            await viewModel.LoadAsync();

            viewModel.AddApp(new AppEntry { Name = "Added", Group = AppGroup.Ide });

            var reloaded = new ConfigStore(directory).Load();
            Assert.Contains(reloaded.Apps, app => app.Id == "persisted");
            Assert.Contains(reloaded.Apps, app => app.Name == "Added");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task MoveBefore_SameItem_KeepsOriginalOrder()
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
                    new AppEntry { Id = "a", Name = "A", Group = AppGroup.Ide, Order = 0 },
                    new AppEntry { Id = "b", Name = "B", Group = AppGroup.Ide, Order = 1 },
                    new AppEntry { Id = "c", Name = "C", Group = AppGroup.Ide, Order = 2 }
                }
            });

            var viewModel = CreateViewModel(directory);
            await viewModel.LoadAsync();

            var itemB = viewModel.IdeApps.Single(app => app.Name == "B");
            viewModel.MoveBefore(itemB, itemB);

            // 拖到自己身上应无操作。此前 source 会先被移除，导致找不到 target 而坠到末尾
            Assert.Equal(
                new[] { "A", "B", "C" },
                viewModel.IdeApps.Select(app => app.Name).ToArray());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Search_ReusesViewModels_AcrossRefreshes()
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
                    new AppEntry { Id = "a", Name = "VS Code", Group = AppGroup.Ide, Order = 0 },
                    new AppEntry { Id = "b", Name = "Cursor", Group = AppGroup.Ide, Order = 1 }
                }
            });

            var viewModel = CreateViewModel(directory);
            await viewModel.LoadAsync();
            var before = viewModel.IdeApps.ToList();

            // 搜索会走与刷新相同的重建路径
            viewModel.SearchText = "code";
            viewModel.SearchText = string.Empty;

            var after = viewModel.IdeApps.ToList();
            Assert.Equal(
                new[] { "VS Code", "Cursor" },
                after.Select(app => app.Name).ToArray());

            // 视图模型按 Id 复用；若每次重建，构造函数里的图标提取会被重复执行
            Assert.Same(before[0], after[0]);
            Assert.Same(before[1], after[1]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static LauncherConfig CreatePersistedConfig()
    {
        return new LauncherConfig
        {
            DefaultsInitialized = true,
            Version = 2,
            Apps =
            {
                new AppEntry
                {
                    Id = "persisted",
                    Name = "Persisted App",
                    Group = AppGroup.Ide,
                    Order = 0
                }
            }
        };
    }

    private static MainViewModel CreateViewModel(string directory)
    {
        var service = new LauncherService(
            new ConfigStore(directory),
            new AppLocator(Array.Empty<string>()),
            new ShortcutScanner(Array.Empty<string>()));
        return new MainViewModel(service);
    }

    private sealed class FakeProcessLauncher : IProcessLauncher
    {
        private readonly LaunchResult _result;

        public FakeProcessLauncher(LaunchResult result)
        {
            _result = result;
        }

        public string? LastPath { get; private set; }

        public LaunchResult Launch(string path)
        {
            LastPath = path;
            return _result;
        }
    }
}
