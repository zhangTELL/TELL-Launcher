using TELLLauncher.Services;

namespace TELLLauncher.Tests;

public class SteamLibraryServiceTests
{
    [Fact]
    public void ScanInstalledGames_ReadsManifestsFromLibraryRoot()
    {
        var root = CreateTempDirectory();

        try
        {
            CreateManifest(root, "10", "Game Ten", "game10");
            CreateManifest(root, "20", "Alpha Game", "alpha");

            Assert.True(File.Exists(Path.Combine(root, "appmanifest_10.acf")));
            Assert.Contains(
                Path.Combine(root, "appmanifest_10.acf"),
                Directory.GetFiles(root));
            Assert.Equal(2, Directory.GetFiles(root)
                .Count(file => file.EndsWith(".acf", StringComparison.OrdinalIgnoreCase)));
            var games = new SteamLibraryService(new[] { root }).ScanInstalledGames();

            Assert.Equal(2, games.Count);
            Assert.Equal("Alpha Game", games[0].Name);
            Assert.Equal("Game Ten", games[1].Name);
            Assert.Equal("20", games[0].AppId);
            Assert.Equal("alpha", games[0].InstallDir);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ScanInstalledGames_ReadsAdditionalLibraryFolders()
    {
        var baseRoot = CreateTempDirectory();
        var extraRoot = CreateTempDirectory();

        try
        {
            var baseSteamApps = Path.Combine(baseRoot, "steamapps");
            Directory.CreateDirectory(baseSteamApps);
            File.WriteAllText(
                Path.Combine(baseSteamApps, "libraryfolders.vdf"),
                $"\"libraryfolders\"\r\n{{\r\n\t\"0\"\r\n\t{{\r\n\t\t\"path\"\t\t\"{extraRoot.Replace("\\", "\\\\")}\"\r\n\t}}\r\n}}\r\n");

            var extraSteamApps = Path.Combine(extraRoot, "steamapps");
            Directory.CreateDirectory(extraSteamApps);
            CreateManifest(extraSteamApps, "30", "Extra Game", "extra");
            Assert.Single(Directory.GetFiles(extraSteamApps)
                .Where(file => file.EndsWith(".acf", StringComparison.OrdinalIgnoreCase)));

            var games = new SteamLibraryService(baseRoot).ScanInstalledGames();

            var game = Assert.Single(games);
            Assert.Equal("Extra Game", game.Name);
            Assert.Equal("30", game.AppId);
        }
        finally
        {
            Directory.Delete(baseRoot, recursive: true);
            Directory.Delete(extraRoot, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"TELLLauncher.SteamLibrary.{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void CreateManifest(
        string root,
        string appId,
        string name,
        string installDir)
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(
            Path.Combine(root, $"appmanifest_{appId}.acf"),
            $"\"AppState\"\r\n{{\r\n\t\"appid\"\t\t\"{appId}\"\r\n\t\"name\"\t\t\"{name}\"\r\n\t\"installdir\"\t\t\"{installDir}\"\r\n}}\r\n");
    }
}
