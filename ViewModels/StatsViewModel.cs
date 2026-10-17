using System.Diagnostics;
using Aniki.Services.Anime;
using Aniki.Services.Auth;

namespace Aniki.ViewModels;

internal sealed class GenreStats
{
    public required string Name { get; set; }
    public int Count { get; set; }
    public double Percentage { get; set; }
    public double MeanScore { get; set; }
}

internal sealed class ScoreBucket
{
    public int Score { get; set; }
    public int Count { get; set; }
    public double BarHeight { get; set; }
    public int MaxCount { get; set; }
}

internal sealed partial class StatsViewModel : ViewModelBase
{
    [ObservableProperty] public partial AnimeStats? AnimeStats { get; set; }
    [ObservableProperty] public partial List<GenreStats>? GenreStats { get; set; }
    [ObservableProperty] public partial List<ScoreBucket>? ScoreDistribution { get; set; }
    [ObservableProperty] public partial UserData? User { get; set; }
    [ObservableProperty] public partial List<AnimeDetails>? CurrentlyWatching { get; set; }
    [ObservableProperty] public partial List<AnimeDetails>? TopRated { get; set; }
    [ObservableProperty] public partial List<GenreStats>? TopGenres { get; set; }
    [ObservableProperty] public partial string? BackdropUrl { get; set; }

    [ObservableProperty] public partial string ProviderName { get; set; } = "";
    public string UserInitial => string.IsNullOrEmpty(User?.Name) ? "?" : User.Name[..1].ToUpperInvariant();
    public bool HasPicture => !string.IsNullOrEmpty(User?.Picture);

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
            ProviderName = AnimeService.CurrentProviderType == ILoginProvider.ProviderType.AniList ? "AniList" : "MyAnimeList";

            try
            {
                User = await _malService.GetUserDataAsync().ConfigureAwait(true);
                OnPropertyChanged(nameof(UserInitial));
                OnPropertyChanged(nameof(HasPicture));
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }

            var animeList = await _malService.GetUserAnimeListAsync().ConfigureAwait(true);
            if (animeList.Count == 0)
                return;

            CalculateAnimeStats(animeList);
            CalculateGenreStats(animeList);
            CalculateScoreDistribution(animeList);
            CalculateHighlights(animeList);
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

    private void CalculateHighlights(List<AnimeDetails> animeList)
    {
        CurrentlyWatching = animeList
            .Where(a => a.UserStatus?.Status == AnimeStatus.Watching)
            .OrderByDescending(a => a.UserStatus!.EpisodesWatched)
            .Take(10)
            .ToList();

        TopRated = animeList
            .Where(a => a.UserStatus?.Score > 0)
            .OrderByDescending(a => a.UserStatus!.Score)
            .ThenByDescending(a => a.Mean)
            .Take(12)
            .ToList();

        TopGenres = GenreStats?.Take(10).ToList();
        BackdropUrl = (TopRated.FirstOrDefault() ?? CurrentlyWatching.FirstOrDefault())?.MainPicture?.Large;
    }

    private void CalculateScoreDistribution(List<AnimeDetails> animeList)
    {
        int[] counts = new int[11];
        foreach (AnimeDetails a in animeList)
        {
            int s = a.UserStatus?.Score ?? 0;
            if (s is >= 1 and <= 10) counts[s]++;
        }

        int max = Math.Max(1, counts.Max());
        ScoreDistribution = Enumerable.Range(1, 10).Reverse()
            .Select(s => new ScoreBucket { Score = s, Count = counts[s], MaxCount = max, BarHeight = Math.Max(3, 130.0 * counts[s] / max) })
            .ToList();
    }
}
