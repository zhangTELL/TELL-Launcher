using System.IO;
using System.Text.Json;

namespace TELLLauncher.Services;

public sealed record SteamGridDbSettings(string ApiKey);

public static class SteamGridDbSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static string GetSettingsPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TELL Launcher",
            "steamgriddb.json");
    }

    public static string? TryLoadApiKey(string? settingsPath = null)
    {
        var envApiKey = Environment.GetEnvironmentVariable("STEAMGRIDDB_API_KEY");
        if (!string.IsNullOrWhiteSpace(envApiKey))
        {
            return envApiKey;
        }

        var path = settingsPath ?? GetSettingsPath();
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            var settings = JsonSerializer.Deserialize<SteamGridDbSettings>(
                json,
                SerializerOptions);
            return string.IsNullOrWhiteSpace(settings?.ApiKey)
                ? null
                : settings.ApiKey;
        }
        catch
        {
            return null;
        }
    }
}
