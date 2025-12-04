using System.Text.Json;

namespace Aniki.Services.Save;

public class SaveEntity<T>
{
    protected string Path { get; private set; }

    private readonly JsonSerializerOptions _options = new()
    {
        IncludeFields = true,
        WriteIndented = true
    };
    
    public SaveEntity(string path)
    {
        Path = path;
    }

    public virtual void Save(string fileName, T data)
    {
        string json = JsonSerializer.Serialize(data, _options);
        string newPath = System.IO.Path.Combine(Path, fileName);
        File.WriteAllText(newPath, json);
    }

    public virtual T? Read(string fileName, T? defaultValue = default)
    {
        string newPath = System.IO.Path.Combine(Path, fileName);
        if (!File.Exists(newPath)) return defaultValue;

        try
        {
            string json = File.ReadAllText(newPath);
            return JsonSerializer.Deserialize<T>(json, _options) ?? defaultValue;
        }
        catch (JsonException)
        {
            File.Delete(newPath);
            return defaultValue;
        }
    }

    public long GetCacheSize()
    {
        if (!Directory.Exists(Path)) return 0;

        return Directory.GetFiles(Path, "*", SearchOption.AllDirectories)
            .Sum(file => new FileInfo(file).Length);
    }
}