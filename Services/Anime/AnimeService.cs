using System.Collections.Concurrent;
using Aniki.Services.Auth;
using Aniki.Services.Auth.Providers;
using Aniki.Services.Interfaces;

namespace Aniki.Services.Anime;

public class AnimeService : IAnimeService
{
    private readonly Dictionary<ILoginProvider.ProviderType, IAnimeProvider> _providers = new();
    private readonly ConcurrentDictionary<ILoginProvider.ProviderType, GenericCacheService<int, AnimeDetails, AnimeField>> _caches;
    private readonly ISaveService _saveService;
    
    private IAnimeProvider? _currentProvider;
    private ILoginProvider.ProviderType? _currentProviderName;

    public bool IsLoggedIn => _currentProvider?.IsLoggedIn ?? false;
    public ILoginProvider.ProviderType? CurrentProviderName => _currentProviderName;

    public AnimeService(ISaveService saveService, ITokenService tokenService)
    {
        _saveService = saveService;

        _caches = new();
    }

    public void RegisterProvider(ILoginProvider.ProviderType name, IAnimeProvider provider)
    {
        _providers[name] = provider;
        
        CacheOptions options = new()
        {
            DefaultTimeToLive = TimeSpan.FromHours(8),
            DiskCachePath = $"{SaveService.MAIN_DIRECTORY}/cache/{name}",
            DiskSyncInterval = TimeSpan.FromMinutes(2),
            EnableDiskCache = true
        };
        
        _caches[name] = new GenericCacheService<int, AnimeDetails, AnimeField>(
            FetchFieldsFromProvider, 
            options
        );
    }

    public void SetActiveProvider(ILoginProvider.ProviderType providerName, string accessToken)
    {
        if (!_providers.TryGetValue(providerName, out IAnimeProvider? provider))
        {
            throw new ArgumentException($"Provider '{providerName}' not registered");
        }

        _currentProvider = provider;
        _currentProviderName = providerName;
        _currentProvider.Init(accessToken);
        
        //todo Clear cache when switching providers to avoid conflicts
        //_cache.Clear();
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

    public async Task<AnimeDetails> GetFieldsAsync(int animeId, bool forceFetch = false, params AnimeField[] fields)
    {
        if (_currentProvider != null)
            return await _caches[_currentProvider.Provider].GetOrFetchFieldsAsync(animeId, forceFetch, fields: fields);
        throw new Exception("This shouldn't happen");
    }

    public async Task<AnimeDetails> GetAllFieldsAsync(int animeId)
    {
        if (_currentProvider != null)
            return await _caches[_currentProvider.Provider].GetOrFetchFieldsAsync(
                animeId,
                fields: (AnimeField[])Enum.GetValues(typeof(AnimeField))
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
    
    public static readonly AnimeField[] MAL_NODE_FIELD_TYPES = new[]
    {
        AnimeField.ID, AnimeField.MY_LIST_STATUS, AnimeField.STATUS, AnimeField.GENRES, AnimeField.SYNOPSIS, AnimeField.MAIN_PICTURE,
        AnimeField.MEAN, AnimeField.POPULARITY, AnimeField.START_DATE, AnimeField.STUDIOS, AnimeField.TITLE, AnimeField.EPISODES
    };

    public async Task<List<AnimeDetails>> GetUserAnimeListAsync(AnimeStatus status = AnimeStatus.None)
    {
        List<AnimeDetails> animeList = await GetCurrentProvider().GetUserAnimeListAsync(status);

        if (_currentProvider != null)
        {
            foreach (AnimeDetails anime in animeList)
            {
                _caches[_currentProvider.Provider].UpdatePartial(
                    anime.Id,
                    anime,
                    MAL_NODE_FIELD_TYPES
                );
            }
        }
        
        return animeList;
    }

    public async Task RemoveFromUserListAsync(int animeId)
    {
        await GetCurrentProvider().RemoveFromUserListAsync(animeId);
        
        // Update cache to reflect removal
        AnimeDetails anime = await GetAllFieldsAsync(animeId);
        anime.UserStatus = null;
        if (_currentProvider != null)
            _caches[_currentProvider.Provider]
                .Update(anime.Id, anime, (AnimeField[])Enum.GetValues(typeof(AnimeField)));
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
        await GetFieldsAsync(animeId, true, AnimeField.MY_LIST_STATUS);
    }

    // ========================================================================
    // Search & Discovery
    // ========================================================================

    public async Task<List<AnimeDetails>> SearchAnimeAsync(string query)
    {
        List<AnimeDetails> results = await GetCurrentProvider().SearchAnimeAsync(query);
        
        // Update cache with search results
        if (_currentProvider != null)
        {
            foreach (AnimeDetails result in results)
            {
                _caches[_currentProvider.Provider].UpdatePartial(
                    result.Id,
                    result,
                    MAL_NODE_FIELD_TYPES
                );
            }
        }
        
        return results;
    }

    public async Task<List<RankingEntry>> GetTopAnimeAsync(RankingCategory category, int limit = 10)
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
                    MAL_NODE_FIELD_TYPES
                );
            }
        }

        return rankings;
    }
}