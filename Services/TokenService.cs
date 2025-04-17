using Aniki.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Aniki.Services
{
    public static class TokenService
    {
        private static string _tokenFilePath;
        private static StoredTokenData _cachedTokens;

        public static void Init()
        {
            string appDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Aniki");

            if (!Directory.Exists(appDataFolder))
            {
                Directory.CreateDirectory(appDataFolder);
            }

            _tokenFilePath = Path.Combine(appDataFolder, "tokens.json");
        }

        public static async Task<StoredTokenData> LoadTokensAsync()
        {
            if (_cachedTokens != null)
            {
                MalUtils.Init(_cachedTokens.AccessToken);
                return _cachedTokens;
            }

            if (!File.Exists(_tokenFilePath))
            {
                return null;
            }

            try
            {
                string encryptedJson = await File.ReadAllTextAsync(_tokenFilePath);
                string decryptedJson = DecryptData(encryptedJson);
                _cachedTokens = JsonSerializer.Deserialize<StoredTokenData>(decryptedJson);

                if (_cachedTokens != null && DateTime.UtcNow > _cachedTokens.ExpiresAtUtc)
                {
                    return null;
                }

                MalUtils.Init(_cachedTokens.AccessToken);
                return _cachedTokens;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading tokens: {ex.Message}");
                return null;
            }
        }

        public static async Task SaveTokensAsync(TokenResponse tokenResponse)
        {
            StoredTokenData tokens = new StoredTokenData
            {
                AccessToken = tokenResponse.access_token,
                RefreshToken = tokenResponse.refresh_token,
                ExpiresAtUtc = DateTime.UtcNow.AddSeconds(tokenResponse.expires_in)
            };

            string json = JsonSerializer.Serialize(tokens);
            string encryptedJson = EncryptData(json);
            await File.WriteAllTextAsync(_tokenFilePath, encryptedJson);
            _cachedTokens = tokens;
        }

        public static void ClearTokens()
        {
            if (File.Exists(_tokenFilePath))
            {
                File.Delete(_tokenFilePath);
            }
            _cachedTokens = null;
        }

        public static bool HasValidToken()
        {
            return _cachedTokens != null &&
                   !string.IsNullOrEmpty(_cachedTokens.AccessToken) &&
                   DateTime.UtcNow < _cachedTokens.ExpiresAtUtc;
        }

        public static string GetAccessToken()
        {
            return _cachedTokens?.AccessToken;
        }

        private static string EncryptData(string data)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(data);
            return Convert.ToBase64String(bytes);
        }

        private static string DecryptData(string encryptedData)
        {
            byte[] bytes = Convert.FromBase64String(encryptedData);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
