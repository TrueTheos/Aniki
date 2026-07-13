namespace Aniki.Services.Interfaces;

internal interface IJikanService
{
    Task<string?> GetAnimeTrailerUrlAsync(int malId);
}
