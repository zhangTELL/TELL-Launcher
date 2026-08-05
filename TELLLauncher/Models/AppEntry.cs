namespace TELLLauncher.Models;

public sealed class AppEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    public string? TargetPath { get; set; }

    public string? IconPath { get; set; }

    public string? DetailImagePath { get; set; }

    public string? Details { get; set; }

    public AppGroup Group { get; set; }

    public int Order { get; set; }

    public bool IsHidden { get; set; }

    public bool IsManual { get; set; }

    public bool IsSteamLibrary { get; set; }

    public DateTime? LastLaunchedAt { get; set; }
}
