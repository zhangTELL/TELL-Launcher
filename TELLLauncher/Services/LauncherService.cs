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
            }
        }
    }
}
