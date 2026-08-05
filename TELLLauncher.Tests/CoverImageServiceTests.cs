using System.Net;
using TELLLauncher.Services;

namespace TELLLauncher.Tests;

public class CoverImageServiceTests
{
    [Theory]
    [InlineData("steam://rungameid/730", "730")]
    [InlineData("steam://rungameid/1245620", "1245620")]
    [InlineData("STEAM://RUNGAMEID/570", "570")]
    [InlineData("steam://rungameid/440/", "440")]
    public void ExtractSteamAppId_ReturnsAppId_ForSteamUrls(
        string targetPath, string expected)
    {
        Assert.Equal(expected, CoverImageService.ExtractSteamAppId(targetPath));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(@"C:\Games\App.exe")]
    [InlineData("https://example.com")]
    [InlineData("steam://library")]
    [InlineData("steam://rungameid/")]
    [InlineData("steam://rungameid/abc")]
    public void ExtractSteamAppId_ReturnsNull_ForNonSteamUrls(string? targetPath)
    {
        Assert.Null(CoverImageService.ExtractSteamAppId(targetPath));
    }

    [Fact]
    public async Task GetCapsulePath_ReturnsCachedFile_WithoutHttpCall()
    {
        var directory = CreateTempDirectory();

        try
        {
            var cachedPath = Path.Combine(directory, "730.jpg");
            File.WriteAllText(cachedPath, "fake-jpg");
            var handler = new CountingHandler(HttpStatusCode.OK, new byte[] { 1 });
            var service = new CoverImageService(directory, handler);

            var result = await service.GetCapsulePathAsync("730");

            Assert.Equal(cachedPath, result);
            Assert.Equal(0, handler.CallCount);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task GetCapsulePath_DownloadsAndCaches_OnMiss()
    {
        var directory = CreateTempDirectory();

        try
        {
            var handler = new CountingHandler(
                HttpStatusCode.OK, new byte[] { 0xFF, 0xD8, 0xFF });
            var service = new CoverImageService(directory, handler);

            var result = await service.GetCapsulePathAsync("730");

            Assert.NotNull(result);
            Assert.True(File.Exists(result));
            Assert.Equal(1, handler.CallCount);

            // 第二次命中缓存，不再请求网络
            var second = await service.GetCapsulePathAsync("730");
            Assert.Equal(result, second);
            Assert.Equal(1, handler.CallCount);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task GetCapsulePath_WritesMissMarker_OnHttpFailure()
    {
        var directory = CreateTempDirectory();

        try
        {
            var handler = new CountingHandler(HttpStatusCode.NotFound, null);
            var service = new CoverImageService(directory, handler);

            var result = await service.GetCapsulePathAsync("999999");

            Assert.Null(result);
            Assert.True(File.Exists(
                Path.Combine(directory, "999999.jpg.miss")));

            // 已标记 miss，不再请求网络
            var second = await service.GetCapsulePathAsync("999999");
            Assert.Null(second);
            Assert.Equal(1, handler.CallCount);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task GetCapsulePath_ReturnsNull_ForNullAppId()
    {
        var directory = CreateTempDirectory();

        try
        {
            var handler = new CountingHandler(HttpStatusCode.OK, new byte[] { 1 });
            var service = new CoverImageService(directory, handler);

            Assert.Null(await service.GetCapsulePathAsync(null));
            Assert.Null(await service.GetCapsulePathAsync(""));
            Assert.Equal(0, handler.CallCount);
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
            $"TELLLauncher.Covers.{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly byte[]? _payload;

        public CountingHandler(HttpStatusCode statusCode, byte[]? payload)
        {
            _statusCode = statusCode;
            _payload = payload;
        }

        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;

            if (_payload is null)
            {
                return Task.FromResult(new HttpResponseMessage(_statusCode));
            }

            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new ByteArrayContent(_payload)
            });
        }
    }
}
