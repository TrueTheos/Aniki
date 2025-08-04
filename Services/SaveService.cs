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
        private static readonly string _cacheDirectory = Path.Combine(_mainDirectory, "ImageCache");
        private static readonly string _settingsConfigFile = Path.Combine(_mainDirectory, "config.json");
        private static readonly string _animeStatusesFile = Path.Combine(_mainDirectory, "animeStatuses.json");
        private static readonly string _seasonCacheFile = Path.Combine(_mainDirectory, "season_cache.json");
        
        public static readonly string DefaultEpisodesFolder = Path.Combine(_mainDirectory, "Episodes");
        public static List<AnimeStatus> AnimeStatuses { get; private set; } = new();
        
        public static void Init()
        {
            if (!Directory.Exists(_cacheDirectory))
            {
                Directory.CreateDirectory(_cacheDirectory);
            }

            if (!Directory.Exists(DefaultEpisodesFolder))
            {
                Directory.CreateDirectory(DefaultEpisodesFolder);
            }

            LoadAnimeStatuses();
        }

        public static SeasonCache GetSeasonCache()
        {
            if (File.Exists(_seasonCacheFile))
            {
                try
                {
                    var json = File.ReadAllText(_seasonCacheFile);
                    return JsonSerializer.Deserialize<SeasonCache>(json) ?? new SeasonCache();
                }
                catch (JsonException)
                {
                    // Cache file is malformed, delete it and start fresh
                    File.Delete(_seasonCacheFile);
                }
            }
            return new SeasonCache();
        }

        public static void SaveSeasonCache(SeasonCache cache)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(cache, options);
            File.WriteAllText(_seasonCacheFile, json);
        }

        public static void SaveImageToCache(string fileName, Bitmap image)
        {
            string filePath = Path.Combine(_cacheDirectory, fileName);
            using (FileStream stream = new FileStream(filePath, FileMode.Create))
            {
                image.Save(stream);
            }
        }

        public static async Task SyncAnimeWithMal()
        {
            try
            {
                List<AnimeData>? animeList = await MalUtils.LoadAnimeList();

                if (animeList != null)
                {
                    AnimeStatuses.Clear();

                    foreach (AnimeData anime in animeList)
                    {
                        if (anime.Node != null)
                        {
                            AnimeStatuses.Add(new()
                            {
                                Title = anime.Node.Title,
                                WatchedEpisodes = anime.ListStatus.NumEpisodesWatched,
                                Status = anime.ListStatus.Status
                            });
                        }
                    }

                    SaveWatchingAnime();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error propagating watching anime from MAL: {ex.Message}");
            }
        }

        public static void LoadAnimeStatuses()
        {
            if (File.Exists(_animeStatusesFile))
            {
                string json = File.ReadAllText(_animeStatusesFile);
                AnimeStatuses = JsonSerializer.Deserialize<List<AnimeStatus>>(json) ?? new List<AnimeStatus>();
                return;
            }

            AnimeStatuses = new();
        }

        public static void SaveWatchingAnime()
        {
            string json = JsonSerializer.Serialize(AnimeStatuses, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_animeStatusesFile, json);
        }

        public static void ChangeWatchingAnimeStatus(string animeName, AnimeStatusApi status)
        {
            AnimeStatus? existingAnime = AnimeStatuses.FirstOrDefault(a => a.Title.Equals(animeName, StringComparison.OrdinalIgnoreCase));
            if (existingAnime != null)
            {
                existingAnime.Status = status;
            }
            SaveWatchingAnime();
        }

        public static void ChangeWatchingAnimeEpisode(string animeName, int watchedEpisodes)
        {
            AnimeStatus? existingAnime = AnimeStatuses.FirstOrDefault(a => a.Title.Equals(animeName, StringComparison.OrdinalIgnoreCase));
            if (existingAnime != null)
            {
                existingAnime.WatchedEpisodes = watchedEpisodes;
            }
            SaveWatchingAnime();
        }


        public static async Task<Bitmap> GetAnimeImage(AnimeDetails anime)
        {
            string cacheFilePath = Path.Combine(_cacheDirectory, $"anime_{anime.Id}.jpg");

            if (File.Exists(cacheFilePath))
            {
                try
                {
                    return new(cacheFilePath);
                }
                catch (Exception)
                {
                    File.Delete(cacheFilePath);
                }
            }

            Bitmap animePicture = await MalUtils.GetAnimeImage(anime.MainPicture);

            if (animePicture != null)
            {
                SaveImageToCache($"anime_{anime.Id}.jpg", animePicture);
                return animePicture;
            }

            return null;
        }

        public static async Task<Bitmap> GetUserProfileImage(int userId)
        {
            string cacheFilePath = Path.Combine(_cacheDirectory, $"profile_{userId}.jpg");

            if (File.Exists(cacheFilePath))
            {
                try
                {
                    return new(cacheFilePath);
                }
                catch (Exception)
                {
                    File.Delete(cacheFilePath);
                }
            }

            Bitmap profileImage = await MalUtils.GetUserPicture();

            if (profileImage != null)
            {
                SaveImageToCache($"profile_{userId}.jpg", profileImage);
                return profileImage;
            }

            return GetDefaultProfileImage();
        }

        private static Bitmap GetDefaultProfileImage()
        {
            try
            {
                string defaultImagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "default_profile.png");
                if (File.Exists(defaultImagePath))
                {
                    return new(defaultImagePath);
                }
            }
            catch (Exception)
            {
            }

            return null;
        }

        public static void SaveSettings(SettingsConfig config)
        {
            File.WriteAllText(_settingsConfigFile, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
            LoadAnimeStatuses();
        }

        public static SettingsConfig GetSettingsConfig()
        {
            if (File.Exists(_settingsConfigFile))
            {
                string json = File.ReadAllText(_settingsConfigFile);
                SettingsConfig? config = JsonSerializer.Deserialize<SettingsConfig>(json);
                return config;
            }

            return null;
        }

        public class AnimeStatus
        {
            public string Title { get; set; }
            public int WatchedEpisodes { get; set; }
            [JsonConverter(typeof(JsonStringEnumConverter))]
            public AnimeStatusApi Status { get; set; }
        }
    }
}