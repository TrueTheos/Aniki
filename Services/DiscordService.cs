using Aniki.Services.Interfaces;
using DiscordRPC;
using DiscordRPC.Logging;

namespace Aniki.Services;

public class DiscordService : IDiscordService, IDisposable
{
    private const string ClientId = "1371263147792535592"; 
    private DiscordRpcClient? _client;

    public DiscordService()
    {
        InitializeClient();
    }

    private void InitializeClient()
    {
        try 
        {
            _client = new DiscordRpcClient(ClientId);

            _client.Logger = new ConsoleLogger { Level = LogLevel.Warning };

            _client.OnReady += (sender, e) => 
            {
                Log.Information($"Discord RPC Ready. Connected to user: {e.User.Username}");
            };

            _client.OnError += (sender, e) =>
            {
                Log.Error($"Discord RPC Error: {e.Code} - {e.Message}");
            };
            
            _client.OnConnectionFailed += (sender, e) =>
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
        if (_client == null || _client.IsDisposed)
        {
            InitializeClient();
        }

        var assets = new Assets
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
            Buttons = new DiscordRPC.Button[]
            {
                new() { Label = "Get Aniki", Url = "https://github.com/TrueTheos/Aniki" }
            }
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