using TELLLauncher.Models;
using TELLLauncher.Services;

namespace TELLLauncher.Tests;

public class ShortcutScannerTests
{
    [Fact]
    public void Scan_ReturnsGameEntriesFromDesktopDirectories()
    {
        var desktop = CreateTempDirectory();

        try
        {
            CreateFile(desktop, @"Game One.lnk");
            CreateFile(desktop, @"Game Two.lnk");

            var entries = new ShortcutScanner(
                    new[] { desktop },
                    _ => null,
                    (_, _) => true)   // 本用例只验证扫描与排序，识别规则另有专门测试
                .Scan();

            Assert.Equal(2, entries.Count);
            Assert.Equal(new[] { "Game One", "Game Two" }, entries.Select(entry => entry.Name));
            Assert.All(entries, entry => Assert.Equal(AppGroup.Game, entry.Group));
            Assert.All(entries, entry => Assert.EndsWith(".lnk", entry.TargetPath!));
            Assert.Equal(0, entries[0].Order);
            Assert.Equal(1, entries[1].Order);
        }
        finally
        {
            Directory.Delete(desktop, recursive: true);
        }
    }

    [Fact]
    public void ScanAndMerge_DoesNotDuplicateExistingGames()
    {
        var desktop = CreateTempDirectory();
        var config = new LauncherConfig();
        config.Apps.Add(new AppEntry
        {
            Name = "Existing Game",
            TargetPath = @"C:\Games\Existing.exe",
            IconPath = @"C:\Games\Existing.exe",
            Group = AppGroup.Game,
            Order = 0
        });

        try
        {
            CreateFile(desktop, @"Existing Game.lnk");
            CreateFile(desktop, @"New Game.lnk");

            string? Resolver(string shortcutPath) =>
                shortcutPath.EndsWith("Existing Game.lnk", StringComparison.OrdinalIgnoreCase)
                    ? @"C:\Games\Existing.exe"
                    : @"C:\Games\New.exe";

            var games = new ShortcutScanner(new[] { desktop }, Resolver, (_, _) => true)
                .ScanAndMerge(config);

            Assert.Equal(2, games.Count);
            Assert.Equal(
                new[] { "Existing Game", "New Game" },
                games.Select(entry => entry.Name).OrderBy(name => name));
            Assert.Equal(2, config.Apps.Count(app => app.Group == AppGroup.Game));
        }
        finally
        {
            Directory.Delete(desktop, recursive: true);
        }
    }

    [Fact]
    public void ScanAndMerge_DoesNotRestoreHiddenGames()
    {
        var desktop = CreateTempDirectory();
        var config = new LauncherConfig();
        config.HiddenGamePaths.Add(@"C:\Hidden\HiddenGame.exe");

        try
        {
            CreateFile(desktop, @"Hidden Game.lnk");

            var games = new ShortcutScanner(
                    new[] { desktop },
                    _ => @"C:\Hidden\HiddenGame.exe",
                    (_, _) => true)   // 本用例验证隐藏列表生效，与识别规则无关
                .ScanAndMerge(config);

            Assert.Empty(games);
            Assert.Empty(config.Apps.Where(app => app.Group == AppGroup.Game));
        }
        finally
        {
            Directory.Delete(desktop, recursive: true);
        }
    }

    [Fact]
    public void Scan_HandlesResolverFailureGracefully()
    {
        var desktop = CreateTempDirectory();

        try
        {
            CreateFile(desktop, @"Broken Game.lnk");

            var entries = new ShortcutScanner(
                    new[] { desktop },
                    _ => throw new InvalidOperationException("resolver failed"),
                    (_, _) => true)
                .Scan();

            var entry = Assert.Single(entries);
            Assert.Equal("Broken Game", entry.Name);
            Assert.EndsWith("Broken Game.lnk", entry.TargetPath!);
        }
        finally
        {
            Directory.Delete(desktop, recursive: true);
        }
    }

    [Fact]
    public void Scan_ParsesUrlShortcutAndExtractsUrl()
    {
        var desktop = CreateTempDirectory();

        try
        {
            CreateUrlFile(desktop, "Counter-Strike 2.url", "steam://rungameid/730");
            CreateUrlFile(desktop, "Apex Legends.url", "steam://rungameid/1172470");

            var entries = new ShortcutScanner(new[] { desktop }).Scan();

            Assert.Equal(2, entries.Count);
            var cs2 = Assert.Single(entries, e => e.Name == "Counter-Strike 2");
            Assert.Equal("steam://rungameid/730", cs2.TargetPath);
            var apex = Assert.Single(entries, e => e.Name == "Apex Legends");
            Assert.Equal("steam://rungameid/1172470", apex.TargetPath);
            Assert.All(entries, e => Assert.Equal(AppGroup.Game, e.Group));
            Assert.All(entries, e => Assert.False(e.IsManual));
        }
        finally
        {
            Directory.Delete(desktop, recursive: true);
        }
    }

