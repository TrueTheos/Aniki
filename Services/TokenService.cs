﻿using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aniki.Services.Interfaces;

namespace Aniki.Services;

public class TokenService : ITokenService
{
    private string _tokenFilePath = "";
    private StoredTokenData? _cachedTokens;
    private readonly byte[] _entropy = Encoding.UTF8.GetBytes("Aniki-Token-Salt-2024");

    private readonly IMalService _malService;
    
    public TokenService(IMalService malService)
    {
        _malService = malService;
    }

    public void Init()
    {
        string appDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Aniki");

        if (!Directory.Exists(appDataFolder))
        {
            Directory.CreateDirectory(appDataFolder);
        }

        _tokenFilePath = Path.Combine(appDataFolder, "tokens.dat");
    }

    public async Task<StoredTokenData?> LoadTokensAsync()
    {
        if (_cachedTokens != null)
        {
            _malService.Init(_cachedTokens.AccessToken);
            return _cachedTokens;
        }

        if (!File.Exists(_tokenFilePath))
        {
            return null;
        }

        try
        {
            byte[] encryptedData = await File.ReadAllBytesAsync(_tokenFilePath);
            string decryptedJson = DecryptData(encryptedData);
            _cachedTokens = JsonSerializer.Deserialize<StoredTokenData>(decryptedJson);

            if (_cachedTokens != null && DateTime.UtcNow > _cachedTokens.ExpiresAtUtc)
            {
                return null;
            }

            _malService.Init(_cachedTokens?.AccessToken ?? string.Empty);
            return _cachedTokens;
        }
        catch (Exception ex)
        {
            Log.Information($"Error loading tokens: {ex.Message}");
            return null;
        }
    }

    public async Task SaveTokensAsync(TokenResponse tokenResponse)
    {
        StoredTokenData tokens = new()
        {
            AccessToken = tokenResponse.access_token ?? string.Empty,
            RefreshToken = tokenResponse.refresh_token,
            ExpiresAtUtc = DateTime.UtcNow.AddSeconds(tokenResponse.expires_in)
        };

        string json = JsonSerializer.Serialize(tokens);
        byte[] encryptedData = EncryptData(json);
        await File.WriteAllBytesAsync(_tokenFilePath, encryptedData);
        _cachedTokens = tokens;
    }

    public void ClearTokens()
    {
        if (File.Exists(_tokenFilePath))
        {
            File.Delete(_tokenFilePath);
        }
        _cachedTokens = null;
    }

    public bool HasValidToken()
    {
        return _cachedTokens != null &&
               !string.IsNullOrEmpty(_cachedTokens.AccessToken) &&
               DateTime.UtcNow < _cachedTokens.ExpiresAtUtc;
    }

    public string GetAccessToken()
    {
        return _cachedTokens?.AccessToken ?? string.Empty;
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