using TELLLauncher.Models;
using TELLLauncher.Services;

namespace TELLLauncher.Tests;

public class ShortcutScannerTests
{
    [Fact]
    public void Scan_ReturnsGameEntriesFromDesktopDirectories()
    {
        var desktop = CreateTempDirectory();

        try
        {
            CreateFile(desktop, @"Game One.lnk");
            CreateFile(desktop, @"Game Two.lnk");

            var entries = new ShortcutScanner(
                    new[] { desktop },
                    _ => null)
                .Scan();

            Assert.Equal(2, entries.Count);
            Assert.Equal(new[] { "Game One", "Game Two" }, entries.Select(entry => entry.Name));
            Assert.All(entries, entry => Assert.Equal(AppGroup.Game, entry.Group));
            Assert.All(entries, entry => Assert.EndsWith(".lnk", entry.TargetPath!));
            Assert.Equal(0, entries[0].Order);
            Assert.Equal(1, entries[1].Order);
        }
        finally
        {
            Directory.Delete(desktop, recursive: true);
        }
    }

    [Fact]
    public void ScanAndMerge_DoesNotDuplicateExistingGames()
    {
        var desktop = CreateTempDirectory();
        var config = new LauncherConfig();
        config.Apps.Add(new AppEntry
        {
            Name = "Existing Game",
            TargetPath = @"C:\Games\Existing.exe",
            IconPath = @"C:\Games\Existing.exe",
            Group = AppGroup.Game,
            Order = 0
        });

        try
        {
            CreateFile(desktop, @"Existing Game.lnk");
            CreateFile(desktop, @"New Game.lnk");

            string? Resolver(string shortcutPath) =>
                shortcutPath.EndsWith("Existing Game.lnk", StringComparison.OrdinalIgnoreCase)
                    ? @"C:\Games\Existing.exe"
                    : @"C:\Games\New.exe";

            var games = new ShortcutScanner(new[] { desktop }, Resolver)
                .ScanAndMerge(config);

            Assert.Equal(2, games.Count);
            Assert.Equal(
                new[] { "Existing Game", "New Game" },
                games.Select(entry => entry.Name).OrderBy(name => name));
            Assert.Equal(2, config.Apps.Count(app => app.Group == AppGroup.Game));
        }
        finally
        {
            Directory.Delete(desktop, recursive: true);
        }
    }

    [Fact]
    public void ScanAndMerge_DoesNotRestoreHiddenGames()
    {
        var desktop = CreateTempDirectory();
        var config = new LauncherConfig();
        config.HiddenGamePaths.Add(@"C:\Hidden\HiddenGame.exe");

        try
        {
            CreateFile(desktop, @"Hidden Game.lnk");

            var games = new ShortcutScanner(
                    new[] { desktop },
                    _ => @"C:\Hidden\HiddenGame.exe")
                .ScanAndMerge(config);

            Assert.Empty(games);
            Assert.Empty(config.Apps.Where(app => app.Group == AppGroup.Game));
        }
        finally
        {
            Directory.Delete(desktop, recursive: true);
        }
    }

    [Fact]
    public void Scan_HandlesResolverFailureGracefully()
    {
        var desktop = CreateTempDirectory();

        try
        {
            CreateFile(desktop, @"Broken Game.lnk");

            var entries = new ShortcutScanner(
                    new[] { desktop },
                    _ => throw new InvalidOperationException("resolver failed"))
                .Scan();

            var entry = Assert.Single(entries);
            Assert.Equal("Broken Game", entry.Name);
            Assert.EndsWith("Broken Game.lnk", entry.TargetPath!);
        }
        finally
        {
            Directory.Delete(desktop, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"TELLLauncher.ShortcutScanner.{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void CreateFile(string root, string relativePath)
    {
        var path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Empty);
    }
}
