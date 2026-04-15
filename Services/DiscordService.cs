using Aniki.Services.Interfaces;
using Aniki.ViewModels;
using DiscordRPC;
using DiscordRPC.Logging;

namespace Aniki.Services;

public class DiscordService : IDiscordService, IDisposable
{
    private const string CLIENT_ID = "1371263147792535592"; 
    private DiscordRpcClient? _client;

    public bool IsEnabled { get; private set; } = true;

    public DiscordService(ISaveService saveService)
    {
        SettingsConfig? config = saveService.GetSettingsConfig();
        if (config != null)
        {
            IsEnabled = config.EnableDiscordPresence;
        }

        if (IsEnabled)
        {
            InitializeClient();
        }
    }

    public void SetEnabled(bool enabled)
    {
        IsEnabled = enabled;
        if (!enabled)
        {
            Reset();
        }
    }

    private void InitializeClient()
    {
        try 
        {
            _client = new DiscordRpcClient(CLIENT_ID);

            _client.Logger = new ConsoleLogger { Level = LogLevel.Warning };

            _client.OnReady += (_, e) => 
            {
                Log.Information($"Discord RPC Ready. Connected to user: {e.User.Username}");
            };

            _client.OnError += (_, e) =>
            {
                Log.Error($"Discord RPC Error: {e.Code} - {e.Message}");
            };
            
            _client.OnConnectionFailed += (_, e) =>
            {
                Log.Error($"Discord RPC Connection Failed: {e}");
            };

            _client.Initialize();
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to initialize Discord RPC: {ex.Message}");
        }
    }

    public void SetPresenceEpisode(string animeTitle, int episodeNumber)
    {
        if (!IsEnabled) return;

        if (_client == null || _client.IsDisposed)
        {
            InitializeClient();
        }

        Assets assets = new()
        {
            LargeImageKey = "aniki_logo",
            LargeImageText = "Aniki Player"
        };

        _client?.SetPresence(new RichPresence
        {
            Details = $"Watching: {animeTitle}",
            State = $"Episode {episodeNumber}",
            Timestamps = Timestamps.Now,
            Assets = assets,
            Buttons =
            [
                new() { Label = "Get Aniki", Url = "https://github.com/TrueTheos/Aniki" }
            ]
        });
    }

    public void Reset()
    {
        _client?.ClearPresence();
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}