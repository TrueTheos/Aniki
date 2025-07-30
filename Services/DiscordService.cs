using Aniki.ViewModels;
using DiscordRPC;
using System;

namespace Aniki.Services
{
    public static class DiscordService
    {
        private const string ClientId = "1371263147792535592"; 

        private static DiscordRpcClient _client;
        private static bool _isDisposed = false;

        static DiscordService()
        {
            _client = new(ClientId);
            AppDomain.CurrentDomain.ProcessExit += (sender, e) => Reset();
        }

        public static void SetPresenceEpisode(Episode ep)
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
            Console.WriteLine("Presence set. Press any key to exit...");
        }

        public static void Reset()
        {
            if (!_isDisposed && _client != null)
            {
                _client.Dispose();
                _isDisposed = true;
            }
        }
    }
}
