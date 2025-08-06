using Aniki.Misc;
using Aniki.Models;
using Aniki.ViewModels;
using Avalonia.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Aniki.Services
{
    using SeasonCache = Dictionary<string, Dictionary<int, SeasonData>>;
    
    public struct SeasonData
    {
        public int Episodes { get; set; }
        public int MalId { get; set; }
    }
    
    public class SaveService
    {
        private static readonly string _mainDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Aniki");
        
        public static readonly string DefaultEpisodesFolder = Path.Combine(_mainDirectory, "Episodes");

        // Managers
        public static CacheManager ImageCache { get; private set; }
        public static AnimeStatusManager AnimeStatusManager { get; private set; }
        public static JsonDataManager<SeasonCache> SeasonCacheManager { get; private set; }
        public static JsonDataManager<SettingsConfig> SettingsManager { get; private set; }

        public static List<AnimeStatus> AnimeStatuses => AnimeStatusManager.AnimeStatuses;
        
        public static void Init()
        {
            EnsureDirectoriesExist();
            InitializeManagers();
        }

        private static void EnsureDirectoriesExist()
        {
            if (!Directory.Exists(_mainDirectory))
                Directory.CreateDirectory(_mainDirectory);
                
            if (!Directory.Exists(DefaultEpisodesFolder))
                Directory.CreateDirectory(DefaultEpisodesFolder);
        }

        private static void InitializeManagers()
        {
            ImageCache = new CacheManager(Path.Combine(_mainDirectory, "ImageCache"));
            AnimeStatusManager = new AnimeStatusManager(Path.Combine(_mainDirectory, "animeStatuses.json"));
            SeasonCacheManager = new JsonDataManager<SeasonCache>(Path.Combine(_mainDirectory, "season_cache.json"));
            SettingsManager = new JsonDataManager<SettingsConfig>(Path.Combine(_mainDirectory, "config.json"));
        }

        public static SeasonCache GetSeasonCache() => SeasonCacheManager.Load(new SeasonCache());
        public static void SaveSeasonCache(SeasonCache cache) => SeasonCacheManager.Save(cache);
        public static void SaveImageToCache(string fileName, Bitmap image) => ImageCache.SaveImage(fileName, image);
        public static async Task SyncAnimeWithMal() => await AnimeStatusManager.SyncWithMal();
        public static void LoadAnimeStatuses() => AnimeStatusManager.Load();
        public static void SaveWatchingAnime() => AnimeStatusManager.Save();
        public static void ChangeWatchingAnimeStatus(string animeName, AnimeStatusApi status) => 
            AnimeStatusManager.ChangeStatus(animeName, status);
        public static void ChangeWatchingAnimeEpisode(string animeName, int watchedEpisodes) => 
            AnimeStatusManager.ChangeEpisodeCount(animeName, watchedEpisodes);
        public static void SaveSettings(SettingsConfig config) => SettingsManager.Save(config);
        public static SettingsConfig? GetSettingsConfig() => SettingsManager.Load();

        public static async Task<Bitmap?> GetAnimeImage(AnimeDetails anime)
        {
            string fileName = $"anime_{anime.Id}.jpg";
            
            Bitmap? cachedImage = ImageCache.LoadImage(fileName);
            if (cachedImage != null) return cachedImage;

            Bitmap? downloadedImage = await MalUtils.GetAnimeImage(anime.MainPicture);
            if (downloadedImage != null)
            {
                ImageCache.SaveImage(fileName, downloadedImage);
                return downloadedImage;
            }

            return null;
        }

        public static async Task<Bitmap?> GetUserProfileImage(int userId)
        {
            string fileName = $"profile_{userId}.jpg";
            
            Bitmap? cachedImage = ImageCache.LoadImage(fileName);
            if (cachedImage != null) return cachedImage;

            Bitmap? downloadedImage = await MalUtils.GetUserPicture();
            if (downloadedImage != null)
            {
                ImageCache.SaveImage(fileName, downloadedImage);
                return downloadedImage;
            }

            return GetDefaultProfileImage();
        }

        private static Bitmap? GetDefaultProfileImage()
        {
            try
            {
                string defaultImagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "default_profile.png");
                if (File.Exists(defaultImagePath))
                {
                    return new Bitmap(defaultImagePath);
                }
            }
            catch (Exception) { }

            return null;
        }

        public static void ClearAllCaches()
        {
            ImageCache.ClearCache();
            SeasonCacheManager.Delete();
        }

        public static long GetTotalCacheSize()
        {
            return ImageCache.GetCacheSize();
        }

        public static void CleanOldCache(TimeSpan maxAge)
        {
            ImageCache.CleanOldCache(maxAge);
        }

        public static Dictionary<string, object> GetCacheStats()
        {
            return new Dictionary<string, object>
            {
                ["ImageCacheSize"] = ImageCache.GetCacheSize(),
                ["ImageCacheFileCount"] = ImageCache.GetCacheFileCount(),
                ["SeasonCacheExists"] = SeasonCacheManager.Exists(),
                ["SettingsExists"] = SettingsManager.Exists(),
                ["AnimeStatusCount"] = AnimeStatuses.Count
            };
        }
    }

    public class AnimeStatus
    {
        public string Title { get; set; } = string.Empty;
        public int WatchedEpisodes { get; set; }
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public AnimeStatusApi Status { get; set; }
    }
}