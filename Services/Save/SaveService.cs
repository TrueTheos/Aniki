using Avalonia.Media.Imaging;
using System.Text.Json.Serialization;
using Aniki.Services.Interfaces;

namespace Aniki.Services;

    
public struct SeasonData
{
    public int Episodes { get; set; }
    public int MalId { get; set; }
}

public class SaveService : ISaveService
{
    public static readonly string _mainDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Aniki");
    
    public string DefaultEpisodesFolder => Path.Combine(_mainDirectory, "Episodes");
    
    public CacheManager? ImageCache { get; private set; }
    public JsonDataManager<SeasonCache>? SeasonCacheManager { get; private set; }
    public JsonDataManager<SettingsConfig>? SettingsManager { get; private set; }

    public SaveService()
    {
        EnsureDirectoriesExist();
        InitializeManagers();
    }

    private void EnsureDirectoriesExist()
    {
        if (!Directory.Exists(_mainDirectory))
            Directory.CreateDirectory(_mainDirectory);
            
        if (!Directory.Exists(DefaultEpisodesFolder))
            Directory.CreateDirectory(DefaultEpisodesFolder);
    }

    private void InitializeManagers()
    {
        ImageCache = new CacheManager(Path.Combine(_mainDirectory, "ImageCache"));
        SeasonCacheManager = new JsonDataManager<SeasonCache>(Path.Combine(_mainDirectory, "season_cache.json"));
        SettingsManager = new JsonDataManager<SettingsConfig>(Path.Combine(_mainDirectory, "config.json"));
    }

    public void Init()
    {
        throw new NotImplementedException();
    }

    public SeasonCache GetSeasonCache() => SeasonCacheManager!.Load(new SeasonCache())!;
    public void SaveSeasonCache(SeasonCache cache) => SeasonCacheManager!.Save(cache);
    public void SaveImageToCache(string fileName, Bitmap image) => ImageCache!.SaveImage(fileName, image);
    public void SaveSettings(SettingsConfig config) => SettingsManager!.Save(config);
    public SettingsConfig? GetSettingsConfig() => SettingsManager!.Load();

    public bool TryGetAnimeImage(int id, out Bitmap? picture)
    {
        string fileName = $"anime_{id}.jpg";
    
        picture = ImageCache!.LoadImage(fileName);
    
        return picture != null;
    }

    public void SaveImage(int id, Bitmap image)
    {
        string fileName = $"anime_{id}.jpg";
        ImageCache?.SaveImage(fileName, image);
    }
}

public class AnimeStatus
{
    public string Title { get; set; } = string.Empty;
    public int WatchedEpisodes { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AnimeStatusApi Status { get; set; }
}