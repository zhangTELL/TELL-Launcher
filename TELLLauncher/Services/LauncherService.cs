using System.IO;
using TELLLauncher.Models;

namespace TELLLauncher.Services;

public sealed class LauncherService
{
    private readonly ConfigStore _configStore;
    private readonly AppLocator _appLocator;
    private readonly ShortcutScanner _shortcutScanner;

    public LauncherService(
        ConfigStore configStore,
        AppLocator? appLocator = null,
        ShortcutScanner? shortcutScanner = null)
    {
        _configStore = configStore;
        _appLocator = appLocator ?? new AppLocator();
        _shortcutScanner = shortcutScanner ?? new ShortcutScanner();
    }

    public LauncherConfig LoadOrCreate()
    {
        var config = _configStore.Load();

        if (!config.DefaultsInitialized)
        {
            foreach (var defaultEntry in _appLocator.CreateDefaultOfficeEntries())
            {
                var exists = config.Apps.Any(app =>
                    app.Name.Equals(defaultEntry.Name, StringComparison.OrdinalIgnoreCase) &&
                    app.Group == defaultEntry.Group);

                if (!exists)
                {
                    config.Apps.Add(defaultEntry);
                }
            }

            config.DefaultsInitialized = true;
        }

        if (config.Version < 2)
        {
            RefreshUnresolvedOfficePaths(config);
            config.Version = 2;
        }

        if (config.Version < 3)
        {
            PruneNonGameEntries(config);
            config.Version = 3;
        }

        if (config.Version < 4)
        {
            BackfillDefaultDetails(config);
            config.Version = 4;
        }

        EnsureSteamLibraryCard(config);
        _shortcutScanner.ScanAndMerge(config);
        _configStore.Save(config);
        return config;
    }

    public async Task<LauncherConfig> LoadOrCreateAsync()
    {
        var config = _configStore.Load();

        if (!config.DefaultsInitialized)
        {
            var defaultEntries = await _appLocator.CreateDefaultOfficeEntriesAsync();
            foreach (var defaultEntry in defaultEntries)
            {
                var exists = config.Apps.Any(app =>
                    app.Name.Equals(defaultEntry.Name, StringComparison.OrdinalIgnoreCase) &&
                    app.Group == defaultEntry.Group);

                if (!exists)
                {
                    config.Apps.Add(defaultEntry);
                }
            }

            config.DefaultsInitialized = true;
        }

        if (config.Version < 2)
        {
            await RefreshUnresolvedOfficePathsAsync(config);
            config.Version = 2;
        }

        if (config.Version < 3)
        {
            PruneNonGameEntries(config);
            config.Version = 3;
        }

        if (config.Version < 4)
        {
            BackfillDefaultDetails(config);
            config.Version = 4;
        }

        EnsureSteamLibraryCard(config);
        await _shortcutScanner.ScanAndMergeAsync(config);
        _configStore.Save(config);
        return config;
    }

    public IReadOnlyList<AppEntry> RefreshGames(LauncherConfig config)
    {
        var games = _shortcutScanner.ScanAndMerge(config);
        _configStore.Save(config);
        return games;
    }

    public async Task<IReadOnlyList<AppEntry>> RefreshGamesAsync(LauncherConfig config)
    {
        var games = await _shortcutScanner.ScanAndMergeAsync(config);
        _configStore.Save(config);
        return games;
    }

    public void Save(LauncherConfig config)
    {
        _configStore.Save(config);
    }

