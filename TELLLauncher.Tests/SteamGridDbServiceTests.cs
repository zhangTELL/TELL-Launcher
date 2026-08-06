using System.Net;
using System.Text;
using TELLLauncher.Services;

namespace TELLLauncher.Tests;

public class SteamGridDbServiceTests
{
    [Fact]
    public async Task GetGridPathAsync_WithSteamAppId_DownloadsAndCaches()
    {
        var directory = CreateTempDirectory();

        try
        {
            var handler = new FakeSteamGridDbHandler();
            var service = new SteamGridDbService(
                directory,
                handler,
                "test-api-key");

            var first = await service.GetGridPathAsync(
                "440",
                "Team Fortress 2");
            var second = await service.GetGridPathAsync(
                "440",
                "Team Fortress 2");

            Assert.NotNull(first);
            Assert.True(File.Exists(first));
            Assert.Equal(first, second);
            Assert.Equal(1, handler.SteamGridCallCount);
            Assert.Equal(0, handler.SearchCallCount);
            Assert.Equal(1, handler.ImageCallCount);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task GetGridPathAsync_WithoutSteamAppId_SearchesByName()
    {
        var directory = CreateTempDirectory();

        try
        {
            var handler = new FakeSteamGridDbHandler();
            var service = new SteamGridDbService(
                directory,
                handler,
                "test-api-key");

            var path = await service.GetGridPathAsync(
                null,
                "Genshin Impact");

            Assert.NotNull(path);
            Assert.True(File.Exists(path));
            Assert.Equal(0, handler.SteamGridCallCount);
            Assert.Equal(1, handler.SearchCallCount);
            Assert.Equal(1, handler.GameGridCallCount);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task GetGridPathAsync_PrefersPortraitPng()
    {
        var directory = CreateTempDirectory();

        try
        {
            var handler = new FakeSteamGridDbHandler();
            var service = new SteamGridDbService(
                directory,
                handler,
                "test-api-key");

            await service.GetGridPathAsync("440", "Team Fortress 2");

            Assert.EndsWith("portrait.png", handler.LastImageUrl);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task GetGridPathAsync_WritesMissMarker_WhenNoGrids()
    {
        var directory = CreateTempDirectory();

        try
        {
            var handler = new FakeSteamGridDbHandler(emptyGrids: true);
            var service = new SteamGridDbService(
                directory,
                handler,
                "test-api-key");

            var first = await service.GetGridPathAsync(
                "440",
                "Team Fortress 2");
            var second = await service.GetGridPathAsync(
                "440",
                "Team Fortress 2");

            Assert.Null(first);
            Assert.Null(second);
            Assert.True(File.Exists(
                Path.Combine(directory, "steam-440.miss")));
            Assert.Equal(1, handler.SteamGridCallCount);
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
            $"TELLLauncher.SteamGridDb.{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class FakeSteamGridDbHandler : HttpMessageHandler
    {
        private readonly bool _emptyGrids;

        public FakeSteamGridDbHandler(bool emptyGrids = false)
        {
            _emptyGrids = emptyGrids;
        }

        public int SearchCallCount { get; private set; }

        public int SteamGridCallCount { get; private set; }

        public int GameGridCallCount { get; private set; }

        public int ImageCallCount { get; private set; }

        public string? LastImageUrl { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            if (uri.Host == "cdn.example")
            {
                ImageCallCount++;
                LastImageUrl = uri.AbsoluteUri;
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
                    "/grids/steam/",
                    StringComparison.OrdinalIgnoreCase))
            {
                SteamGridCallCount++;
                return Task.FromResult(JsonResponse(
                    _emptyGrids ? EmptyGridsJson : GridsJson));
            }

            if (uri.AbsolutePath.Contains(
                    "/grids/game/",
                    StringComparison.OrdinalIgnoreCase))
            {
                GameGridCallCount++;
                return Task.FromResult(JsonResponse(
                    _emptyGrids ? EmptyGridsJson : GridsJson));
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
                  "id": 10602,
                  "name": "Team Fortress 2",
                  "verified": true,
                  "types": ["steam"]
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
                  "id": 1,
                  "score": 1,
                  "style": "alternate",
                  "width": 920,
                  "height": 430,
                  "mime": "image/jpeg",
                  "url": "https://cdn.example/grid/wide.jpg",
                  "thumb": "https://cdn.example/thumb/wide.jpg",
                  "upvotes": 0,
                  "downvotes": 0
                },
                {
                  "id": 2,
                  "score": 1,
                  "style": "alternate",
                  "width": 600,
                  "height": 900,
                  "mime": "image/png",
                  "url": "https://cdn.example/grid/portrait.png",
                  "thumb": "https://cdn.example/thumb/portrait.jpg",
                  "upvotes": 0,
                  "downvotes": 0
                }
              ]
            }
            """;

        private const string EmptyGridsJson =
            """
            {
              "success": true,
              "data": []
            }
            """;
    }
}
