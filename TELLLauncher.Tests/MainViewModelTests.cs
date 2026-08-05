using TELLLauncher.Services;
using TELLLauncher.ViewModels;
using TELLLauncher.Models;

namespace TELLLauncher.Tests;

public class MainViewModelTests
{
    [Fact]
    public void Load_PopulatesOfficeAndGameCollections()
    {
        var directory = CreateTempDirectory();

        try
        {
            var service = new LauncherService(
                new ConfigStore(directory),
                new AppLocator(Array.Empty<string>()),
                new ShortcutScanner(Array.Empty<string>()));
            var viewModel = new MainViewModel(service);

            viewModel.Load();

            Assert.Equal(4, viewModel.IdeApps.Count);
            Assert.Equal(6, viewModel.AiToolApps.Count);
            Assert.Empty(viewModel.GameApps);
            Assert.Contains("办公 10", viewModel.StatusText);
            Assert.Contains("游戏 0", viewModel.StatusText);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Launch_Success_DoesNotShowNotification()
    {
        var directory = CreateTempDirectory();

        try
        {
            var service = new LauncherService(
                new ConfigStore(directory),
                new AppLocator(Array.Empty<string>()),
                new ShortcutScanner(Array.Empty<string>()));
            var fakeLauncher = new FakeProcessLauncher(
                new LaunchResult(true, string.Empty));
            var viewModel = new MainViewModel(service, fakeLauncher);
            var executable = Path.Combine(directory, "app.exe");
            File.WriteAllText(executable, string.Empty);

            viewModel.Launch(new AppItemViewModel(new AppEntry
            {
                Name = "Test",
                TargetPath = executable,
                Group = AppGroup.Ide
            }));

            Assert.False(viewModel.HasNotification);
            Assert.Equal(executable, fakeLauncher.LastPath);
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
    public void Search_FiltersOfficeIntoCombinedResults()
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
            viewModel.Load();

            viewModel.SearchText = "code";

            Assert.True(viewModel.IsSearching);
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
    public void ClearSearch_RestoresGroupedViews()
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
            viewModel.Load();
            viewModel.SearchText = "code";

            viewModel.SearchText = string.Empty;

            Assert.False(viewModel.IsSearching);
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
    public void AddApp_AddsManualEntryToSelectedGroup()
    {
        var directory = CreateTempDirectory();

        try
        {
            var viewModel = CreateViewModel(directory);
            viewModel.Load();

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
    public void UpdateApp_ChangesNamePathAndGroup()
    {
        var directory = CreateTempDirectory();

        try
        {
            var viewModel = CreateViewModel(directory);
            viewModel.Load();
            var item = viewModel.IdeApps[0];

            viewModel.UpdateApp(item, new AppEntry
            {
                Name = "Renamed Tool",
                TargetPath = @"C:\Tools\Renamed.exe",
                Group = AppGroup.AiTool
            });

            Assert.DoesNotContain(viewModel.IdeApps, app => app.Model.Id == item.Model.Id);
            Assert.Contains(viewModel.AiToolApps, app =>
                app.Name == "Renamed Tool" &&
                app.TargetPath == @"C:\Tools\Renamed.exe");
            Assert.Equal(3, viewModel.IdeApps.Count);
            Assert.Equal(7, viewModel.AiToolApps.Count);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RemoveGame_AddsHiddenPathAndRemovesFromList()
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
            viewModel.Load();
            var game = viewModel.GameApps.Single();

            viewModel.RemoveApp(game);

            Assert.Empty(viewModel.GameApps);
            var saved = store.Load();
            Assert.Contains(@"C:\Games\Test.exe", saved.HiddenGamePaths);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void MoveUpAndDown_ChangeOrderWithinGroup()
    {
        var directory = CreateTempDirectory();

        try
        {
            var viewModel = CreateViewModel(directory);
            viewModel.Load();
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
    public void MoveBefore_ReordersWithinGroup()
    {
        var directory = CreateTempDirectory();

        try
        {
            var viewModel = CreateViewModel(directory);
            viewModel.Load();
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
    public void MoveToEnd_AppendsWithinGroup()
    {
        var directory = CreateTempDirectory();

        try
        {
            var viewModel = CreateViewModel(directory);
            viewModel.Load();
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
    public void MoveApp_ChangesGroupAndAppendsOrder()
    {
        var directory = CreateTempDirectory();

        try
        {
            var viewModel = CreateViewModel(directory);
            viewModel.Load();
            var item = viewModel.IdeApps[0];

            viewModel.MoveApp(item, AppGroup.Game);

            Assert.DoesNotContain(viewModel.IdeApps, app => app.Model.Id == item.Model.Id);
            Assert.Contains(viewModel.GameApps, app => app.Model.Id == item.Model.Id);
            Assert.Equal(0, viewModel.GameApps.First(app => app.Model.Id == item.Model.Id).Model.Order);
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
