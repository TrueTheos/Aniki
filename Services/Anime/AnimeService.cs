using System.Collections.Concurrent;
using Aniki.Services.Auth;
using Aniki.Services.Cache;
using Aniki.Services.Save;

namespace Aniki.Services.Anime;

public class AnimeService : IAnimeService
{
    private readonly Dictionary<ILoginProvider.ProviderType, IAnimeProvider> _providers = new();
    private readonly ConcurrentDictionary<ILoginProvider.ProviderType, GenericCacheService<int, AnimeDetails, AnimeField>> _caches = new();

    private IAnimeProvider? _currentProvider;

    public static bool IsLoggedIn;
    public static ILoginProvider.ProviderType CurrentProviderType = ILoginProvider.ProviderType.Mal;

    public void RegisterProvider(ILoginProvider.ProviderType name, IAnimeProvider provider)
    {
        _providers[name] = provider;
        
        CacheOptions options = new()
        {
            DefaultTimeToLive = TimeSpan.FromHours(8),
            DiskCachePath = $"{SaveService.CachePath}/{name}",
            DiskSyncInterval = TimeSpan.FromMinutes(2),
            EnableDiskCache = true
        };
        
        _caches[name] = new GenericCacheService<int, AnimeDetails, AnimeField>(
            FetchFieldsFromProvider, 
            options
        );
    }

    public void SetActiveProvider(ILoginProvider.ProviderType providerName, string? accessToken)
    {
        if (!_providers.TryGetValue(providerName, out IAnimeProvider? provider))
        {
            throw new ArgumentException($"Provider '{providerName}' not registered");
        }

        _currentProvider = provider;
        CurrentProviderType = providerName;
        _currentProvider.Init(accessToken);

        IsLoggedIn = _currentProvider.IsLoggedIn;
        if (!IsLoggedIn) CurrentProviderType = ILoginProvider.ProviderType.Mal; //we just use MAL as default
    }

    private IAnimeProvider GetCurrentProvider()
    {
        if (_currentProvider == null)
        {
            throw new InvalidOperationException("No active provider set");
        }
        return _currentProvider;
    }
    
    public void SubscribeToFieldChange(int animeId, FieldChangeHandler<AnimeDetails> handler, params AnimeField[] fields)
    {
        if (_currentProvider != null)
            _caches[_currentProvider.Provider].SubscribeToFieldChange(animeId, handler, fields);
    }

    public void UnsubscribeFromFieldChange(int animeId, FieldChangeHandler<AnimeDetails> handler, params AnimeField[] fields)
    {
        if (_currentProvider != null)
            _caches[_currentProvider.Provider].UnsubscribeFromFieldChange(animeId, handler, fields);
    }

    public async Task<AnimeDetails?> GetFieldsAsync(int animeId, bool forceFetch = false, params AnimeField[] fields)
    {
        if (_currentProvider != null)
            return await _caches[_currentProvider.Provider].GetOrFetchFieldsAsync(animeId, forceFetch, fields: fields);
        return null;
    }

    public async Task<AnimeDetails> GetAllFieldsAsync(int animeId)
    {
        if (_currentProvider != null)
            return await _caches[_currentProvider.Provider].GetOrFetchFieldsAsync(
                animeId,
                fields: Enum.GetValues<AnimeField>()
            );
        throw new Exception("This shouldn't happen"); 
    }

    private async Task<AnimeDetails?> FetchFieldsFromProvider(int animeId, params AnimeField[] fields)
    {
        IAnimeProvider provider = GetCurrentProvider();
        AnimeDetails? details = await provider.FetchAnimeDetailsAsync(animeId, fields);
        
        if (details != null && _currentProvider != null)
        {
            _caches[_currentProvider.Provider].Update(animeId, details, fields);
        }
        
        return details;
    }

    public async Task<UserData> GetUserDataAsync()
    {
        return await GetCurrentProvider().GetUserDataAsync();
    }
    
    public static readonly AnimeField[] MalNodeFieldTypes = new[]
    {
        AnimeField.Id, AnimeField.MyListStatus, AnimeField.Status, AnimeField.Genres, AnimeField.Synopsis, AnimeField.MainPicture,
        AnimeField.Mean, AnimeField.Popularity, AnimeField.StartDate, AnimeField.Studios, AnimeField.Title, AnimeField.Episodes, AnimeField.MediaType
    };

    public async Task<List<AnimeDetails>> GetUserAnimeListAsync(AnimeStatus status = AnimeStatus.None)
    {
        if (_currentProvider != null)
        {
            List<AnimeDetails> animeList = await GetCurrentProvider().GetUserAnimeListAsync(status);
            
            foreach (AnimeDetails anime in animeList)
            {
                //todo, dont use the inner list of animes in provider. cuz it doesnt update. we need to make sure it updates each time we make a change or just use cache
                _caches[_currentProvider.Provider].UpdatePartial(
                    anime.Id,
                    anime,
                    MalNodeFieldTypes
                );
            }

            return animeList;
        }
        
        return new();
    }

    public async Task RemoveFromUserListAsync(int animeId)
    {
        await GetCurrentProvider().RemoveFromUserListAsync(animeId);
        
        // Update cache to reflect removal
        AnimeDetails anime = await GetAllFieldsAsync(animeId);
        anime.UserStatus = null;
        if (_currentProvider != null)
            _caches[_currentProvider.Provider]
                .Update(anime.Id, anime, Enum.GetValues<AnimeField>());
    }

    public async Task SetAnimeStatusAsync(int animeId, AnimeStatus status)
    {
        await GetCurrentProvider().SetAnimeStatusAsync(animeId, status);
        await AfterUserStatusChange(animeId);
    }
    
    public async Task SetAnimeScoreAsync(int animeId, int score)
    {
        await GetCurrentProvider().SetAnimeScoreAsync(animeId, score);
        await AfterUserStatusChange(animeId);
    }

    public async Task SetEpisodesWatchedAsync(int animeId, int episodes)
    {
        await GetCurrentProvider().SetEpisodesWatchedAsync(animeId, episodes);
        await AfterUserStatusChange(animeId);
    }

    private async Task AfterUserStatusChange(int animeId)
    {
        await GetFieldsAsync(animeId, true, AnimeField.MyListStatus);
    }

    public async Task<List<AnimeDetails>> SearchAnimeAsync(string query)
    {
        List<AnimeDetails> results = await GetCurrentProvider().SearchAnimeAsync(query);
        
        if (_currentProvider != null)
        {
            foreach (AnimeDetails result in results)
            {
                _caches[_currentProvider.Provider].UpdatePartial(
                    result.Id,
                    result,
                    MalNodeFieldTypes
                );
            }
        }
        
        return results;
    }

    public async Task<List<RankingEntry>> GetTopAnimeAsync(RankingCategory category, int limit)
    {
        List<RankingEntry> rankings = await GetCurrentProvider().GetTopAnimeAsync(category, limit);
        
        // Update cache with ranking data
        if (_currentProvider != null)
        {
            foreach (RankingEntry entry in rankings)
            {
                _caches[_currentProvider.Provider].UpdatePartial(
                    entry.Details.Id,
                    entry.Details,
                    MalNodeFieldTypes
                );
            }
        }

        return rankings;
    }
}