using Avalonia.Media.Imaging;

namespace Aniki.Services;

public class CacheManager
    {
        private readonly string _cacheDirectory;

        public CacheManager(string cacheDirectory)
        {
            _cacheDirectory = cacheDirectory;
            EnsureDirectoryExists();
        }

        private void EnsureDirectoryExists()
        {
            if (!Directory.Exists(_cacheDirectory))
            {
                Directory.CreateDirectory(_cacheDirectory);
            }
        }

        public void SaveImage(string fileName, Bitmap image)
        {
            string filePath = Path.Combine(_cacheDirectory, fileName);
            using (FileStream stream = new(filePath, FileMode.Create))
            {
                image.Save(stream);
            }
        }

        public Bitmap? LoadImage(string fileName)
        {
            string filePath = Path.Combine(_cacheDirectory, fileName);
            if (!File.Exists(filePath)) return null;

            try
            {
                return new Bitmap(filePath);
            }
            catch (Exception)
            {
                File.Delete(filePath);
                return null;
            }
        }

        public void ClearCache()
        {
            if (Directory.Exists(_cacheDirectory))
            {
                Directory.Delete(_cacheDirectory, true);
                Directory.CreateDirectory(_cacheDirectory);
            }
        }

        public long GetCacheSize()
        {
            if (!Directory.Exists(_cacheDirectory)) return 0;

            return Directory.GetFiles(_cacheDirectory, "*", SearchOption.AllDirectories)
                           .Sum(file => new FileInfo(file).Length);
        }

        public int GetCacheFileCount()
        {
            if (!Directory.Exists(_cacheDirectory)) return 0;
            return Directory.GetFiles(_cacheDirectory, "*", SearchOption.AllDirectories).Length;
        }

        public void CleanOldCache(TimeSpan maxAge)
        {
            if (!Directory.Exists(_cacheDirectory)) return;

            var cutoffTime = DateTime.Now - maxAge;
            var filesToDelete = Directory.GetFiles(_cacheDirectory, "*", SearchOption.AllDirectories)
                                       .Where(file => File.GetLastAccessTime(file) < cutoffTime);

            foreach (var file in filesToDelete)
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception) { }
            }
        }
    }