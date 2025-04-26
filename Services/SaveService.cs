using Aniki.Models;
using Aniki.ViewModels;
using Avalonia.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Aniki.Services
{
    public class SaveService
    {
        private static readonly string _mainDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Aniki");

        private static readonly string _cacheDirectory = Path.Combine(_mainDirectory, "ImageCache");

        public static readonly string DefaultEpisodesFolder = Path.Combine(_mainDirectory, "Episodes");

        private static readonly string _settingsConfigFile = Path.Combine(_mainDirectory, "config.json");

        static SaveService()
        {
            if (!Directory.Exists(_cacheDirectory))
            {
                Directory.CreateDirectory(_cacheDirectory);
            }

            if (!Directory.Exists(DefaultEpisodesFolder))
            {
                Directory.CreateDirectory(DefaultEpisodesFolder);
            }
        }

        public static void SaveImageToCache(string fileName, Bitmap image)
        {
            string filePath = Path.Combine(_cacheDirectory, fileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                image.Save(stream);
            }
        }

        public static async Task<Bitmap> GetAnimeImage(AnimeDetails anime)
        {
            string cacheFilePath = Path.Combine(_cacheDirectory, $"anime_{anime.Id}.jpg");

            if (File.Exists(cacheFilePath))
            {
                try
                {
                    return new Bitmap(cacheFilePath);
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
                    return new Bitmap(cacheFilePath);
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
                    return new Bitmap(defaultImagePath);
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
        }

        public static SettingsConfig GetSettingsConfig()
        {
            if (File.Exists(_settingsConfigFile))
            {
                var json = File.ReadAllText(_settingsConfigFile);
                var config = JsonSerializer.Deserialize<SettingsConfig>(json);
                return config;
            }

            return null;
        }
    }
}
