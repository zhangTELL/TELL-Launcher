using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace TELLLauncher.Services;

/// <summary>
/// Steam 游戏胶囊封面图服务：按 AppId 从 CDN 下载 library_600x900 竖版图，
/// 缓存到本地目录；下载失败写 miss 标记，避免重复请求。
/// </summary>
public sealed class CoverImageService
{
    private const string CdnUrlTemplate =
        "https://cdn.akamai.steamstatic.com/steam/apps/{0}/library_600x900.jpg";

    private static readonly Regex SteamAppIdPattern = new(
        @"^steam://rungameid/(\d+)/?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly string _cacheDirectory;
    private readonly HttpMessageHandler? _messageHandler;
    private HttpClient? _httpClient;

    public CoverImageService(string cacheDirectory, HttpMessageHandler? messageHandler = null)
    {
        _cacheDirectory = cacheDirectory;
        _messageHandler = messageHandler;
    }

    /// <summary>
    /// 从启动目标（如 steam://rungameid/730）中提取 Steam AppId，非 Steam 目标返回 null。
    /// </summary>
    public static string? ExtractSteamAppId(string? targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return null;
        }

        var match = SteamAppIdPattern.Match(targetPath.Trim());
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// 获取封面图本地路径：缓存命中直接返回；否则尝试下载并缓存；
    /// 失败或已标记 miss 时返回 null。
    /// </summary>
    public async Task<string?> GetCapsulePathAsync(string? appId)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return null;
        }

        var cachePath = Path.Combine(_cacheDirectory, $"{appId}.jpg");
        if (File.Exists(cachePath))
        {
            return cachePath;
        }

        var missMarkerPath = cachePath + ".miss";
        if (File.Exists(missMarkerPath))
        {
            return null;
        }

        try
        {
            Directory.CreateDirectory(_cacheDirectory);
            var bytes = await GetHttpClient()
                .GetByteArrayAsync(string.Format(CdnUrlTemplate, appId));

            if (bytes.Length == 0)
            {
                File.WriteAllText(missMarkerPath, string.Empty);
                return null;
            }

            // 先写临时文件再移动，避免中断留下半个图片文件
            var tempPath = cachePath + ".tmp";
            await File.WriteAllBytesAsync(tempPath, bytes);
            File.Move(tempPath, cachePath, overwrite: true);
            return cachePath;
        }
        catch (Exception ex) when (
            ex is HttpRequestException or TaskCanceledException or IOException)
        {
            try
            {
                File.WriteAllText(missMarkerPath, string.Empty);
            }
            catch
            {
                // 标记写失败不阻塞，下次启动会重试下载
            }

            return null;
        }
    }

    private HttpClient GetHttpClient()
    {
        return _httpClient ??= _messageHandler is null
            ? new HttpClient { Timeout = TimeSpan.FromSeconds(10) }
            : new HttpClient(_messageHandler) { Timeout = TimeSpan.FromSeconds(10) };
    }
}
