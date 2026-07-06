using System.Diagnostics;
using Aniki.Services.Anime;

namespace Aniki.ViewModels;

public class GenreStats
{
    public required string Name { get; set; }
    public int Count { get; set; }
    public double Percentage { get; set; }
    public double MeanScore { get; set; }
}
    
public partial class StatsViewModel : ViewModelBase
{
    [ObservableProperty] public partial AnimeStats? AnimeStats { get; set; }
    [ObservableProperty] public partial List<GenreStats>? GenreStats { get; set; }

    private readonly IAnimeService _malService;
        
    public StatsViewModel(IAnimeService malService)
    {
        _malService = malService;
        LoadStats();
    }

    private async void LoadStats()
    {
        try
        {
            if(!AnimeService.IsLoggedIn) return;
        
            var animeList = await _malService.GetUserAnimeListAsync();
            if (animeList.Count == 0)
                return;

            CalculateAnimeStats(animeList);
            CalculateGenreStats(animeList);
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }
    }

    private void CalculateAnimeStats(List<AnimeDetails> animeList)
    {
        AnimeStats stats = new();
    
        var validAnime = animeList.Where(a => a.UserStatus != null).ToList();
        var scoredAnime = validAnime.Where(a => a.UserStatus!.Score > 0).ToList();
    
        if (scoredAnime.Count == 0)
        {
            AnimeStats = stats;
            return;
        }
            
        stats.DaysWatched =  validAnime.Sum(a => a.UserStatus!.EpisodesWatched * 24.0) / (24.0 * 60.0);
        stats.MeanScore = scoredAnime.Count != 0 ? scoredAnime.Average(a => a.UserStatus!.Score) : 0;
        stats.Watching = validAnime.Count(a => a.UserStatus!.Status == AnimeStatus.Watching);
        stats.Completed = validAnime.Count(a => a.UserStatus!.Status == AnimeStatus.Completed);
        stats.OnHold = validAnime.Count(a => a.UserStatus!.Status == AnimeStatus.OnHold);
        stats.Dropped = validAnime.Count(a => a.UserStatus!.Status == AnimeStatus.Dropped);
        stats.PlanToWatch = validAnime.Count(a => a.UserStatus!.Status == AnimeStatus.PlanToWatch);
        stats.TotalEntries = validAnime.Count;
        stats.Episodes = validAnime.Sum(a => a.UserStatus!.EpisodesWatched);

        AnimeStats = stats;
    }

    private void CalculateGenreStats(List<AnimeDetails> animeList)
    {
        var allGenres = animeList.SelectMany(a => a.Genres ?? []).ToList();
        int totalGenres = allGenres.Count;

        GenreStats = allGenres
            .GroupBy(g => g)
            .Select(g =>
            {
                var animeInGenre = animeList.Where(a => a.Genres?.Any(ag => ag == g.Key) == true && a.UserStatus?.Score > 0).ToList();
                return new GenreStats
                {
                    Name = g.Key,
                    Count = g.Count(),
                    Percentage = totalGenres > 0 ? (double)g.Count() / totalGenres * 100 : 0,
                    MeanScore = animeInGenre.Any(a => a.UserStatus != null) ?
                        animeInGenre.Where(a => a.UserStatus != null).Average(a => a.UserStatus!.Score) : 0
                };
            })
            .OrderByDescending(g => g.Count)
            .ToList();
    }
}