using System.IO;
using TELLLauncher.Models;

namespace TELLLauncher.Services;

public sealed class AppDescriptor
{
    public AppDescriptor(string name, AppGroup group, params string[] executableNames)
    {
        Name = name;
        Group = group;
        ExecutableNames = executableNames;
    }

    public string Name { get; }

    public AppGroup Group { get; }

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
                Group = descriptor.Group,
                Order = order++,
                IsManual = false
            });
        }

        return entries;
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
            new("Visual Studio", AppGroup.Ide, "devenv.exe"),
            new("VS Code", AppGroup.Ide, "Code.exe", "code.exe"),
            new("PyCharm", AppGroup.Ide, "pycharm64.exe", "pycharm.exe", "PyCharm.exe"),
            new("IntelliJ IDEA", AppGroup.Ide, "idea64.exe", "idea.exe", "idea64.exe"),
            new("Trae", AppGroup.AiTool, "Trae.exe", "trae.exe"),
            new("WorkBuddy", AppGroup.AiTool, "WorkBuddy.exe", "workbuddy.exe"),
            new("ChatGPT", AppGroup.AiTool, "ChatGPT.exe", "chatgpt.exe"),
            new("Claude", AppGroup.AiTool, "Claude.exe", "claude.exe"),
            new("CC Switch", AppGroup.AiTool, "CCSwitch.exe", "ccswitch.exe", "CC Switch.exe"),
            new("Marvis", AppGroup.AiTool, "Marvis.exe", "marvis.exe")
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
