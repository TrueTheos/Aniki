namespace Aniki.Services.Interfaces;

public interface IJikanService
{
    Task<string?> GetAnimeTrailerUrlAsync(int malId);
}
