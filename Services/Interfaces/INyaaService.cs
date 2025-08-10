namespace Aniki.Services.Interfaces;

public interface INyaaService
{
    public Task<List<NyaaTorrent>> SearchAsync(string animeName, int episodeNumber);
}