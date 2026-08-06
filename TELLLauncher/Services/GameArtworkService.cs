using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using TELLLauncher.Models;

namespace TELLLauncher.Services;

public sealed class GameArtworkService
{
    private const int MaxDepth = 6;
    private const int MaxCandidates = 400;

    private static readonly IReadOnlyDictionary<string, string> GameDirectoryKeywords =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["崩坏"] = "Star Rail",
            ["星穹"] = "Star Rail",
            ["绝区零"] = "ZenlessZoneZero",
            ["原神"] = "Genshin Impact",
            ["异环"] = "Neverness To Everness",
            ["鸣潮"] = "Wuthering Waves"
        };

    private static readonly (string GameKeyword, string IconFile)[] LauncherIconMappings =
    {
        ("星穹铁道", "hkrpg_cn.ico"),
        ("崩坏：星穹铁道", "hkrpg_cn.ico"),
        ("绝区零", "nap_cn.ico"),
        ("原神", "hk4e_cn.ico"),
        ("崩坏3", "bh3_cn.ico")
    };

    private readonly CoverImageService _coverImageService;

    public GameArtworkService(
        string cacheDirectory,
        HttpMessageHandler? messageHandler = null)
    {
        _coverImageService = new CoverImageService(cacheDirectory, messageHandler);
    }

    public GameArtworkService(CoverImageService coverImageService)
    {
        _coverImageService = coverImageService;
    }

    public async Task<string?> GetCapsulePathAsync(AppEntry app)
    {
        if (app.Group != AppGroup.Game)
        {
            return null;
        }

        var steamAppId = ResolveSteamAppId(app.TargetPath);
        if (steamAppId is not null)
        {
            return await _coverImageService.GetCapsulePathAsync(steamAppId);
        }

        var launchPath = ResolveLaunchPath(app.TargetPath);
        if (string.IsNullOrWhiteSpace(launchPath) || !File.Exists(launchPath))
        {
            return null;
        }

        return await Task.Run(() => FindHighResolutionImage(launchPath, app.Name));
    }

    public static string? FindHighResolutionImage(
        string launchPath,
        string? gameName = null)
    {
        var rootDirectory = Path.GetDirectoryName(launchPath);
        if (string.IsNullOrWhiteSpace(rootDirectory) ||
            !Directory.Exists(rootDirectory))
        {
            return null;
        }

        var launcherIcon = FindLauncherIcon(rootDirectory, gameName);
        if (launcherIcon is not null)
        {
            return launcherIcon;
        }

        var searchRoots = CreateSearchRoots(rootDirectory, gameName);
        var bestPath = (string?)null;
        var bestScore = -1L;
        var examined = 0;

        foreach (var candidate in EnumerateCandidates(searchRoots))
        {
            if (examined >= MaxCandidates)
            {
                break;
            }

            examined++;

            try
            {
                using var image = Image.FromFile(candidate);
                var area = (long)image.Width * image.Height;
                var score = area +
                            ScoreFileName(candidate) +
                            ScoreGameMatch(candidate, gameName);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestPath = candidate;
                }
            }
            catch
            {
                // 损坏或无法解码的图片直接跳过
            }
        }

        return bestPath;
    }

    public static string? ReadUrlShortcut(string urlPath)
    {
        try
        {
            foreach (var line in File.ReadLines(urlPath))
            {
                if (line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
                {
                    var url = line.Substring(4).Trim();
                    return string.IsNullOrWhiteSpace(url) ? null : url;
                }
            }
        }
        catch
        {
            // 读取失败时返回 null
        }

        return null;
    }

    private static string? ResolveSteamAppId(string? targetPath)
    {
        var steamAppId = CoverImageService.ExtractSteamAppId(targetPath);
        if (steamAppId is not null)
        {
            return steamAppId;
        }

        if (string.IsNullOrWhiteSpace(targetPath) ||
            !targetPath.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return CoverImageService.ExtractSteamAppId(ReadUrlShortcut(targetPath));
    }

    private static string? ResolveLaunchPath(string? targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return null;
        }

        if (targetPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            return ShortcutScanner.ResolveShortcutTarget(targetPath) ?? targetPath;
        }

        if (targetPath.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
        {
            return ReadUrlShortcut(targetPath) ?? targetPath;
        }

        return targetPath;
    }

    private static IReadOnlyList<SearchRoot> CreateSearchRoots(
        string rootDirectory,
        string? gameName)
    {
        if (string.IsNullOrWhiteSpace(gameName))
        {
            return new[] { new SearchRoot(rootDirectory, MaxDepth) };
        }

        if (DirectoryMatchesGame(rootDirectory, gameName))
        {
            return new[] { new SearchRoot(rootDirectory, MaxDepth) };
        }

        var matches = new List<string>();
        var pending = new Queue<(string Directory, int Depth)>();
        pending.Enqueue((rootDirectory, 0));

        while (pending.Count > 0)
        {
            var (directory, depth) = pending.Dequeue();
            if (depth >= 3)
            {
                continue;
            }

            IEnumerable<string> directories;
            try
            {
                directories = Directory.EnumerateDirectories(directory);
            }
            catch (Exception ex) when (
                ex is UnauthorizedAccessException or DirectoryNotFoundException or IOException)
            {
                continue;
            }

            foreach (var subdirectory in directories)
            {
                if (ShouldSkipDirectory(subdirectory))
                {
                    continue;
                }

                if (DirectoryMatchesGame(subdirectory, gameName))
                {
                    matches.Add(subdirectory);
                }

                pending.Enqueue((subdirectory, depth + 1));
            }
        }

        if (matches.Count == 0)
        {
            return new[] { new SearchRoot(rootDirectory, MaxDepth) };
        }

        var roots = new List<SearchRoot>
        {
            new(rootDirectory, 0)
        };
        roots.AddRange(matches.Select(path => new SearchRoot(path, MaxDepth)));
        return roots;
    }

    private static string? FindLauncherIcon(
        string rootDirectory,
        string? gameName)
    {
        if (string.IsNullOrWhiteSpace(gameName))
        {
            return null;
        }

        foreach (var mapping in LauncherIconMappings)
        {
            if (!gameName.Contains(
                    mapping.GameKeyword,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var iconPath = FindFileByName(rootDirectory, mapping.IconFile);
            if (iconPath is not null)
            {
                return iconPath;
            }
        }

        return null;
    }

    private static string? FindFileByName(string rootDirectory, string fileName)
    {
        var pending = new Stack<(string Directory, int Depth)>();
        pending.Push((rootDirectory, 0));

        while (pending.Count > 0)
        {
            var (directory, depth) = pending.Pop();
            if (depth > 4)
            {
                continue;
            }

            IEnumerable<string> directories;
            try
            {
                directories = Directory.EnumerateDirectories(directory);
            }
            catch (Exception ex) when (
                ex is UnauthorizedAccessException or DirectoryNotFoundException or IOException)
            {
                continue;
            }

            foreach (var subdirectory in directories)
            {
                if (!ShouldSkipDirectory(subdirectory))
                {
                    pending.Push((subdirectory, depth + 1));
                }
            }

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(directory);
            }
            catch (Exception ex) when (
                ex is UnauthorizedAccessException or DirectoryNotFoundException or IOException)
            {
                continue;
            }

            foreach (var file in files)
            {
                if (Path.GetFileName(file).Equals(
                        fileName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return file;
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateCandidates(
        IEnumerable<SearchRoot> searchRoots)
    {
        var pending = new Stack<(string Directory, int Depth, int MaxDepth)>();
        foreach (var root in searchRoots)
        {
            pending.Push((root.Path, 0, root.MaxDepth));
        }

        while (pending.Count > 0)
        {
            var (directory, depth, maxDepth) = pending.Pop();
            if (depth > maxDepth)
            {
                continue;
            }

            IEnumerable<string> directories;
            try
            {
                directories = Directory.EnumerateDirectories(directory);
            }
            catch (Exception ex) when (
                ex is UnauthorizedAccessException or DirectoryNotFoundException or IOException)
            {
                continue;
            }

            foreach (var subdirectory in directories)
            {
                if (!ShouldSkipDirectory(subdirectory))
                {
                    pending.Push((subdirectory, depth + 1, maxDepth));
                }
            }

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(directory);
            }
            catch (Exception ex) when (
                ex is UnauthorizedAccessException or DirectoryNotFoundException or IOException)
            {
                continue;
            }

            foreach (var file in files)
            {
                if (IsCandidateFile(file))
                {
                    yield return file;
                }
            }
        }
    }

    private static bool IsCandidateFile(string path)
    {
        var extension = Path.GetExtension(path);
        if (!extension.Equals(".png", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".webp", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".ico", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var fileName = Path.GetFileName(path);
        if (fileName.StartsWith('.'))
        {
            return false;
        }

        var lower = fileName.ToLowerInvariant();
        return lower.Contains("cover") ||
               lower.Contains("capsule") ||
               lower.Contains("header") ||
               lower.Contains("hero") ||
               lower.Contains("banner") ||
               lower.Contains("keyart") ||
               lower.Contains("boxart") ||
               lower.Contains("poster") ||
               lower.Contains("background") ||
               lower.Contains("wallpaper") ||
               lower.Contains("logo") ||
               lower.Contains("icon") ||
               lower.Contains("thumb") ||
               lower.Contains("art") ||
               lower.Contains("bg") ||
               lower.Contains("loading") ||
               lower.Contains("splash") ||
               lower.Contains("menu") ||
               lower.Contains("login") ||
               lower.Contains("start") ||
               lower.Contains("title") ||
               lower.Contains("screen");
    }

    private static bool ShouldSkipDirectory(string path)
    {
        var name = Path.GetFileName(path).ToLowerInvariant();
        return name is "logs" or "log" or "cache" or "webcache"
            or "screenshots" or "movies" or "videos" or "video"
            or "sound" or "music" or "audio" or "save" or "saves"
            or "config" or "cfg" or "bin" or "redist" or "crashreports";
    }

    private static bool DirectoryMatchesGame(string path, string gameName)
    {
        foreach (var pair in GameDirectoryKeywords)
        {
            if (gameName.Contains(pair.Key, StringComparison.OrdinalIgnoreCase) &&
                path.Contains(pair.Value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return path.Contains(gameName, StringComparison.OrdinalIgnoreCase);
    }

    private static long ScoreFileName(string path)
    {
        var lower = Path.GetFileName(path).ToLowerInvariant();
        long score = 0;
        if (lower.Contains("cover") || lower.Contains("capsule")) score += 4000;
        if (lower.Contains("hero") || lower.Contains("keyart")) score += 3000;
        if (lower.Contains("banner") || lower.Contains("header")) score += 2500;
        if (lower.Contains("poster") || lower.Contains("splash")) score += 2000;
        if (lower.Contains("background") || lower.Contains("wallpaper")) score += 1500;
        if (lower.Contains("logo")) score += 1000;
        if (lower.Contains("icon") || lower.Contains("thumb")) score += 500;
        if (lower.Contains("bg") || lower.Contains("loading") || lower.Contains("splash")) score += 800;
        if (lower.Contains("menu") || lower.Contains("login") || lower.Contains("start")) score += 600;
        if (lower.Contains("title") || lower.Contains("screen")) score += 600;
        return score;
    }

    private static long ScoreGameMatch(string path, string? gameName)
    {
        if (string.IsNullOrWhiteSpace(gameName))
        {
            return 0;
        }

        foreach (var pair in GameDirectoryKeywords)
        {
            if (gameName.Contains(pair.Key, StringComparison.OrdinalIgnoreCase) &&
                path.Contains(pair.Value, StringComparison.OrdinalIgnoreCase))
            {
                return 200000;
            }
        }

        return path.Contains(gameName, StringComparison.OrdinalIgnoreCase)
            ? 200000
            : 0;
    }

    private readonly struct SearchRoot
    {
        public SearchRoot(string path, int maxDepth)
        {
            Path = path;
            MaxDepth = maxDepth;
        }

        public string Path { get; }

        public int MaxDepth { get; }
    }
}
