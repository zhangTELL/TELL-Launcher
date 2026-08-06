using System.Net;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using TELLLauncher.Models;
using TELLLauncher.Services;

namespace TELLLauncher.Tests;

public class GameArtworkServiceTests
{
    [Fact]
    public void FindHighResolutionImage_ReturnsLargestRelevantImage()
    {
        var directory = CreateTempDirectory();

        try
        {
            SavePng(Path.Combine(directory, "icon.png"), 64, 64);
            var heroPath = Path.Combine(directory, "hero.png");
            SavePng(heroPath, 512, 512);

            var result = GameArtworkService.FindHighResolutionImage(
                Path.Combine(directory, "game.exe"));

            Assert.Equal(heroPath, result);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void FindHighResolutionImage_ReturnsNull_WhenNoRelevantImages()
    {
        var directory = CreateTempDirectory();

        try
        {
            SavePng(Path.Combine(directory, "random.png"), 256, 256);

            var result = GameArtworkService.FindHighResolutionImage(
                Path.Combine(directory, "game.exe"));

            Assert.Null(result);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void FindHighResolutionImage_ChoosesMatchingGameDirectory()
    {
        var directory = CreateTempDirectory();

        try
        {
            var starRailDirectory = Path.Combine(directory, "games", "Star Rail Game");
            var zenlessDirectory = Path.Combine(directory, "games", "ZenlessZoneZero Game");
            Directory.CreateDirectory(starRailDirectory);
            Directory.CreateDirectory(zenlessDirectory);

            var expected = Path.Combine(starRailDirectory, "hero.png");
            SavePng(expected, 800, 600);
            SavePng(Path.Combine(zenlessDirectory, "hero.png"), 1200, 900);

            var result = GameArtworkService.FindHighResolutionImage(
                Path.Combine(directory, "launcher.exe"),
                "崩坏：星穹铁道");

            Assert.Equal(expected, result);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void FindHighResolutionImage_ReturnsMiHoYoLauncherIcon()
    {
        var directory = CreateTempDirectory();

        try
        {
            var iconDirectory = Path.Combine(directory, "1.17.0.376", "ico");
            Directory.CreateDirectory(iconDirectory);
            var expected = Path.Combine(iconDirectory, "hkrpg_cn.ico");
            File.WriteAllText(expected, "fake-icon");

            var result = GameArtworkService.FindHighResolutionImage(
                Path.Combine(directory, "launcher.exe"),
                "崩坏：星穹铁道");

            Assert.Equal(expected, result);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ReadUrlShortcut_ReturnsUrl()
    {
        var directory = CreateTempDirectory();

        try
        {
            var urlPath = Path.Combine(directory, "game.url");
            File.WriteAllText(urlPath, "[InternetShortcut]\r\nURL=steam://rungameid/730\r\n");

            var result = GameArtworkService.ReadUrlShortcut(urlPath);

            Assert.Equal("steam://rungameid/730", result);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task GetCapsulePath_PrefersSteamGridDbOverLocalSearch()
    {
        var directory = CreateTempDirectory();

        try
        {
            var gameDirectory = Path.Combine(directory, "game");
            Directory.CreateDirectory(gameDirectory);
            File.WriteAllText(
                Path.Combine(gameDirectory, "game.exe"),
                "fake-exe");
            SavePng(Path.Combine(gameDirectory, "hero.png"), 512, 512);

            var app = new AppEntry
            {
                Name = "Test Game",
                Group = AppGroup.Game,
                TargetPath = Path.Combine(gameDirectory, "game.exe")
            };
            var handler = new FakeApiFirstHandler();
            var service = new GameArtworkService(
                Path.Combine(directory, "cache"),
                handler,
                new SteamGridDbService(
                    Path.Combine(directory, "sgdb-cache"),
                    handler,
                    "test-api-key"));

            var result = await service.GetCapsulePathAsync(app);

            Assert.NotNull(result);
            Assert.StartsWith(
                Path.Combine(directory, "sgdb-cache"),
                result);
            Assert.Equal(1, handler.SearchCallCount);
            Assert.Equal(1, handler.GameGridCallCount);
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
            $"TELLLauncher.GameArtwork.{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void SavePng(string path, int width, int height)
    {
        using var bitmap = new Bitmap(width, height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.DarkSlateBlue);
        bitmap.Save(path, ImageFormat.Png);
    }

    private sealed class FakeApiFirstHandler : HttpMessageHandler
    {
        public int SearchCallCount { get; private set; }

        public int GameGridCallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            if (uri.Host == "cdn.example")
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(new byte[] { 1, 2, 3, 4 })
                });
            }

            if (uri.AbsolutePath.Contains(
                    "/search/autocomplete/",
                    StringComparison.OrdinalIgnoreCase))
            {
                SearchCallCount++;
                return Task.FromResult(JsonResponse(SearchJson));
            }

            if (uri.AbsolutePath.Contains(
                    "/grids/game/",
                    StringComparison.OrdinalIgnoreCase))
            {
                GameGridCallCount++;
                return Task.FromResult(JsonResponse(GridsJson));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        private static HttpResponseMessage JsonResponse(string json)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }

        private const string SearchJson =
            """
            {
              "success": true,
              "data": [
                {
                  "id": 42,
                  "name": "Test Game",
                  "verified": true,
                  "types": []
                }
              ]
            }
            """;

        private const string GridsJson =
            """
            {
              "success": true,
              "data": [
                {
                  "id": 7,
                  "score": 1,
                  "style": "alternate",
                  "width": 600,
                  "height": 900,
                  "mime": "image/png",
                  "url": "https://cdn.example/grid/test.png",
                  "thumb": "https://cdn.example/thumb/test.jpg",
                  "upvotes": 0,
                  "downvotes": 0
                }
              ]
            }
            """;
    }
}
