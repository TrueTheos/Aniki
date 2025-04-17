using Aniki.Models;
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
    public class ImageCache
    {
        private static readonly string CacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Aniki", "ImageCache");

        static ImageCache()
        {
            if (!Directory.Exists(CacheDirectory))
            {
                Directory.CreateDirectory(CacheDirectory);
            }
        }

        public static void SaveToCache(string fileName, Bitmap image)
        {
            string filePath = Path.Combine(CacheDirectory, fileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                image.Save(stream);
            }
        }

        public static async Task<Bitmap> GetAnimeImage(AnimeDetails anime)
        {
            string cacheFilePath = Path.Combine(CacheDirectory, $"anime_{anime.Id}.jpg");

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

            Bitmap animePicture = await MalUtils.GetAnimeImage(anime.Main_Picture);

            if (animePicture != null)
            {
                SaveToCache($"anime_{anime.Id}.jpg", animePicture);
                return animePicture;
            }

            return null;
        }

        public static async Task<Bitmap> GetUserProfileImage(int userId)
        {
            string cacheFilePath = Path.Combine(CacheDirectory, $"profile_{userId}.jpg");

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
                SaveToCache($"profile_{userId}.jpg", profileImage);
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
    }
}
