using Aniki.Services.Anime;
using Aniki.Services.Anime.Providers;
using Aniki.Services.Auth;
using Aniki.Services.Auth.Providers;
using Aniki.Services.Interfaces;
using Aniki.Services.Save;
using Microsoft.Extensions.DependencyInjection;

namespace Aniki.Misc;

public static class ServiceCollectionExtensions
{
    public static void AddCommonServices(this IServiceCollection collection)
    {
        collection.AddSingleton<ILoginService, LoginService>();
        collection.AddSingleton<IAbsoluteEpisodeParser, AbsoluteEpisodeParser>();
        collection.AddSingleton<IAnimeNameParser, AnimeNameParser>();
        collection.AddSingleton<ICalendarService, CalendarService>();
        collection.AddSingleton<IDiscordService, DiscordService>();
        collection.AddSingleton<IAnimeService, AnimeService>();
        collection.AddSingleton<INyaaService, NyaaService>();
        collection.AddSingleton<ISaveService, SaveService>();
        collection.AddSingleton<ITokenService, TokenService>();
        collection.AddSingleton<IAllMangaScraperService, AllMangaScraperService>();
        collection.AddSingleton<IVideoPlayerService, VideoPlayerService>();

        collection.AddSingleton<AnilistService>();
        collection.AddSingleton<MalService>();
        
        collection.AddSingleton<ILoginProvider, AnilistLoginProvider>();
        collection.AddSingleton<ILoginProvider, MalLoginProvider>();

        collection.AddSingleton<LoginViewModel>();
        collection.AddSingleton<MainViewModel>();
        collection.AddSingleton<AnimeDetailsViewModel>();
        collection.AddSingleton<UserAnimeListViewModel>();
        collection.AddSingleton<CalendarViewModel>();
        collection.AddSingleton<ConfirmEpisodeViewModel>();
        collection.AddSingleton<SettingsViewModel>();
        collection.AddSingleton<StatsViewModel>();
        collection.AddSingleton<TorrentSearchViewModel>();
        collection.AddSingleton<WatchAnimeViewModel>();
        collection.AddSingleton<AnimeBrowseViewModel>();
        collection.AddSingleton<DownloadedViewModel>();
        collection.AddSingleton<OnlineViewModel>();
    }
}