namespace Aniki.Services.Interfaces;

public interface IDiscordService
{
    public void SetPresenceEpisode(string animeTitle, int episodeNumber);
    public void Reset();
}