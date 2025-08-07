

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
        
    public StatsViewModel()
    {
        LoadStats();
    }

    private async void LoadStats()
    {
        var animeList = await MalUtils.GetUserAnimeList();
        if (animeList == null || !animeList.Any())
            return;

        CalculateAnimeStats(animeList);
        CalculateGenreStats(animeList);
    }

    private void CalculateAnimeStats(List<AnimeData> animeList)
    {
        var stats = new AnimeStats();
        var scoredAnime = animeList.Where(a => a.ListStatus != null && a.ListStatus.Score > 0).ToList();
            
        animeList = animeList.Where(a => a.ListStatus != null).ToList();
        if (!scoredAnime.Any())
        {
            AnimeStats = stats;
            return;
        }
            
        stats.DaysWatched =  animeList.Sum(a => a.ListStatus!.NumEpisodesWatched * 24.0) / (24.0 * 60.0);
        stats.MeanScore = scoredAnime.Any() ? scoredAnime.Average(a => a.ListStatus!.Score) : 0;
        stats.Watching = animeList.Count(a => a.ListStatus!.Status == Misc.AnimeStatusApi.watching);
        stats.Completed = animeList.Count(a => a.ListStatus!.Status == Misc.AnimeStatusApi.completed);
        stats.OnHold = animeList.Count(a => a.ListStatus!.Status == Misc.AnimeStatusApi.on_hold);
        stats.Dropped = animeList.Count(a => a.ListStatus!.Status == Misc.AnimeStatusApi.dropped);
        stats.PlanToWatch = animeList.Count(a => a.ListStatus!.Status == Misc.AnimeStatusApi.plan_to_watch);
        stats.TotalEntries = animeList.Count;
        stats.Episodes = animeList.Sum(a => a.ListStatus!.NumEpisodesWatched);

        AnimeStats = stats;
    }

    private void CalculateGenreStats(List<AnimeData> animeList)
    {
        var allGenres = animeList.SelectMany(a => a.Node.Genres ?? new Genre[0]).ToList();
        var totalGenres = allGenres.Count;

        GenreStats = allGenres
            .GroupBy(g => g.Name)
            .Select(g =>
            {
                var animeInGenre = animeList.Where(a => a.Node.Genres?.Any(ag => ag.Name == g.Key) == true && a.ListStatus?.Score > 0).ToList();
                return new GenreStats
                {
                    Name = g.Key,
                    Count = g.Count(),
                    Percentage = totalGenres > 0 ? (double)g.Count() / totalGenres * 100 : 0,
                    MeanScore = animeInGenre.Where(a => a.ListStatus != null).Any() ?
                        animeInGenre.Where(a => a.ListStatus != null).Average(a => a.ListStatus!.Score) : 0
                };
            })
            .OrderByDescending(g => g.Count)
            .ToList();
    }
}