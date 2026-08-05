using System.IO;
using System.Runtime.InteropServices;
using TELLLauncher.Models;

namespace TELLLauncher.Services;

public sealed class ShortcutScanner
{
    private readonly IReadOnlyList<string> _desktopDirectories;
    private readonly Func<string, string?> _shortcutTargetResolver;

    public ShortcutScanner(
        IEnumerable<string>? desktopDirectories = null,
        Func<string, string?>? shortcutTargetResolver = null)
    {
        _desktopDirectories = desktopDirectories?.ToList() ?? CreateDefaultDesktopDirectories();
        _shortcutTargetResolver = shortcutTargetResolver ?? (_ => null);
    }

    public IReadOnlyList<AppEntry> Scan()
    {
        var shortcuts = _desktopDirectories
            .SelectMany(SafeEnumerateShortcuts)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return BuildEntries(shortcuts);
    }

    public async Task<IReadOnlyList<AppEntry>> ScanAsync()
    {
        var result = await Task.Run(() =>
        {
            var shortcuts = _desktopDirectories
                .SelectMany(SafeEnumerateShortcuts)
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return BuildEntries(shortcuts);
        });

        return result;
    }

    public IReadOnlyList<AppEntry> ScanAndMerge(LauncherConfig config)
    {
        var scannedEntries = Scan();
        return MergeEntries(config, scannedEntries);
    }

    public async Task<IReadOnlyList<AppEntry>> ScanAndMergeAsync(LauncherConfig config)
    {
        var scannedEntries = await ScanAsync();
        return MergeEntries(config, scannedEntries);
    }

    private static IReadOnlyList<AppEntry> MergeEntries(LauncherConfig config, IReadOnlyList<AppEntry> scannedEntries)
    {
        var existingGames = config.Apps
            .Where(app => app.Group == AppGroup.Game)
            .ToList();

        var existingKeys = new HashSet<string>(
            existingGames.Select(GetKey).Concat(config.HiddenGamePaths),
            StringComparer.OrdinalIgnoreCase);

        var nextOrder = existingGames.Count == 0
            ? 0
            : existingGames.Max(app => app.Order) + 1;

        foreach (var entry in scannedEntries)
        {
            if (!existingKeys.Add(GetKey(entry)))
            {
                continue;
            }

            entry.Order = nextOrder++;
            config.Apps.Add(entry);
        }

        return config.Apps
            .Where(app => app.Group == AppGroup.Game && !app.IsHidden)
            .OrderBy(app => app.Order)
            .ToList();
    }

    private IReadOnlyList<AppEntry> BuildEntries(IReadOnlyList<string> shortcuts)
    {
        var entries = new List<AppEntry>(shortcuts.Count);
        var order = 0;

        foreach (var shortcutPath in shortcuts)
        {
            string? resolvedTarget;
            try
            {
                resolvedTarget = _shortcutTargetResolver(shortcutPath);
            }
            catch
            {
                resolvedTarget = null;
            }

            var targetPath = string.IsNullOrWhiteSpace(resolvedTarget)
                ? shortcutPath
                : resolvedTarget;

            entries.Add(new AppEntry
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = Path.GetFileNameWithoutExtension(shortcutPath),
                TargetPath = targetPath,
                IconPath = targetPath,
                Group = AppGroup.Game,
                Order = order++,
                IsHidden = false,
                IsManual = false
            });
        }

        return entries;
    }

    public static string? ResolveShortcutTarget(string shortcutPath)
    {
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null)
            {
                return null;
            }

            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            var targetPath = (string?)shortcut.TargetPath;
            return string.IsNullOrWhiteSpace(targetPath) ? null : targetPath;
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyList<string> CreateDefaultDesktopDirectories()
    {
        var directories = new List<string>();
        AddIfExists(directories, Environment.GetFolderPath(
            Environment.SpecialFolder.DesktopDirectory));
        AddIfExists(directories, Environment.GetFolderPath(
            Environment.SpecialFolder.CommonDesktopDirectory));
        return directories;
    }

    private static void AddIfExists(ICollection<string> directories, string path)
    {
        if (Directory.Exists(path))
        {
            directories.Add(path);
        }
    }

    private static IEnumerable<string> SafeEnumerateShortcuts(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var directory = pending.Pop();

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
                pending.Push(subdirectory);
            }

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(directory, "*.lnk");
            }
            catch (Exception ex) when (
                ex is UnauthorizedAccessException or DirectoryNotFoundException or IOException)
            {
                continue;
            }

            foreach (var file in files)
            {
                yield return file;
            }
        }
    }

    private static string GetKey(AppEntry entry)
    {
        var path = string.IsNullOrWhiteSpace(entry.TargetPath)
            ? entry.IconPath
            : entry.TargetPath;

        return string.IsNullOrWhiteSpace(path) ? entry.Id : path;
    }
}
