using System.IO;
using System.Runtime.InteropServices;
using TELLLauncher.Models;

namespace TELLLauncher.Services;

public sealed class ShortcutScanner
{
    private readonly IReadOnlyList<string> _desktopDirectories;
    private readonly Func<string, string?> _shortcutTargetResolver;
    private readonly Func<string, string?> _shortcutIconResolver;
    private readonly Func<string, string?, bool> _gameFilter;

    /// <param name="desktopDirectories">扫描目录，默认取当前用户桌面与公共桌面。</param>
    /// <param name="shortcutTargetResolver">解析 .lnk 到真实目标的函数，默认走 WScript.Shell。</param>
    /// <param name="shortcutIconResolver">读取 .lnk 自带图标位置的函数，默认走 WScript.Shell。</param>
    /// <param name="gameFilter">
    /// 判定快捷方式是否为游戏，默认使用 <see cref="GameShortcutRules.IsGame"/> 的严格规则。
    /// 测试可注入自定义判定来隔离扫描逻辑本身。
    /// </param>
    public ShortcutScanner(
        IEnumerable<string>? desktopDirectories = null,
        Func<string, string?>? shortcutTargetResolver = null,
        Func<string, string?, bool>? gameFilter = null,
        Func<string, string?>? shortcutIconResolver = null)
    {
        _desktopDirectories = desktopDirectories?.ToList() ?? CreateDefaultDesktopDirectories();

        // 生产环境默认把 .lnk 解析到真实目标。若保留快捷方式自身的路径，去重键就变成了
        // 快捷方式文件名：同一游戏改个名会再生成一张卡片，快捷方式被移走后条目还会
        // 误判为"未定位"。测试可通过构造参数注入自己的解析器。
        _shortcutTargetResolver = shortcutTargetResolver ?? ResolveShortcutTarget;
        _shortcutIconResolver = shortcutIconResolver ?? ResolveShortcutIconLocation;
        _gameFilter = gameFilter ?? ((name, targetPath) => GameShortcutRules.IsGame(name, targetPath));
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

    private IReadOnlyList<AppEntry> MergeEntries(LauncherConfig config, IReadOnlyList<AppEntry> scannedEntries)
    {
        var existingGames = config.Apps
            .Where(app => app.Group == AppGroup.Game)
            .ToList();

        MigrateLauncherShortcutIcons(existingGames);

        // 已有条目要给出全部可能的键：历史配置存的是快捷方式本身，
        // 现在的扫描结果已解析为真实目标，两种形式都要能命中，否则升级后会重复添加
        var existingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in existingGames.SelectMany(GetKeys).Concat(config.HiddenGamePaths))
        {
            existingKeys.Add(key);
        }

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

    /// <summary>
    /// 迁移：旧版把游戏条目的 IconPath 存成 .lnk 自身或解析后的目标 exe。
    /// 聚合启动器（米哈游 HYP 等）的所有游戏快捷方式指向同一个 launcher.exe，
    /// 只有快捷方式自带的 IconLocation 才区分游戏，按它刷新图标。
    /// 仅当 IconPath 仍与 TargetPath 相同（即从未被单独指定过）时才改写。
    /// </summary>
    private void MigrateLauncherShortcutIcons(IReadOnlyList<AppEntry> existingGames)
    {
        foreach (var game in existingGames)
        {
            if (string.IsNullOrWhiteSpace(game.TargetPath) ||
                string.IsNullOrWhiteSpace(game.IconPath) ||
                !string.Equals(game.IconPath, game.TargetPath, StringComparison.OrdinalIgnoreCase) ||
                !game.TargetPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var iconLocation = _shortcutIconResolver(game.TargetPath);
            if (!string.IsNullOrWhiteSpace(iconLocation) && File.Exists(iconLocation))
            {
                game.IconPath = iconLocation;
            }
        }
    }

    private IReadOnlyList<AppEntry> BuildEntries(IReadOnlyList<string> shortcuts)
    {
        var entries = new List<AppEntry>(shortcuts.Count);
        var order = 0;

        foreach (var shortcutPath in shortcuts)
        {
            var name = Path.GetFileNameWithoutExtension(shortcutPath);
            string? targetPath;

            if (shortcutPath.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
            {
                targetPath = ParseUrlShortcut(shortcutPath);
            }
            else
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

                targetPath = string.IsNullOrWhiteSpace(resolvedTarget)
                    ? shortcutPath
                    : resolvedTarget;
            }

            // 只收录能确认为游戏的快捷方式。识别不出来的（浏览器、卸载程序、文档等）
            // 一律跳过，否则游戏栏目会被桌面上的无关快捷方式占满。
            if (!_gameFilter(name, targetPath))
            {
                continue;
            }

            // 快捷方式自带 IconLocation 且文件存在时优先用作图标：
            // 聚合启动器（米哈游 HYP 等）的所有游戏快捷方式目标都是同一个 launcher.exe，
            // 只有 IconLocation 里存的才是游戏本体图标
            var iconPath = targetPath;
            if (shortcutPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                var iconLocation = _shortcutIconResolver(shortcutPath);
                if (!string.IsNullOrWhiteSpace(iconLocation) && File.Exists(iconLocation))
                {
                    iconPath = iconLocation;
                }
            }

            entries.Add(new AppEntry
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = name,
                TargetPath = targetPath,
                IconPath = iconPath,
                Group = AppGroup.Game,
                Order = order++,
                IsHidden = false,
                IsManual = false
            });
        }

        return entries;
    }

    private static string? ParseUrlShortcut(string urlPath)
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
            // 文件无法读取时返回 null，保留卡片但无法启动
        }

