namespace Aniki.Services.Interfaces;

public interface IDiscordService
{
    public void SetPresenceEpisode(DownloadedEpisode ep);
    public void Reset();
}