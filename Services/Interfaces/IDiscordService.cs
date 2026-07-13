namespace Aniki.Services.Interfaces;

internal interface IDiscordService
{
    public bool IsEnabled { get; }
    public void SetEnabled(bool enabled);
    public void SetPresenceEpisode(string animeTitle, int episodeNumber);
    public void Reset();
}