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
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var manifestPath in Directory.EnumerateFiles(root)
                         .Where(file =>
                             file.EndsWith(".acf", StringComparison.OrdinalIgnoreCase) &&
                             Path.GetFileName(file).StartsWith(
                                 "appmanifest_",
                                 StringComparison.OrdinalIgnoreCase)))
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
        var content = File.ReadAllText(manifestPath);
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
        var content = File.ReadAllText(libraryFoldersPath);
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
}
