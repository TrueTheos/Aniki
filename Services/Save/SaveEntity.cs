using System.Text.Json;

namespace Aniki.Services;

public class SaveEntity<T>
{
    protected string _path { get; private set; }

    private readonly JsonSerializerOptions _options = new()
    {
        IncludeFields = true,
        WriteIndented = true
    };
    
    public SaveEntity(string path)
    {
        _path = path;
    }

    public virtual void Save(string fileName, T data)
    {
        string json = JsonSerializer.Serialize(data, _options);
        string newPath = Path.Combine(_path, fileName);
        File.WriteAllText(newPath, json);
    }

    public virtual T? Read(string fileName, T? defaultValue = default)
    {
        string newPath = Path.Combine(_path, fileName);
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
        if (!Directory.Exists(_path)) return 0;

        return Directory.GetFiles(_path, "*", SearchOption.AllDirectories)
            .Sum(file => new FileInfo(file).Length);
    }
}