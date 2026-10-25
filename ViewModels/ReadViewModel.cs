using System.Collections.ObjectModel;
using System.Diagnostics;
using Aniki.Services.Anime;

namespace Aniki.ViewModels;

internal sealed partial class ReadViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmpty), nameof(ShowNoChapters), nameof(ShowProblem), nameof(MarkReadText))]
    public partial AnimeDetails? Manga { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNoChapters), nameof(ShowProblem))]
    public partial ObservableCollection<MangaChapter> Chapters { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageIndicator))]
    public partial ObservableCollection<string> Pages { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageIndicator))]
    public partial int CurrentPage { get; set; } = 1;

    public string PageIndicator => Pages.Count == 0 ? "" : $"Page {Math.Min(CurrentPage, Pages.Count)} / {Pages.Count}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MarkReadText), nameof(ShowProblem), nameof(ProblemTitle), nameof(ProblemText),
                              nameof(ShowOpenExternal), nameof(ShowSkip), nameof(ShowSearch), nameof(OpenExternalText), nameof(ShowWebReader))]
    public partial MangaChapter? SelectedChapter { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNoChapters), nameof(ShowProblem))]
    public partial bool IsLoading { get; set; }

    [ObservableProperty] public partial bool IsLoadingPages { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProblemText))]
    public partial string? Error { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowProblem), nameof(ProblemTitle), nameof(ProblemText), nameof(ShowSkip), nameof(ShowSearch))]
    public partial string? PageError { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProblemTitle), nameof(ProblemText))]
    public partial int RemovedChapters { get; set; }

    [ObservableProperty] public partial bool IsFullscreen { get; set; }
    [ObservableProperty] public partial ObservableCollection<AnimeDetails> ContinueReading { get; set; } = [];

    public bool ShowEmpty => Manga == null;
    public bool ShowNoChapters => Manga != null && !IsLoading && Chapters.Count == 0;
    private static readonly bool WebReaderSupported = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();

    private bool IsExternal => SelectedChapter?.IsExternal == true;
    public bool ShowWebReader => IsExternal && WebReaderSupported;
    public bool ShowProblem => ShowNoChapters || PageError != null || (IsExternal && !WebReaderSupported);
    public bool ShowOpenExternal => IsExternal;
    public bool ShowSkip => IsExternal || PageError != null;
    public bool ShowSearch => !IsExternal;
    public string OpenExternalText => $"Read on {SelectedChapter?.Source}";

    public string ProblemTitle => IsExternal ? $"Chapter {SelectedChapter!.Number} is on {SelectedChapter.Source}"
        : PageError != null ? "This chapter isn't available"
        : RemovedChapters > 0 ? "English chapters were taken down"
        : "No English chapters on MangaDex";

    public string ProblemText => IsExternal
        ? "The publisher hosts this chapter officially, so MangaDex can't show its pages here. It opens in your browser."
        : PageError ?? Error ?? (RemovedChapters > 0
            ? $"MangaDex had {RemovedChapters} English chapters, but they were removed, usually because the series is officially licensed. Search MangaDex for another upload, or keep tracking your progress from the details page."
            : "MangaDex has no English chapters for this title. You can still track your progress from the details page.");

    private int ChaptersRead => Manga?.UserStatus?.EpisodesWatched ?? 0;
    public string MarkReadText => SelectedChapter != null && (int)SelectedChapter.NumberValue is var n && n > 0 && n <= ChaptersRead
        ? "Read ✓"
        : "Mark as read";

    private readonly IAnimeService _animeService;
    private readonly MangaDexService _mangaDex;

    private static MainViewModel Main => DependencyInjection.Instance.ServiceProvider!.GetRequiredService<MainViewModel>();

    public ReadViewModel(IAnimeService animeService, MangaDexService mangaDex)
    {
        _animeService = animeService;
        _mangaDex = mangaDex;
    }

    public override Task Enter()
    {
        if (Manga == null) _ = LoadContinueReadingAsync();
        return base.Enter();
    }

    private async Task LoadContinueReadingAsync()
    {
        if (!AnimeService.IsLoggedIn) return;
        try
        {
            var reading = await _animeService.Manga.GetUserAnimeListAsync(AnimeStatus.Watching).ConfigureAwait(true);
            ContinueReading = new ObservableCollection<AnimeDetails>(reading.Take(12));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load reading list: {ex.Message}");
        }
    }

    public async Task OpenAsync(AnimeDetails manga)
    {
        if (Manga?.Id == manga.Id && Chapters.Count > 0) return;

        Manga = manga;
        Chapters = [];
        Pages.Clear();
        SelectedChapter = null;
        Error = null;
        PageError = null;
        RemovedChapters = 0;
        IsLoading = true;
        try
        {
            string? id = await _mangaDex.FindMangaIdAsync(manga.Id, AnimeService.CurrentProviderType, manga.Title ?? "").ConfigureAwait(true);
            (List<MangaChapter> chapters, int removed) = id == null ? ([], 0) : await _mangaDex.GetChaptersAsync(id).ConfigureAwait(true);
            if (Manga != manga) return;

            RemovedChapters = removed;
            Chapters = new ObservableCollection<MangaChapter>(chapters);
            if (chapters.Count > 0)
                SelectedChapter = chapters.FirstOrDefault(c => c.NumberValue > ChaptersRead) ?? chapters[0];
        }
        catch (Exception ex)
        {
            Error = $"Couldn't reach MangaDex: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    async partial void OnSelectedChapterChanged(MangaChapter? value)
    {
        Pages.Clear();
        ChapterPageLoader.Instance.ReleaseChapter();
        PageError = null;
        CurrentPage = 1;
        if (value == null || value.IsExternal) return;

        IsLoadingPages = true;
        try
        {
            List<string> pages = await _mangaDex.GetPageUrlsAsync(value.Id).ConfigureAwait(true);
            if (SelectedChapter == value)
                Pages = new ObservableCollection<string>(pages);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            if (SelectedChapter == value)
                PageError = $"MangaDex still lists chapter {value.Number}, but its pages were taken down. Try another chapter or search MangaDex.";
        }
        catch (Exception ex)
        {
            if (SelectedChapter == value)
                PageError = $"Couldn't load chapter {value.Number}: {ex.Message}";
        }
        finally
        {
            if (SelectedChapter == value) IsLoadingPages = false;
        }
    }

    [RelayCommand]
    private async Task NextChapter()
    {
        await MarkRead(quiet: true).ConfigureAwait(true);
        await SkipChapter().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task SkipChapter()
    {
        int index = SelectedChapter == null ? -1 : Chapters.IndexOf(SelectedChapter);
        if (index >= 0 && index + 1 < Chapters.Count)
            SelectedChapter = Chapters[index + 1];
        else if (index >= 0)
            await ToastService.Show("That was the last chapter on MangaDex").ConfigureAwait(true);
    }

    [RelayCommand]
    private void PreviousChapter()
    {
        int index = SelectedChapter == null ? -1 : Chapters.IndexOf(SelectedChapter);
        if (index > 0)
            SelectedChapter = Chapters[index - 1];
    }

    [RelayCommand]
    private Task MarkReadClicked() => MarkRead(quiet: false);

    private async Task MarkRead(bool quiet)
    {
        if (Manga == null || SelectedChapter == null) return;

        int chapter = (int)SelectedChapter.NumberValue;
        string? skip = !AnimeService.IsLoggedIn ? "Log in to track your reading"
            : chapter < 1 ? $"Chapter {SelectedChapter.Number} is a prologue, so it doesn't count toward progress"
            : chapter <= ChaptersRead ? $"Already marked up to chapter {ChaptersRead}"
            : null;
        if (skip != null)
        {
            if (!quiet) await ToastService.Show(skip).ConfigureAwait(true);
            return;
        }

        try
        {
            UserAnimeStatus? status = Manga.UserStatus;
            if (status == null || status.Status is AnimeStatus.None or AnimeStatus.PlanToWatch)
                await _animeService.Manga.SetAnimeStatusAsync(Manga.Id, AnimeStatus.Watching).ConfigureAwait(true);
            await _animeService.Manga.SetEpisodesWatchedAsync(Manga.Id, chapter).ConfigureAwait(true);

            Manga.UserStatus ??= new UserAnimeStatus { Status = AnimeStatus.Watching };
            Manga.UserStatus.EpisodesWatched = chapter;
            OnPropertyChanged(nameof(MarkReadText));
            await ToastService.Show($"✓ Chapter {chapter} marked as read").ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            await ToastService.Show($"Failed to update progress: {ex.Message}").ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private void ToggleFullscreen() => IsFullscreen = !IsFullscreen;

    [RelayCommand]
    private void OpenExternal()
    {
        if (SelectedChapter?.ExternalUrl is { } url)
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    [RelayCommand]
    private Task Open(AnimeDetails manga) => OpenAsync(manga);

    [RelayCommand]
    private static void BrowseManga() => Main.ShowAnimeBrowsePageCommand.Execute(null);

    [RelayCommand]
    private static void Explore() => Main.ShowExplorePageCommand.Execute(null);

    [RelayCommand]
    private async Task BackToDetails()
    {
        if (Manga != null) await Main.GoToMedia(Manga.Id).ConfigureAwait(true);
    }

    [RelayCommand]
    private void SearchMangaDex()
    {
        if (Manga?.Title == null) return;
        Process.Start(new ProcessStartInfo($"https://mangadex.org/search?q={Uri.EscapeDataString(Manga.Title)}") { UseShellExecute = true });
    }
}
