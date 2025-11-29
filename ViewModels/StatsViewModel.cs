using Aniki.Services.Anime;
using Aniki.Services.Interfaces;

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
    [ObservableProperty]
    private AnimeStats? _animeStats;

    [ObservableProperty]
    private List<GenreStats>? _genreStats;
    
    private readonly IAnimeService _malService;
        
    public StatsViewModel(IAnimeService malService)
    {
        _malService = malService;
        LoadStats();
    }

    private async void LoadStats()
    {
        if(!MalService.IS_LOGGED_IN) return;
        
        var animeList = await _malService.GetUserAnimeListAsync();
        if (animeList == null || !animeList.Any())
            return;

        CalculateAnimeStats(animeList);
        CalculateGenreStats(animeList);
    }

    private void CalculateAnimeStats(List<AnimeData> animeList)
    {
        AnimeStats stats = new AnimeStats();
    
        var validAnime = animeList.Where(a => a.UserStatus != null).ToList();
        var scoredAnime = validAnime.Where(a => a.UserStatus!.Score > 0).ToList();
    
        if (!scoredAnime.Any())
        {
            AnimeStats = stats;
            return;
        }
            
        stats.DaysWatched =  validAnime.Sum(a => a.UserStatus!.EpisodesWatched * 24.0) / (24.0 * 60.0);
        stats.MeanScore = scoredAnime.Any() ? scoredAnime.Average(a => a.UserStatus!.Score) : 0;
        stats.Watching = validAnime.Count(a => a.UserStatus!.Status == AnimeStatus.Watching);
        stats.Completed = validAnime.Count(a => a.UserStatus!.Status == AnimeStatus.Completed);
        stats.OnHold = validAnime.Count(a => a.UserStatus!.Status == AnimeStatus.OnHold);
        stats.Dropped = validAnime.Count(a => a.UserStatus!.Status == AnimeStatus.Dropped);
        stats.PlanToWatch = validAnime.Count(a => a.UserStatus!.Status == AnimeStatus.PlanToWatch);
        stats.TotalEntries = validAnime.Count;
        stats.Episodes = validAnime.Sum(a => a.UserStatus!.EpisodesWatched);

        AnimeStats = stats;
    }

    private void CalculateGenreStats(List<AnimeData> animeList)
    {
        var allGenres = animeList.SelectMany(a => a.Details.Genres ?? new Genre[0]).ToList();
        var totalGenres = allGenres.Count;

        GenreStats = allGenres
            .GroupBy(g => g.Name)
            .Select(g =>
            {
                var animeInGenre = animeList.Where(a => a.Details.Genres?.Any(ag => ag.Name == g.Key) == true && a.UserStatus?.Score > 0).ToList();
                return new GenreStats
                {
                    Name = g.Key,
                    Count = g.Count(),
                    Percentage = totalGenres > 0 ? (double)g.Count() / totalGenres * 100 : 0,
                    MeanScore = animeInGenre.Where(a => a.UserStatus != null).Any() ?
                        animeInGenre.Where(a => a.UserStatus != null).Average(a => a.UserStatus!.Score) : 0
                };
            })
            .OrderByDescending(g => g.Count)
            .ToList();
    }
}