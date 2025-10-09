using Aniki.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Aniki.Misc;

public static class ServiceCollectionExtensions
{
    public static void AddCommonServices(this IServiceCollection collection)
    {
        collection.AddSingleton<IOAuthService, OAuthService>();
        collection.AddSingleton<IAbsoluteEpisodeParser, AbsoluteEpisodeParser>();
        collection.AddSingleton<IAnimeNameParser, AnimeNameParser>();
        collection.AddSingleton<ICalendarService, CalendarService>();
        collection.AddSingleton<IDiscordService, DiscordService>();
        collection.AddSingleton<IMalService, MalService>();
        collection.AddSingleton<INyaaService, NyaaService>();
        collection.AddSingleton<IOAuthService, OAuthService>();
        collection.AddSingleton<ISaveService, SaveService>();
        collection.AddSingleton<ITokenService, TokenService>();

        collection.AddSingleton<LoginViewModel>();
        collection.AddSingleton<MainViewModel>();
        collection.AddSingleton<AnimeDetailsViewModel>();
        collection.AddSingleton<CalendarViewModel>();
        collection.AddSingleton<ConfirmEpisodeViewModel>();
        collection.AddSingleton<SettingsViewModel>();
        collection.AddSingleton<StatsViewModel>();
        collection.AddSingleton<TorrentSearchViewModel>();
        collection.AddSingleton<WatchAnimeViewModel>();
        collection.AddSingleton<AnimeBrowseViewModel>();
    }
}