namespace Aniki.Services.Interfaces;

public interface IAnimeNameParser
{
    public Task<ParseResult> ParseAnimeFilename(string filename);
}