    private void RefreshUnresolvedOfficePaths(LauncherConfig config)
    {
        var hasUnresolved = config.Apps.Any(app =>
            !app.IsManual &&
            (app.Group == AppGroup.Ide || app.Group == AppGroup.AiTool) &&
            string.IsNullOrWhiteSpace(app.TargetPath));

        if (!hasUnresolved)
        {
            return;
        }

        foreach (var defaultEntry in _appLocator.CreateDefaultOfficeEntries())
        {
            if (string.IsNullOrWhiteSpace(defaultEntry.TargetPath))
            {
                continue;
            }

            var existing = config.Apps.FirstOrDefault(app =>
                !app.IsManual &&
                app.Group == defaultEntry.Group &&
                app.Name.Equals(defaultEntry.Name, StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(app.TargetPath));

            if (existing is not null)
            {
                existing.TargetPath = defaultEntry.TargetPath;
                existing.IconPath = defaultEntry.IconPath;
                if (string.IsNullOrWhiteSpace(existing.Details))
                {
                    existing.Details = defaultEntry.Details;
                }
            }
        }
    }

    private async Task RefreshUnresolvedOfficePathsAsync(LauncherConfig config)
    {
        var hasUnresolved = config.Apps.Any(app =>
            !app.IsManual &&
            (app.Group == AppGroup.Ide || app.Group == AppGroup.AiTool) &&
            string.IsNullOrWhiteSpace(app.TargetPath));

        if (!hasUnresolved)
        {
            return;
        }

        var defaultEntries = await _appLocator.CreateDefaultOfficeEntriesAsync();
        foreach (var defaultEntry in defaultEntries)
        {
            if (string.IsNullOrWhiteSpace(defaultEntry.TargetPath))
            {
                continue;
            }

            var existing = config.Apps.FirstOrDefault(app =>
                !app.IsManual &&
                app.Group == defaultEntry.Group &&
                app.Name.Equals(defaultEntry.Name, StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(app.TargetPath));

            if (existing is not null)
            {
                existing.TargetPath = defaultEntry.TargetPath;
                existing.IconPath = defaultEntry.IconPath;
                if (string.IsNullOrWhiteSpace(existing.Details))
                {
                    existing.Details = defaultEntry.Details;
                }
            }
        }
    }

    /// <summary>
    /// 迁移到 v3：清理早期"扫到什么算什么"阶段误收进来的非游戏条目。
    /// 只处理自动扫描得到的（非手动添加），并把其路径写入隐藏列表 ——
    /// 否则桌面上的同一个快捷方式会在下次扫描时被重新加回来。
    /// </summary>
    private static void PruneNonGameEntries(LauncherConfig config)
    {
        var stale = config.Apps
            .Where(app => app.Group == AppGroup.Game
                          && !app.IsManual
                          && !app.IsSteamLibrary
                          && !GameShortcutRules.IsGame(app.Name, app.TargetPath))
            .ToList();

        foreach (var app in stale)
        {
            config.Apps.Remove(app);

            var key = app.TargetPath;
            if (!string.IsNullOrWhiteSpace(key) &&
                !config.HiddenGamePaths.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                config.HiddenGamePaths.Add(key);
            }
        }
    }

    /// <summary>
    /// 迁移到 v4：为历史条目补填默认应用的详情描述。
    /// </summary>
    /// <remarks>
    /// <see cref="RefreshUnresolvedOfficePaths(LauncherConfig)"/> 顺带写 Details，
    /// 但它要求条目 TargetPath 为空才匹配 —— 已经定位成功的条目永远不会被再次处理。
    /// 因此在这之前保存的配置里，VS Code / Visual Studio 等默认应用的 Details 恒为空，
    /// 详情页只能显示"暂无详细信息"。
    /// 这里按名字回填，不依赖 TargetPath，也不覆盖用户自己填过的内容。
    /// </remarks>
    private static void BackfillDefaultDetails(LauncherConfig config)
    {
        var detailsByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var descriptor in AppLocator.CreateDefaultDescriptors())
        {
            if (!string.IsNullOrWhiteSpace(descriptor.Details))
            {
                detailsByName[descriptor.Name] = descriptor.Details;
            }
        }

        foreach (var app in config.Apps)
        {
            if (app.IsManual || !string.IsNullOrWhiteSpace(app.Details))
            {
                continue;
            }

            if (detailsByName.TryGetValue(app.Name, out var details))
            {
                app.Details = details;
            }
        }
    }

    private void EnsureSteamLibraryCard(LauncherConfig config)
    {
        if (config.Apps.Any(app => app.IsSteamLibrary) ||
            config.HiddenGamePaths.Contains(
                "steam://library",
                StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        var steamPath = SteamLibraryService.DetectSteamInstallPath();
        var steamExe = string.IsNullOrWhiteSpace(steamPath)
            ? null
            : Path.Combine(steamPath, "steam.exe");

        config.Apps.Add(new AppEntry
        {
            Id = "steam-library",
            Name = "Steam 游戏库",
            TargetPath = "steam://library",
            IconPath = File.Exists(steamExe) ? steamExe : null,
            Group = AppGroup.Game,
            Order = -1000,
            IsSteamLibrary = true
        });
    }
}
