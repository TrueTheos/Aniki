using Aniki.Models;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Aniki
{
    public static class TokenManager
    {
        private static readonly string AppName = "Aniki";
        private static readonly string FileName = "mal_auth.token";
        private static readonly DataProtectionScope Scope = DataProtectionScope.CurrentUser;
        private static readonly byte[]? Entropy = null;

        private static string GetTokenFilePath()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appFolderPath = Path.Combine(appDataPath, AppName);
            Directory.CreateDirectory(appFolderPath);
            return Path.Combine(appFolderPath, FileName);
        }

        public static async Task SaveTokensAsync(TokenResponse tokenResponse)
        {
            if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.access_token) || string.IsNullOrEmpty(tokenResponse.refresh_token))
            {
                ClearTokens(); 
                return;
            }

            try
            {
                var dataToStore = new StoredTokenData
                {
                    AccessToken = tokenResponse.access_token,
                    RefreshToken = tokenResponse.refresh_token,
                    ExpiresAtUtc = DateTime.UtcNow.AddSeconds(tokenResponse.expires_in > 30 ? tokenResponse.expires_in - 30 : tokenResponse.expires_in)
                };

                string json = JsonSerializer.Serialize(dataToStore);
                byte[] rawData = Encoding.UTF8.GetBytes(json);

                byte[] encryptedData = ProtectedData.Protect(rawData, Entropy, Scope);

                string filePath = GetTokenFilePath();
                MalUtils.Init(tokenResponse.access_token);
                await File.WriteAllBytesAsync(filePath, encryptedData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving tokens: {ex.Message}");
                ClearTokens();
            }
        }

        public static async Task<StoredTokenData?> LoadTokensAsync()
        {
            string filePath = GetTokenFilePath();
            if (!File.Exists(filePath))
            {
                return null;
            }

            try
            {
                byte[] encryptedData = await File.ReadAllBytesAsync(filePath);

                byte[] rawData = ProtectedData.Unprotect(encryptedData, Entropy, Scope);

                string json = Encoding.UTF8.GetString(rawData);
                var storedData = JsonSerializer.Deserialize<StoredTokenData>(json);

                MalUtils.Init(storedData.AccessToken);
                return storedData;
            }
            catch (CryptographicException cex)
            {
                Console.WriteLine($"Error decrypting token data (may happen if user context changed): {cex.Message}");
                ClearTokens();
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading tokens: {ex.Message}");
                ClearTokens();
                return null;
            }
        }

        public static void ClearTokens()
        {
            string filePath = GetTokenFilePath();
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing tokens: {ex.Message}");
            }
        }
    }
}