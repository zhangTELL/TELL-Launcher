using System.IO;
using System.Text.Json;
using TELLLauncher.Models;

namespace TELLLauncher.Services;

public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private const int MaxBackups = 3;

    private readonly string _configDirectory;
    private readonly string _fileName;

    public ConfigStore(string configDirectory, string fileName = "config.json")
    {
        _configDirectory = configDirectory;
        _fileName = fileName;
    }

    public string ConfigPath => Path.Combine(_configDirectory, _fileName);

    public LauncherConfig Load()
    {
        Directory.CreateDirectory(_configDirectory);

        if (!File.Exists(ConfigPath))
        {
            return Save(CreateDefault());
        }

        try
        {
            var json = File.ReadAllText(ConfigPath);
            if (string.IsNullOrWhiteSpace(json))
            {
                BackupCorruptFile();
                return Save(CreateDefault());
            }

            return JsonSerializer.Deserialize<LauncherConfig>(json, SerializerOptions)
                   ?? CreateDefault();
        }
        catch (JsonException)
        {
            BackupCorruptFile();
            return Save(CreateDefault());
        }
    }

    public LauncherConfig Save(LauncherConfig config)
    {
        Directory.CreateDirectory(_configDirectory);

        if (File.Exists(ConfigPath))
        {
            var backupPath = Path.Combine(
                _configDirectory,
                $"config.bak-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            File.Copy(ConfigPath, backupPath, overwrite: true);
            PruneOldBackups();
        }

        var json = JsonSerializer.Serialize(config, SerializerOptions);
        File.WriteAllText(ConfigPath, json);
        return config;
    }

    private void PruneOldBackups()
    {
        var backups = Directory.GetFiles(_configDirectory, "config.bak-*.json")
            .OrderByDescending(f => f)
            .ToList();

        for (var i = MaxBackups; i < backups.Count; i++)
        {
            try
            {
                File.Delete(backups[i]);
            }
            catch
            {
                // 删除失败不阻塞主流程
            }
        }
    }

    private void BackupCorruptFile()
    {
        var backupPath = Path.Combine(
            _configDirectory,
            $"config.bak-{DateTime.Now:yyyyMMdd-HHmmss}.json");

        File.Copy(ConfigPath, backupPath, overwrite: true);
        File.Delete(ConfigPath);
    }

    private static LauncherConfig CreateDefault()
    {
        return new LauncherConfig();
    }
}
