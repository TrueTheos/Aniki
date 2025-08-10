using Aniki.Services.Interfaces;
using DiscordRPC;

namespace Aniki.Services;

public class DiscordService : IDiscordService
{
    private const string ClientId = "1371263147792535592"; 

    private DiscordRpcClient _client;
    private bool _isDisposed = false;

    public DiscordService()
    {
        _client = new(ClientId);
        AppDomain.CurrentDomain.ProcessExit += (sender, e) => Reset();
    }

    public void SetPresenceEpisode(Episode ep)
    {
        if (_isDisposed)
        {
            _client = new(ClientId);
            _isDisposed = false;
        }

        if (!_client.IsInitialized)
            _client.Initialize();

        _client.SetPresence(new()
        {
            Details = $"Watching: {ep.Title}",
            State = $"Episode {ep.EpisodeNumber}",
            Assets = new()
            {
                LargeImageKey = "default",
                LargeImageText = "Use Aniki"
            },
            Buttons = new DiscordRPC.Button[]
            {
                new() { Label = "Use Aniki", Url = "https://github.com/TrueTheos/Aniki" }
            }
        });
        Log.Information("Presence set. Press any key to exit...");
    }

    public void Reset()
    {
        if (!_isDisposed && _client != null)
        {
            _client.Dispose();
            _isDisposed = true;
        }
    }
}