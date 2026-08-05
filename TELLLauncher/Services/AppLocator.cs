using System.IO;
using TELLLauncher.Models;

namespace TELLLauncher.Services;

public sealed class AppDescriptor
{
    public AppDescriptor(string name, AppGroup group, string details = "", params string[] executableNames)
    {
        Name = name;
        Group = group;
        Details = details;
        ExecutableNames = executableNames;
    }

    public string Name { get; }

    public AppGroup Group { get; }

    public string Details { get; }

    public IReadOnlyList<string> ExecutableNames { get; }
}

public sealed class AppLocator
{
    private readonly IReadOnlyList<string> _searchRoots;

    public AppLocator(IEnumerable<string>? searchRoots = null)
    {
        _searchRoots = searchRoots?.ToList() ?? CreateDefaultSearchRoots();
    }

    public IReadOnlyList<AppEntry> CreateDefaultOfficeEntries()
    {
        var descriptors = CreateDefaultDescriptors();
        var entries = new List<AppEntry>(descriptors.Count);
        var order = 0;

        foreach (var descriptor in descriptors)
        {
            var targetPath = Find(descriptor);
            entries.Add(new AppEntry
            {
                Id = $"default-{CreateStableId(descriptor.Name)}",
                Name = descriptor.Name,
                TargetPath = targetPath,
                IconPath = targetPath,
                Details = descriptor.Details,
                Group = descriptor.Group,
                Order = order++,
                IsManual = false
            });
        }

        return entries;
    }

    public async Task<IReadOnlyList<AppEntry>> CreateDefaultOfficeEntriesAsync()
    {
        var descriptors = CreateDefaultDescriptors();
        return await Task.Run(() =>
        {
            var entries = new List<AppEntry>(descriptors.Count);
            var order = 0;

            foreach (var descriptor in descriptors)
            {
                var targetPath = Find(descriptor);
                entries.Add(new AppEntry
                {
                    Id = $"default-{CreateStableId(descriptor.Name)}",
                    Name = descriptor.Name,
                    TargetPath = targetPath,
                    IconPath = targetPath,
                    Details = descriptor.Details,
                    Group = descriptor.Group,
                    Order = order++,
                    IsManual = false
                });
            }

            return (IReadOnlyList<AppEntry>)entries;
        });
    }

    public string? Find(AppDescriptor descriptor)
    {
        foreach (var root in _searchRoots)
        {
            foreach (var shortcut in SafeEnumerateFiles(root, "*.lnk"))
            {
                if (PathMatches(shortcut, descriptor))
                {
                    return shortcut;
                }
            }
        }

        foreach (var root in _searchRoots)
        {
            foreach (var executableName in descriptor.ExecutableNames)
            {
                foreach (var file in SafeEnumerateFiles(root, executableName))
                {
                    if (PathMatches(file, descriptor))
                    {
                        return file;
                    }
                }
            }
        }

        return null;
    }

    public static IReadOnlyList<AppDescriptor> CreateDefaultDescriptors()
    {
        return new List<AppDescriptor>
        {
            new("Visual Studio", AppGroup.Ide,
                "Microsoft 推出的重量级集成开发环境，支持 C#、C++、.NET 等多种语言和平台的全功能 IDE。",
                "devenv.exe"),
            new("VS Code", AppGroup.Ide,
                "微软出品的轻量级代码编辑器，拥有丰富的插件生态，支持几乎所有主流编程语言。",
                "Code.exe", "code.exe"),
            new("PyCharm", AppGroup.Ide,
                "JetBrains 推出的 Python 专用 IDE，提供智能代码补全、调试、测试和科学计算支持。",
                "pycharm64.exe", "pycharm.exe", "PyCharm.exe"),
            new("IntelliJ IDEA", AppGroup.Ide,
                "JetBrains 旗舰 Java/Kotlin IDE，以智能代码分析和强大的重构能力著称。",
                "idea64.exe", "idea.exe", "idea64.exe"),
            new("Trae", AppGroup.AiTool,
                "字节跳动推出的 AI 编程助手，集成智能代码生成和对话式编程能力。",
                "Trae.exe", "trae.exe"),
            new("WorkBuddy", AppGroup.AiTool,
                "新一代 AI 智能助手桌面客户端，支持多模型对话、文件管理和自动化工作流。",
                "WorkBuddy.exe", "workbuddy.exe"),
            new("ChatGPT", AppGroup.AiTool,
                "OpenAI 推出的 AI 对话客户端，基于 GPT 系列大语言模型，支持文本、代码和图像生成。",
                "ChatGPT.exe", "chatgpt.exe"),
            new("Claude", AppGroup.AiTool,
                "Anthropic 推出的 AI 助手桌面客户端，以长上下文和安全性著称。",
                "Claude.exe", "claude.exe"),
            new("CC Switch", AppGroup.AiTool,
                "AI 编程助手配置切换工具，用于在不同 AI 服务商之间快速切换。",
                "CCSwitch.exe", "ccswitch.exe", "CC Switch.exe"),
            new("Marvis", AppGroup.AiTool,
                "AI 驱动的浏览器自动化助手，支持智能网页交互和数据抓取。",
                "Marvis.exe", "marvis.exe")
        };
    }

    private static IReadOnlyList<string> CreateDefaultSearchRoots()
    {
        var roots = new List<string>();

        AddIfExists(roots, Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            "Programs"));
        AddIfExists(roots, Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            "Programs"));
        AddIfExists(roots, Environment.GetFolderPath(
            Environment.SpecialFolder.DesktopDirectory));
        AddIfExists(roots, Environment.GetFolderPath(
            Environment.SpecialFolder.CommonDesktopDirectory));
        AddIfExists(roots, Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs"));

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(
            Environment.SpecialFolder.ProgramFilesX86);

        AddIfExists(roots, Path.Combine(programFiles, "JetBrains"));
        AddIfExists(roots, Path.Combine(programFilesX86, "JetBrains"));
        AddIfExists(roots, Path.Combine(programFiles, "Microsoft Visual Studio"));
        AddIfExists(roots, Path.Combine(programFilesX86, "Microsoft Visual Studio"));

        return roots;
    }

    private static void AddIfExists(ICollection<string> roots, string path)
    {
        if (Directory.Exists(path))
        {
            roots.Add(path);
        }
    }

    private static bool PathMatches(string path, AppDescriptor descriptor)
    {
        var fileName = Path.GetFileName(path);
        if (descriptor.ExecutableNames.Any(
                name => fileName.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return path.Contains(descriptor.Name, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> SafeEnumerateFiles(string root, string pattern)
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
                files = Directory.EnumerateFiles(directory, pattern);
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

    private static string CreateStableId(string name)
    {
        return new string(name.Where(char.IsLetterOrDigit).ToArray());
    }
}
