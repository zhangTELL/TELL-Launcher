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
            Assert.Single(viewModel.SearchResults);
            Assert.Equal("VS Code", viewModel.SearchResults[0].Name);
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
            Assert.Empty(viewModel.SearchResults);
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
                        TargetPath = @"C:\Games\Test.exe",
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
            Assert.Contains(@"C:\Games\Test.exe", saved.HiddenGamePaths);
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
