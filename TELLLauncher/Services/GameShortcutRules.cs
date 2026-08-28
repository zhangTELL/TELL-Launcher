using System.IO;

namespace TELLLauncher.Services;

/// <summary>
/// 判定一个快捷方式是否应当进入"游戏"栏目。
/// 采用严格策略：只收录能确证为游戏的目标，避免把桌面上的浏览器、卸载程序、
/// 文档快捷方式一并收进游戏库。识别不出来的游戏可由用户手动添加。
/// </summary>
public static class GameShortcutRules
{
    /// <summary>
    /// Steam 库目录特征：Steam 游戏一律安装在 &lt;库路径&gt;\steamapps\common\&lt;游戏名&gt; 下。
    /// </summary>
    private const string SteamLibraryPathHint = "\\steamapps\\common\\";

    /// <summary>
    /// 已知游戏厂商与启动器在路径中的目录名特征，命中即认为目标位于游戏安装目录。
    /// </summary>
    private static readonly string[] KnownPublisherHints =
    {
        // 米哈游 / HoYoverse
        "Genshin Impact", "原神",
        "Honkai", "崩坏",
        "Star Rail", "星穹铁道",
        "Zenless Zone Zero", "绝区零",
        // 库洛
        "Wuthering Waves", "鸣潮",
        // 常见平台与厂商
        "steamapps",
        "Epic Games",
        "Riot Games",
        "Rockstar Games",
        "Ubisoft",
        "EA Games",
        "Origin Games",
        "Battle.net",
        "Blizzard",
        "GOG Galaxy",
        "Xbox Games"
    };

    /// <summary>
    /// 判定快捷方式是否为游戏。
    /// </summary>
    /// <param name="name">快捷方式文件名（不含扩展名），供未来按名称扩展规则。</param>
    /// <param name="targetPath">解析后的目标：可能是 steam:// 协议、exe 路径或快捷方式自身。</param>
    public static bool IsGame(string name, string? targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return false;
        }

        // 1) Steam 运行协议是最可靠的信号：steam://rungameid/&lt;appid&gt;
        if (CoverImageService.ExtractSteamAppId(targetPath) is not null)
        {
            return true;
        }

        var normalized = targetPath.Replace(
            Path.AltDirectorySeparatorChar,
            Path.DirectorySeparatorChar);

        // 2) 目标位于 Steam 库目录内
        if (normalized.Contains(SteamLibraryPathHint, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 3) 目标路径命中已知游戏厂商/启动器目录
        foreach (var hint in KnownPublisherHints)
        {
            if (normalized.Contains(hint, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
