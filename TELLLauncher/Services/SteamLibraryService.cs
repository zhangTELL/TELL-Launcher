using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace TELLLauncher.Services;

public sealed record SteamGameInfo(string AppId, string Name, string InstallDir);

public sealed class SteamLibraryService
{
    private readonly IReadOnlyList<string> _steamAppsRoots;
    private readonly string? _steamInstallPath;

    public SteamLibraryService(string? steamInstallPath = null)
    {
        _steamInstallPath = string.IsNullOrWhiteSpace(steamInstallPath)
            ? DetectSteamInstallPath()
            : steamInstallPath;
        _steamAppsRoots = CreateLibraryRoots(_steamInstallPath);
    }

    public SteamLibraryService(IEnumerable<string> steamAppsRoots)
    {
        _steamAppsRoots = steamAppsRoots.ToList();
        _steamInstallPath = DetectSteamInstallPath();
    }

    public IReadOnlyList<SteamGameInfo> ScanInstalledGames()
    {
        var games = new List<SteamGameInfo>();

        foreach (var root in _steamAppsRoots)
        {
            foreach (var manifestPath in SafeEnumerateManifests(root))
            {
                var game = ParseManifest(manifestPath);
                if (game is not null &&
                    games.All(existing => existing.AppId != game.AppId))
                {
                    games.Add(game);
                }
            }
        }

        return games
            .OrderBy(game => game.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// 枚举库目录下的 appmanifest_*.acf。目录不存在或无权限时静默跳过，
    /// 避免整个游戏库扫描因单个目录失败而中断。
    /// </summary>
    private static IEnumerable<string> SafeEnumerateManifests(string root)
    {
        if (!Directory.Exists(root))
        {
            yield break;
        }

        List<string> files;
        try
        {
            files = Directory.EnumerateFiles(root)
                .Where(file =>
                    file.EndsWith(".acf", StringComparison.OrdinalIgnoreCase) &&
                    Path.GetFileName(file).StartsWith(
                        "appmanifest_",
                        StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        catch (Exception ex) when (
            ex is UnauthorizedAccessException or DirectoryNotFoundException or IOException)
        {
            yield break;
        }

        foreach (var file in files)
        {
            yield return file;
        }
    }

    public string? GetLocalCapsulePath(string appId)
    {
        return FindLocalCapsulePath(appId, _steamInstallPath);
    }

    public static string? FindLocalCapsulePath(
        string appId,
        string? steamInstallPath = null)
    {
        var installPath = steamInstallPath ?? DetectSteamInstallPath();
        if (string.IsNullOrWhiteSpace(installPath))
        {
            return null;
        }

        var cacheDirectory = Path.Combine(
            installPath,
            "appcache",
            "librarycache",
            appId);

        foreach (var fileName in new[]
                 {
                     "library_600x900.jpg",
                     "library_600x900_schinese.jpg",
                     "library_hero.jpg",
                     "library_hero_schinese.jpg",
                     "header_schinese.jpg",
                     "header.jpg"
                 })
        {
            var candidate = Path.Combine(cacheDirectory, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public static string? DetectSteamInstallPath()
    {
        try
        {
            using var localMachineKey = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\WOW6432Node\Valve\Steam");
            var localMachinePath = localMachineKey?.GetValue("InstallPath") as string;
            if (!string.IsNullOrWhiteSpace(localMachinePath) &&
                Directory.Exists(localMachinePath))
            {
                return localMachinePath;
            }

            using var currentUserKey = Registry.CurrentUser.OpenSubKey(
                @"Software\Valve\Steam");
            var currentUserPath = currentUserKey?.GetValue("SteamPath") as string;
            if (!string.IsNullOrWhiteSpace(currentUserPath) &&
                Directory.Exists(currentUserPath))
            {
                return currentUserPath;
            }
        }
        catch
        {
            // Registry access can fail in locked-down environments.
        }

        var knownPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Steam");
        return Directory.Exists(knownPath) ? knownPath : null;
    }

    private static IReadOnlyList<string> CreateLibraryRoots(string? steamInstallPath)
    {
        var roots = new List<string>();
        if (string.IsNullOrWhiteSpace(steamInstallPath))
        {
            return roots;
        }

        var baseSteamApps = Path.Combine(steamInstallPath, "steamapps");
        AddIfExists(roots, baseSteamApps);

        var libraryFoldersPath = Path.Combine(baseSteamApps, "libraryfolders.vdf");
        if (!File.Exists(libraryFoldersPath))
        {
            return roots;
        }

        foreach (var libraryPath in ParseLibraryPaths(libraryFoldersPath))
        {
            AddIfExists(roots, Path.Combine(libraryPath, "steamapps"));
        }

        return roots;
    }

    private static void AddIfExists(ICollection<string> roots, string path)
    {
        if (Directory.Exists(path))
        {
            roots.Add(path);
        }
    }

    private static SteamGameInfo? ParseManifest(string manifestPath)
    {
        var content = TryReadAllText(manifestPath);
        if (content is null)
        {
            return null;
        }

        var appId = ReadValue(content, "appid");
        var name = ReadValue(content, "name");
        var installDir = ReadValue(content, "installdir");

        if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return new SteamGameInfo(appId, name, installDir ?? string.Empty);
    }

    private static string? ReadValue(string content, string key)
    {
        var match = Regex.Match(
            content,
            $@"^\s*""{Regex.Escape(key)}""\s+""(.*)""\s*$",
            RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static IEnumerable<string> ParseLibraryPaths(string libraryFoldersPath)
    {
        var content = TryReadAllText(libraryFoldersPath);
        if (content is null)
        {
            yield break;
        }

        foreach (Match match in Regex.Matches(
                     content,
                     @"^\s*""path""\s+""(.*)""\s*$",
                     RegexOptions.Multiline))
        {
            var value = match.Groups[1].Value.Replace("\\\\", "\\");
            if (!string.IsNullOrWhiteSpace(value))
            {
                yield return value;
            }
        }
    }

    /// <summary>
    /// 读取文本文件，Steam 占用或权限不足时返回 null 而不是抛出，
    /// 使单个清单/配置文件的失败不影响整体扫描。
    /// </summary>
    private static string? TryReadAllText(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