    [Fact]
    public void Scan_IncludesBothLnkAndUrlFiles()
    {
        var desktop = CreateTempDirectory();

        try
        {
            CreateFile(desktop, "Desktop Game.lnk");
            CreateUrlFile(desktop, "Steam Game.url", "steam://rungameid/730");

            var entries = new ShortcutScanner(new[] { desktop }, _ => null, (_, _) => true).Scan();

            Assert.Equal(2, entries.Count);
            Assert.Contains(entries, e => e.Name == "Desktop Game");
            Assert.Contains(entries, e => e.Name == "Steam Game");
        }
        finally
        {
            Directory.Delete(desktop, recursive: true);
        }
    }

    [Fact]
    public void Scan_HandlesCorruptUrlFileGracefully()
    {
        var desktop = CreateTempDirectory();

        try
        {
            CreateFile(desktop, "Corrupt.url");  // 空文件，没有 URL=
            CreateUrlFile(desktop, "Good.url", "steam://rungameid/730");

            var entries = new ShortcutScanner(new[] { desktop }).Scan();

            // 损坏的 .url 解析不出目标，严格识别无法确认它是游戏，因此不再收录；
            // 正常的文件不受影响
            var good = Assert.Single(entries);
            Assert.Equal("Good", good.Name);
            Assert.Equal("steam://rungameid/730", good.TargetPath);
        }
        finally
        {
            Directory.Delete(desktop, recursive: true);
        }
    }

    [Fact]
    public void ScanAndMerge_DeduplicatesByUrl()
    {
        var desktop = CreateTempDirectory();
        var config = new LauncherConfig();
        config.Apps.Add(new AppEntry
        {
            Name = "CS2",
            TargetPath = "steam://rungameid/730",
            Group = AppGroup.Game,
            Order = 0
        });

        try
        {
            CreateUrlFile(desktop, "CS2.url", "steam://rungameid/730");

            var games = new ShortcutScanner(new[] { desktop }).ScanAndMerge(config);

            // 已存在相同 URL 的游戏，不应重复添加
            Assert.Single(config.Apps.Where(a => a.Group == AppGroup.Game));
            Assert.Single(games);
        }
        finally
        {
            Directory.Delete(desktop, recursive: true);
        }
    }

    [Fact]
    public void Scan_KeepsOnlyShortcutsThatLookLikeGames()
    {
        var desktop = CreateTempDirectory();

        try
        {
            // Steam 运行协议：收录
            CreateUrlFile(desktop, "Counter-Strike 2.url", "steam://rungameid/730");
            // 目标位于 Steam 库目录：收录
            CreateUrlFile(
                desktop,
                "Hades.url",
                @"D:\SteamLibrary\steamapps\common\Hades\Hades.exe");
            // 目标位于已知厂商目录：收录
            CreateUrlFile(
                desktop,
                "Genshin.url",
                @"D:\Games\Genshin Impact\GenshinImpact.exe");
            // 普通软件与文档：不收录
            CreateUrlFile(desktop, "Browser.url", @"C:\Program Files\Browser\browser.exe");
            CreateUrlFile(desktop, "Report.url", @"C:\Users\TELL\Documents\report.pdf");

            var entries = new ShortcutScanner(new[] { desktop }).Scan();

            Assert.Equal(
                new[] { "Counter-Strike 2", "Genshin", "Hades" },
                entries.Select(entry => entry.Name).OrderBy(name => name).ToArray());
        }
        finally
        {
            Directory.Delete(desktop, recursive: true);
        }
    }

    [Fact]
    public void Scan_IgnoresShortcutsInSubdirectories()
    {
        var desktop = CreateTempDirectory();

        try
        {
            CreateUrlFile(desktop, "Top Level Game.url", "steam://rungameid/730");
            CreateUrlFile(
                desktop,
                Path.Combine("备份", "Nested Game.url"),
                "steam://rungameid/1172470");

            var entries = new ShortcutScanner(new[] { desktop }).Scan();

            // 只扫描桌面顶层，避免把子文件夹（解压包、备份目录等）里的快捷方式一并收进来
            var game = Assert.Single(entries);
            Assert.Equal("Top Level Game", game.Name);
        }
        finally
        {
            Directory.Delete(desktop, recursive: true);
        }
    }

    [Fact]
    public void Scan_UsesShortcutIconLocationForEntryIcon()
    {
        var desktop = CreateTempDirectory();
        var iconFile = Path.Combine(desktop, "hkrpg_cn.ico");
        File.WriteAllBytes(iconFile, new byte[] { 1 }); // 只要求文件存在，解码由 IconService 负责

        try
        {
            CreateFile(desktop, @"崩坏：星穹铁道.lnk");

            var entries = new ShortcutScanner(
                    new[] { desktop },
                    _ => @"D:\miHoYo Launcher\launcher.exe",
                    (_, _) => true,
                    _ => iconFile)
                .Scan();

            var entry = Assert.Single(entries);
            Assert.Equal(iconFile, entry.IconPath);
            Assert.Equal(@"D:\miHoYo Launcher\launcher.exe", entry.TargetPath);
        }
        finally
        {
            Directory.Delete(desktop, recursive: true);
        }
    }

