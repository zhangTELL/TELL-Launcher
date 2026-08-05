namespace TELLLauncher.Models;

public sealed class LauncherConfig
{
    public int Version { get; set; } = 1;

    public bool DefaultsInitialized { get; set; }

    public List<AppEntry> Apps { get; set; } = new();

    public List<string> HiddenGamePaths { get; set; } = new();
}
