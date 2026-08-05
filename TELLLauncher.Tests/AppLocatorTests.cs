using TELLLauncher.Models;
using TELLLauncher.Services;

namespace TELLLauncher.Tests;

public class AppLocatorTests
{
    [Fact]
    public void CreateDefaultOfficeEntries_ReturnsExpectedNamesAndGroups()
    {
        var locator = new AppLocator(Array.Empty<string>());

        var entries = locator.CreateDefaultOfficeEntries();

        Assert.Equal(10, entries.Count);
        Assert.Equal(
            new[]
            {
                "Visual Studio",
                "VS Code",
                "PyCharm",
                "IntelliJ IDEA",
                "Trae",
                "WorkBuddy",
                "ChatGPT",
                "Claude",
                "CC Switch",
                "Marvis"
            },
            entries.Select(entry => entry.Name));

        Assert.Equal(4, entries.Count(entry => entry.Group == AppGroup.Ide));
        Assert.Equal(6, entries.Count(entry => entry.Group == AppGroup.AiTool));
        Assert.All(entries, entry => Assert.Null(entry.TargetPath));
    }

    [Fact]
    public void CreateDefaultOfficeEntries_LocatesKnownExecutablesInSearchRoots()
    {
        var root = CreateTempDirectory();

        try
        {
            CreateFile(root, @"Microsoft Visual Studio\Common7\IDE\devenv.exe");
            CreateFile(root, @"Microsoft VS Code\Code.exe");
            CreateFile(root, @"JetBrains\PyCharm 2024.1\bin\pycharm64.exe");
            CreateFile(root, @"JetBrains\IntelliJ IDEA 2024.1\bin\idea64.exe");
            CreateFile(root, @"Trae\Trae.exe");
            CreateFile(root, @"WorkBuddy\WorkBuddy.exe");
            CreateFile(root, @"ChatGPT\ChatGPT.exe");
            CreateFile(root, @"Claude\claude.exe");
            CreateFile(root, @"CC Switch\CCSwitch.exe");
            CreateFile(root, @"Marvis\Marvis.exe");

            var entries = new AppLocator(new[] { root }).CreateDefaultOfficeEntries();

            Assert.All(entries, entry => Assert.NotNull(entry.TargetPath));
            Assert.All(entries, entry => Assert.True(File.Exists(entry.TargetPath)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Find_DoesNotMatchUnrelatedExecutable()
    {
        var root = CreateTempDirectory();

        try
        {
            CreateFile(root, @"OtherTools\NotepadLike.exe");

            var locator = new AppLocator(new[] { root });
            var descriptor = AppLocator.CreateDefaultDescriptors()
                .Single(item => item.Name == "VS Code");

            Assert.Null(locator.Find(descriptor));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"TELLLauncher.AppLocator.{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void CreateFile(string root, string relativePath)
    {
        var path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Empty);
    }
}
