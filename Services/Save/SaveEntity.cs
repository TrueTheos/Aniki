using System.Text.Json;

namespace Aniki.Services.Save;

internal class SaveEntity<T>
{
    protected string Path { get; }

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
}