using System.Text.Json;

namespace Untouch;

internal sealed class Config
{
    public bool SchemeEnabled { get; set; } = true;

    private static string PathOnDisk =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Untouch", "config.json");

    public static Config Load()
    {
        try
        {
            var path = PathOnDisk;
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var cfg = JsonSerializer.Deserialize<Config>(json);
                if (cfg != null) return cfg;
            }
        }
        catch { /* fall through to defaults */ }
        return new Config();
    }

    public void Save()
    {
        try
        {
            var path = PathOnDisk;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(this));
        }
        catch { /* non-fatal */ }
    }
}
