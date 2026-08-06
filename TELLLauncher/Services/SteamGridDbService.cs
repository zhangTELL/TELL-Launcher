using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace TELLLauncher.Services;

public sealed record SteamGridDbGame(
    long Id,
    string Name,
    bool Verified,
    IReadOnlyList<string> Types,
    long? ReleaseDate);

public sealed record SteamGridDbImage(
    long Id,
    int Score,
    string? Style,
    string Url,
    string? Thumb,
    int Width,
    int Height,
    string? Mime,
    int Upvotes,
    int Downvotes);

/// <summary>
/// SteamGridDB 封面图服务：按 Steam AppId 或游戏名搜索并下载竖版高清封面，
/// 缓存到本地目录；失败写 miss 标记，避免重复请求。
/// </summary>
public sealed class SteamGridDbService
{
    private const string BaseUrl = "https://www.steamgriddb.com/api/v2";
    private static readonly string[] PreferredDimensions =
        { "600x900", "920x430", "460x215" };

    private readonly string _cacheDirectory;
    private readonly HttpClient _httpClient;
    private readonly bool _hasApiKey;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _keyLocks = new();

    public SteamGridDbService(
        string cacheDirectory,
        HttpMessageHandler? messageHandler = null,
        string? apiKey = null)
    {
        _cacheDirectory = cacheDirectory;
        var resolvedApiKey = string.IsNullOrWhiteSpace(apiKey)
            ? SteamGridDbSettingsStore.TryLoadApiKey()
            : apiKey;
        _hasApiKey = !string.IsNullOrWhiteSpace(resolvedApiKey);
        _httpClient = messageHandler is null
            ? new HttpClient { Timeout = TimeSpan.FromSeconds(15) }
            : new HttpClient(messageHandler) { Timeout = TimeSpan.FromSeconds(15) };
        if (_hasApiKey)
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", resolvedApiKey);
        }
    }

    public async Task<string?> GetGridPathAsync(
        string? steamAppId,
        string gameName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(gameName))
        {
            return null;
        }

        var cacheKey = !string.IsNullOrWhiteSpace(steamAppId)
            ? $"steam-{steamAppId}"
            : $"search-{SanitizeName(gameName)}";

        var keyLock = _keyLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await keyLock.WaitAsync(cancellationToken);
        try
        {
            var cachedPath = FindCachedFile(cacheKey);
            if (cachedPath is not null)
            {
                return cachedPath;
            }

            if (!_hasApiKey)
            {
                return null;
            }

            if (HasMissMarker(cacheKey))
            {
                return null;
            }

            string? path = null;
            if (!string.IsNullOrWhiteSpace(steamAppId))
            {
                path = await TryGetBySteamAppIdAsync(
                    steamAppId,
                    cacheKey,
                    cancellationToken);
            }

            path ??= await TryGetByNameAsync(
                gameName,
                cacheKey,
                cancellationToken);

            if (path is null)
            {
                WriteMissMarker(cacheKey);
            }

            return path;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
        finally
        {
            keyLock.Release();
        }
    }

    private async Task<string?> TryGetBySteamAppIdAsync(
        string steamAppId,
        string cacheKey,
        CancellationToken cancellationToken)
    {
        var images = await GetGridsAsync(
            $"grids/steam/{Uri.EscapeDataString(steamAppId)}",
            cancellationToken);
        return await DownloadBestImageAsync(images, cacheKey, cancellationToken);
    }

    private async Task<string?> TryGetByNameAsync(
        string gameName,
        string cacheKey,
        CancellationToken cancellationToken)
    {
        var games = await SearchGamesAsync(gameName, cancellationToken);
        if (games.Count == 0)
        {
            return null;
        }

        var game = ChooseBestGame(games, gameName);
        var images = await GetGridsAsync(
            $"grids/game/{game.Id}",
            cancellationToken);
        return await DownloadBestImageAsync(images, cacheKey, cancellationToken);
    }

    private async Task<IReadOnlyList<SteamGridDbGame>> SearchGamesAsync(
        string gameName,
        CancellationToken cancellationToken)
    {
        using var response = await SendGetAsync(
            $"search/autocomplete/{Uri.EscapeDataString(gameName)}",
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Array.Empty<SteamGridDbGame>();
        }

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken));
        var data = GetDataArray(document.RootElement);
        if (data is null)
        {
            return Array.Empty<SteamGridDbGame>();
        }

        var games = new List<SteamGridDbGame>();
        foreach (var item in data.Value.EnumerateArray())
        {
            var id = GetInt64(item, "id");
            var name = GetString(item, "name");
            if (id is null || string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            games.Add(new SteamGridDbGame(
                id.Value,
                name,
                GetBoolean(item, "verified"),
                GetStringArray(item, "types"),
                GetNullableInt64(item, "release_date")));
        }

        return games;
    }

    private async Task<IReadOnlyList<SteamGridDbImage>> GetGridsAsync(
        string relativeUrl,
        CancellationToken cancellationToken)
    {
        var query = $"dimensions={string.Join(",", PreferredDimensions)}";
        using var response = await SendGetAsync(
            $"{relativeUrl}?{query}",
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Array.Empty<SteamGridDbImage>();
        }

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken));
        var data = GetDataArray(document.RootElement);
        if (data is null)
        {
            return Array.Empty<SteamGridDbImage>();
        }

        var images = new List<SteamGridDbImage>();
        foreach (var item in data.Value.EnumerateArray())
        {
            var url = GetString(item, "url");
            var id = GetInt64(item, "id");
            if (string.IsNullOrWhiteSpace(url) || id is null)
            {
                continue;
            }

            images.Add(new SteamGridDbImage(
                id.Value,
                GetInt32(item, "score") ?? 0,
                GetString(item, "style"),
                url,
                GetString(item, "thumb"),
                GetInt32(item, "width") ?? 0,
                GetInt32(item, "height") ?? 0,
                GetString(item, "mime"),
                GetInt32(item, "upvotes") ?? 0,
                GetInt32(item, "downvotes") ?? 0));
        }

        return images;
    }

    private async Task<HttpResponseMessage> SendGetAsync(
        string relativeUrl,
        CancellationToken cancellationToken)
    {
        return await _httpClient.GetAsync(
            $"{BaseUrl}/{relativeUrl}",
            cancellationToken);
    }

    private async Task<string?> DownloadBestImageAsync(
        IReadOnlyList<SteamGridDbImage> images,
        string cacheKey,
        CancellationToken cancellationToken)
    {
        var best = ChooseBestImage(images);
        if (best is null)
        {
            return null;
        }

        try
        {
            Directory.CreateDirectory(_cacheDirectory);
            var bytes = await _httpClient.GetByteArrayAsync(
                best.Url,
                cancellationToken);
            if (bytes.Length == 0)
            {
                return null;
            }

            var extension = Path.GetExtension(new Uri(best.Url).AbsolutePath);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".png";
            }

            var tempPath = Path.Combine(_cacheDirectory, cacheKey + ".tmp");
            await File.WriteAllBytesAsync(tempPath, bytes, cancellationToken);
            var targetPath = Path.Combine(
                _cacheDirectory,
                cacheKey + extension);
            File.Move(tempPath, targetPath, overwrite: true);
            return targetPath;
        }
        catch (Exception ex) when (
            ex is HttpRequestException or TaskCanceledException or IOException)
        {
            return null;
        }
    }

    private static SteamGridDbImage? ChooseBestImage(
        IReadOnlyList<SteamGridDbImage> images)
    {
        return images
            .OrderByDescending(GetImageRank)
            .ThenBy(image => image.Id)
            .FirstOrDefault();
    }

    private static long GetImageRank(SteamGridDbImage image)
    {
        long rank = 0;
        if (image.Width == 600 && image.Height == 900)
        {
            rank += 1_000_000;
        }
        else if (image.Width > 0 && image.Height > 0)
        {
            var ratio = (double)image.Width / image.Height;
            if (Math.Abs(ratio - 2.0 / 3.0) < 0.08)
            {
                rank += 900_000;
            }
            else if (Math.Abs(ratio - 920.0 / 430.0) < 0.08)
            {
                rank += 500_000;
            }

            rank += (long)image.Width * image.Height;
        }

        if (string.Equals(image.Mime, "image/png", StringComparison.OrdinalIgnoreCase))
        {
            rank += 50_000;
        }

        if (string.Equals(image.Style, "official", StringComparison.OrdinalIgnoreCase))
        {
            rank += 25_000;
        }
        else if (string.Equals(image.Style, "alternate", StringComparison.OrdinalIgnoreCase))
        {
            rank += 20_000;
        }

        rank += image.Upvotes * 10L - image.Downvotes * 5L;
        rank += Math.Clamp(image.Score, 0, 100) * 1_000L;
        return rank;
    }

    private static SteamGridDbGame ChooseBestGame(
        IReadOnlyList<SteamGridDbGame> games,
        string gameName)
    {
        return games
            .OrderByDescending(game =>
            {
                var rank = 0;
                if (game.Name.Equals(gameName, StringComparison.OrdinalIgnoreCase))
                {
                    rank += 100;
                }
                else if (game.Name.Contains(gameName, StringComparison.OrdinalIgnoreCase) ||
                         gameName.Contains(game.Name, StringComparison.OrdinalIgnoreCase))
                {
                    rank += 50;
                }

                if (game.Verified)
                {
                    rank += 20;
                }

                if (game.Types.Contains("steam", StringComparer.OrdinalIgnoreCase))
                {
                    rank += 10;
                }

                return rank;
            })
            .ThenBy(game => game.Id)
            .First();
    }

    private string? FindCachedFile(string cacheKey)
    {
        if (!Directory.Exists(_cacheDirectory))
        {
            return null;
        }

        foreach (var file in Directory.EnumerateFiles(
                     _cacheDirectory,
                     cacheKey + ".*"))
        {
            if (file.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) ||
                file.EndsWith(".miss", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return file;
        }

        return null;
    }

    private bool HasMissMarker(string cacheKey)
    {
        return File.Exists(Path.Combine(_cacheDirectory, cacheKey + ".miss"));
    }

    private void WriteMissMarker(string cacheKey)
    {
        try
        {
            Directory.CreateDirectory(_cacheDirectory);
            File.WriteAllText(
                Path.Combine(_cacheDirectory, cacheKey + ".miss"),
                string.Empty);
        }
        catch
        {
            // 标记写失败不阻塞，下次启动会重试
        }
    }

    private static string SanitizeName(string gameName)
    {
        var invalidCharacters = new HashSet<char>(Path.GetInvalidFileNameChars());
        var safeName = new string(gameName
            .Select(character =>
                invalidCharacters.Contains(character) ? '_' : character)
            .ToArray())
            .Trim();
        return string.IsNullOrWhiteSpace(safeName)
            ? "game"
            : safeName;
    }

    private static JsonElement? GetDataArray(JsonElement root)
    {
        if (!root.TryGetProperty("success", out var success) ||
            !success.GetBoolean() ||
            !root.TryGetProperty("data", out var data))
        {
            return null;
        }

        return data.ValueKind == JsonValueKind.Array
            ? data
            : null;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static long? GetInt64(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) &&
               value.TryGetInt64(out var result)
            ? result
            : null;
    }

    private static int? GetInt32(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) &&
               value.TryGetInt32(out var result)
            ? result
            : null;
    }

    private static long? GetNullableInt64(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) &&
               value.ValueKind == JsonValueKind.Number &&
               value.TryGetInt64(out var result)
            ? result
            : null;
    }

    private static bool GetBoolean(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) &&
               value.ValueKind == JsonValueKind.True;
    }

    private static IReadOnlyList<string> GetStringArray(
        JsonElement element,
        string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()!)
            .ToList();
    }
}
