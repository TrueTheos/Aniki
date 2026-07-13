namespace Aniki.Services.Interfaces;

internal interface INyaaService
{
    public Task<List<NyaaTorrent>> SearchAsync(string animeName, string episodeNumber);
}