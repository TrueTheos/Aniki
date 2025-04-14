using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Aniki
{
    // Simple class to hold the relevant token data for storage
    public class StoredTokenData
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
    }

    public static class TokenManager
    {
        private static readonly string AppName = "Aniki"; // Or your specific app name
        private static readonly string FileName = "mal_auth.token";
        // Using CurrentUser scope makes the data decryptable only by the same user on the same machine.
        private static readonly DataProtectionScope Scope = DataProtectionScope.CurrentUser;
        // Optional: Add entropy for extra security, but you MUST use the same entropy to decrypt.
        // For simplicity, we'll omit it here.
        private static readonly byte[]? Entropy = null;

        private static string GetTokenFilePath()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appFolderPath = Path.Combine(appDataPath, AppName);
            Directory.CreateDirectory(appFolderPath); // Ensure the directory exists
            return Path.Combine(appFolderPath, FileName);
        }

        public static async Task SaveTokensAsync(TokenResponse tokenResponse)
        {
            if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.access_token) || string.IsNullOrEmpty(tokenResponse.refresh_token))
            {
                // Don't save incomplete data
                ClearTokens(); // Clear any old data if the new one is invalid
                return;
            }

            try
            {
                var dataToStore = new StoredTokenData
                {
                    AccessToken = tokenResponse.access_token,
                    RefreshToken = tokenResponse.refresh_token,
                    ExpiresAtUtc = DateTime.UtcNow.AddSeconds(tokenResponse.expires_in > 30 ? tokenResponse.expires_in - 30 : tokenResponse.expires_in) // Store expiry slightly earlier
                };

                string json = JsonSerializer.Serialize(dataToStore);
                byte[] rawData = Encoding.UTF8.GetBytes(json);

                // Encrypt the data
                byte[] encryptedData = ProtectedData.Protect(rawData, Entropy, Scope);

                // Save to file
                string filePath = GetTokenFilePath();
                await File.WriteAllBytesAsync(filePath, encryptedData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving tokens: {ex.Message}");
                // Consider more robust error handling/logging
                ClearTokens(); // Clear potentially corrupted file
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

                // Decrypt data
                byte[] rawData = ProtectedData.Unprotect(encryptedData, Entropy, Scope);

                string json = Encoding.UTF8.GetString(rawData);
                var storedData = JsonSerializer.Deserialize<StoredTokenData>(json);

                // Optional: Check for expiry here, although MAL API calls will fail anyway
                // if (storedData != null && storedData.ExpiresAtUtc < DateTime.UtcNow)
                // {
                //     Console.WriteLine("Access token loaded but expired.");
                //     // Ideally, trigger refresh token flow here or return null/specific status
                //     // For now, we'll return it and let the API call handle failure/refresh later
                // }

                return storedData;
            }
            catch (CryptographicException cex)
            {
                Console.WriteLine($"Error decrypting token data (may happen if user context changed): {cex.Message}");
                ClearTokens(); // Clear invalid data
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading tokens: {ex.Message}");
                ClearTokens(); // Clear potentially corrupted file
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