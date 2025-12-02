using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aniki.Services.Auth;
using Aniki.Services.Interfaces;

namespace Aniki.Services;

public class TokenService : ITokenService
{
    private string _tokenDirectoryPath = Path.Combine(SaveService.MAIN_DIRECTORY, "tokens");
    private readonly Dictionary<ILoginProvider.ProviderType, StoredTokenData> _cachedTokens = new();
    private readonly byte[] _entropy = Encoding.UTF8.GetBytes("Aniki-Token-Salt-2024");

    public void Init()
    {
        if (!Directory.Exists(_tokenDirectoryPath))
        {
            Directory.CreateDirectory(_tokenDirectoryPath);
        }
    }

    public async Task<StoredTokenData?> LoadTokensAsync(ILoginProvider.ProviderType providerId)
    {
        if (_cachedTokens.TryGetValue(providerId, out var cachedToken))
        {
            return cachedToken;
        }

        string tokenFilePath = Path.Combine(_tokenDirectoryPath, $"{providerId}.dat");
        if (!File.Exists(tokenFilePath))
        {
            return null;
        }

        try
        {
            byte[] encryptedData = await File.ReadAllBytesAsync(tokenFilePath);
            string decryptedJson = DecryptData(encryptedData);
            var storedTokenData = JsonSerializer.Deserialize<StoredTokenData>(decryptedJson);

            if (storedTokenData != null && DateTime.UtcNow > storedTokenData.ExpiresAtUtc)
            {
                return null;
            }

            if (storedTokenData != null)
            {
                _cachedTokens[providerId] = storedTokenData;
            }

            return storedTokenData;
        }
        catch (Exception ex)
        {
            Log.Information($"Error loading tokens for {providerId}: {ex.Message}");
            return null;
        }
    }

    public async Task SaveTokensAsync(ILoginProvider.ProviderType providerId, TokenResponse tokenResponse)
    {
        StoredTokenData tokens = new()
        {
            AccessToken = tokenResponse.access_token ?? string.Empty,
            RefreshToken = tokenResponse.refresh_token,
            ExpiresAtUtc = DateTime.UtcNow.AddSeconds(tokenResponse.expires_in)
        };

        string json = JsonSerializer.Serialize(tokens);
        byte[] encryptedData = EncryptData(json);
        
        string tokenFilePath = Path.Combine(_tokenDirectoryPath, $"{providerId}.dat");
        await File.WriteAllBytesAsync(tokenFilePath, encryptedData);
        _cachedTokens[providerId] = tokens;
    }

    public void ClearTokens(ILoginProvider.ProviderType providerId)
    {
        string tokenFilePath = Path.Combine(_tokenDirectoryPath, $"{providerId}.dat");
        if (File.Exists(tokenFilePath))
        {
            File.Delete(tokenFilePath);
        }
        _cachedTokens.Remove(providerId);
    }

    public bool HasValidToken(ILoginProvider.ProviderType providerId)
    {
        if (_cachedTokens.TryGetValue(providerId, out var token))
        {
            return !string.IsNullOrEmpty(token.AccessToken) &&
                   DateTime.UtcNow < token.ExpiresAtUtc;
        }
        return false;
    }

    public string GetAccessToken(ILoginProvider.ProviderType providerId)
    {
        if (_cachedTokens.TryGetValue(providerId, out var token))
        {
            return token.AccessToken ?? string.Empty;
        }
        return string.Empty;
    }

    private byte[] EncryptData(string data)
    {
        if (OperatingSystem.IsWindows())
        {
            byte[] dataBytes = Encoding.UTF8.GetBytes(data);
            return ProtectedData.Protect(dataBytes, _entropy, DataProtectionScope.CurrentUser);
        }
        else
        {
            return EncryptDataAes(data);
        }
    }

    private string DecryptData(byte[] encryptedData)
    {
        if (OperatingSystem.IsWindows())
        {
            byte[] decryptedBytes = ProtectedData.Unprotect(encryptedData, _entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decryptedBytes);
        }
        else
        {
            return DecryptDataAes(encryptedData);
        }
    }
    
    private byte[] EncryptDataAes(string data)
    {
        using var aes = Aes.Create();
        
        string machineKey = Environment.MachineName + Environment.UserName + "Aniki-Secret-Key";
        byte[] key = new byte[32];
        using (var sha = SHA256.Create())
        {
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(machineKey));
            Array.Copy(hash, key, 32);
        }
        
        aes.Key = key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        byte[] dataBytes = Encoding.UTF8.GetBytes(data);
        byte[] encryptedBytes = encryptor.TransformFinalBlock(dataBytes, 0, dataBytes.Length);
        
        byte[] result = new byte[aes.IV.Length + encryptedBytes.Length];
        Array.Copy(aes.IV, 0, result, 0, aes.IV.Length);
        Array.Copy(encryptedBytes, 0, result, aes.IV.Length, encryptedBytes.Length);
        
        return result;
    }

    private string DecryptDataAes(byte[] encryptedData)
    {
        using var aes = Aes.Create();
        
        string machineKey = Environment.MachineName + Environment.UserName + "Aniki-Secret-Key";
        byte[] key = new byte[32];
        using (var sha = SHA256.Create())
        {
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(machineKey));
            Array.Copy(hash, key, 32);
        }
        
        aes.Key = key;
        
        byte[] iv = new byte[16];
        byte[] cipherText = new byte[encryptedData.Length - 16];
        Array.Copy(encryptedData, 0, iv, 0, 16);
        Array.Copy(encryptedData, 16, cipherText, 0, cipherText.Length);
        
        aes.IV = iv;
        
        using var decryptor = aes.CreateDecryptor();
        byte[] decryptedBytes = decryptor.TransformFinalBlock(cipherText, 0, cipherText.Length);
        
        return Encoding.UTF8.GetString(decryptedBytes);
    }
}