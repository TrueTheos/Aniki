using System.IO;
using System.Text.Json;

namespace Aniki.Services;

public class JsonDataManager<T>
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _options;

    public JsonDataManager(string filePath, JsonSerializerOptions? options = null)
    {
        _filePath = filePath;
        _options = options ?? new JsonSerializerOptions { WriteIndented = true };
    }

    public void Save(T data)
    {
        string json = JsonSerializer.Serialize(data, _options);
        File.WriteAllText(_filePath, json);
    }

    public T? Load(T? defaultValue = default)
    {
        if (!File.Exists(_filePath)) return defaultValue;

        try
        {
            string json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<T>(json) ?? defaultValue;
        }
        catch (JsonException)
        {
            File.Delete(_filePath);
            return defaultValue;
        }
    }

    public bool Exists() => File.Exists(_filePath);

    public void Delete()
    {
        if (File.Exists(_filePath))
            File.Delete(_filePath);
    }
}