        return null;
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

    /// <summary>
    /// 读取 .lnk 自带的图标位置。聚合启动器（米哈游 HYP 等）为每个游戏生成指向同一个
    /// launcher.exe 的快捷方式，仅 IconLocation 不同 —— 只解析目标 exe 会把所有游戏
    /// 都显示成启动器图标。返回 "path,index" 中的 path 部分；空路径（",0"，表示使用
    /// 目标默认图标）返回 null。
    /// </summary>
    public static string? ResolveShortcutIconLocation(string shortcutPath)
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
            var raw = (string?)shortcut.IconLocation;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var separator = raw.LastIndexOf(',');
            var path = separator >= 0 ? raw[..separator].Trim() : raw.Trim();
            return string.IsNullOrWhiteSpace(path) ? null : path;
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

    /// <summary>
    /// 只枚举桌面顶层的快捷方式。游戏快捷方式通常直接放在桌面，递归子目录会把解压包、
    /// 备份文件夹里的快捷方式一并收进来，遇到 junction/符号链接还有死循环风险。
    /// </summary>
    private static IEnumerable<string> SafeEnumerateShortcuts(string root)
    {
        if (!Directory.Exists(root))
        {
            yield break;
        }

        List<string> files;
        try
        {
            // 必须在这里立即求值：EnumerateFiles 与 Concat 都是延迟执行的，
            // 若推迟到 foreach 才枚举，catch 就拦不住真正抛出的 I/O 异常
            files = Directory.EnumerateFiles(root, "*.lnk")
                .Concat(Directory.EnumerateFiles(root, "*.url"))
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

    private static string GetKey(AppEntry entry)
    {
        var path = string.IsNullOrWhiteSpace(entry.TargetPath)
            ? entry.IconPath
            : entry.TargetPath;

        if (!string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        // 目标解析失败（如损坏的 .url）时用名称兜底。若退回随机生成的 Id，
        // 每次扫描都会把它当成新条目再添加一次，卡片会无限累积。
        return $"name:{entry.Name}";
    }

    /// <summary>
    /// 条目所有可能的去重键：目标路径、图标路径，以及快捷方式解析后的真实目标。
    /// </summary>
    private static IEnumerable<string> GetKeys(AppEntry entry)
    {
        foreach (var path in new[] { entry.TargetPath, entry.IconPath })
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            yield return path;

            if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                var resolved = ResolveShortcutTarget(path);
                if (!string.IsNullOrWhiteSpace(resolved))
                {
                    yield return resolved;
                }
            }
        }

        yield return GetKey(entry);
    }
}