    [Fact]
    public void Scan_KeepsTargetAsIconWhenShortcutHasNoIconLocation()
    {
        var desktop = CreateTempDirectory();

        try
        {
            CreateFile(desktop, @"Plain Game.lnk");

            var entries = new ShortcutScanner(
                    new[] { desktop },
                    _ => @"C:\Games\Game.exe",
                    (_, _) => true,
                    _ => null)
                .Scan();

            var entry = Assert.Single(entries);
            Assert.Equal(@"C:\Games\Game.exe", entry.IconPath);
        }
        finally
        {
            Directory.Delete(desktop, recursive: true);
        }
    }

    [Fact]
    public void ScanAndMerge_MigratesSharedLauncherIconToShortcutIconLocation()
    {
        var desktop = CreateTempDirectory();
        var iconFile = Path.Combine(desktop, "hkrpg_cn.ico");
        File.WriteAllBytes(iconFile, new byte[] { 1 });
        var config = new LauncherConfig();
        config.Apps.Add(new AppEntry
        {
            Name = "崩坏：星穹铁道",
            TargetPath = @"C:\Users\zhang\Desktop\崩坏：星穹铁道.lnk",
            IconPath = @"C:\Users\zhang\Desktop\崩坏：星穹铁道.lnk",
            Group = AppGroup.Game,
            Order = 0
        });

        try
        {
            var games = new ShortcutScanner(
                    new[] { desktop },
                    _ => @"D:\miHoYo Launcher\launcher.exe",
                    (_, _) => true,
                    _ => iconFile)
                .ScanAndMerge(config);

            var game = Assert.Single(games);
            Assert.Equal(iconFile, game.IconPath);
        }
        finally
        {
            Directory.Delete(desktop, recursive: true);
        }
    }

    [Fact]
    public void ScanAndMerge_DoesNotOverwriteExplicitIconPath()
    {
        var desktop = CreateTempDirectory();
        var iconFile = Path.Combine(desktop, "hkrpg_cn.ico");
        File.WriteAllBytes(iconFile, new byte[] { 1 });
        var explicitIcon = @"C:\custom\my-icon.ico";
        var config = new LauncherConfig();
        config.Apps.Add(new AppEntry
        {
            Name = "已自定义图标",
            TargetPath = @"C:\Users\zhang\Desktop\已自定义图标.lnk",
            IconPath = explicitIcon,
            Group = AppGroup.Game,
            Order = 0
        });

        try
        {
            var games = new ShortcutScanner(
                    new[] { desktop },
                    _ => @"D:\miHoYo Launcher\launcher.exe",
                    (_, _) => true,
                    _ => iconFile)
                .ScanAndMerge(config);

            var game = Assert.Single(games);
            Assert.Equal(explicitIcon, game.IconPath);
        }
        finally
        {
            Directory.Delete(desktop, recursive: true);
        }
    }

    /// <summary>
    /// 用真实 .lnk（WScript.Shell 生成）验证图标链路：IconLocation 指向一个有效 .ico、
    /// 目标 exe 不存在时，LoadIcon 仍能出图 —— 证明图标来自 IconLocation 而非目标。
    /// </summary>
    [Fact]
    public void LoadIcon_PrefersShortcutIconLocationOverMissingTarget()
    {
        var directory = CreateTempDirectory();
        var iconFile = Path.Combine(directory, "game.ico");
        var shortcutPath = Path.Combine(directory, "崩坏：星穹铁道.lnk");
        WriteIcoFile(iconFile);

        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try
            {
                var shellType = Type.GetTypeFromProgID("WScript.Shell")!;
                dynamic shell = Activator.CreateInstance(shellType)!;
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = @"Q:\No Such Directory\launcher.exe";
                shortcut.Arguments = "--game=hkrpg_cn";
                shortcut.IconLocation = $"{iconFile},0";
                shortcut.Save();

                var resolved = ShortcutScanner.ResolveShortcutIconLocation(shortcutPath);
                if (!string.Equals(resolved, iconFile, StringComparison.OrdinalIgnoreCase))
                {
                    captured = new InvalidOperationException(
                        $"IconLocation 解析结果不符：{resolved}");
                    return;
                }

                if (IconService.LoadIcon(shortcutPath) is null)
                {
                    captured = new InvalidOperationException(
                        "LoadIcon 未能从 IconLocation 提取图标");
                }
            }
            catch (Exception ex)
            {
                captured = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(captured);

        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
            // COM 服务器偶发延迟释放文件句柄，清理失败不影响结论
        }
    }

    private static void WriteIcoFile(string path)
    {
        using var stream = File.Create(path);
        System.Drawing.SystemIcons.Information.Save(stream);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"TELLLauncher.ShortcutScanner.{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void CreateFile(string root, string relativePath)
    {
        var path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Empty);
    }

    private static void CreateUrlFile(string root, string relativePath, string url)
    {
        var path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path,
            $"[InternetShortcut]\r\nURL={url}\r\n");
    }
}
