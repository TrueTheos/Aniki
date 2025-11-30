using Aniki.Services.Auth;
using Aniki.Services.Auth.Providers;
using Aniki.Services.Interfaces;

namespace Aniki.Services.Anime;

public class AnimeService : IAnimeService
{
    private readonly Dictionary<ILoginProvider.ProviderType, IAnimeProvider> _providers = new();
    private readonly GenericCacheService<int, AnimeDetails, AnimeField> _cache;
    private readonly ISaveService _saveService;
    
    private IAnimeProvider? _currentProvider;
    private ILoginProvider.ProviderType? _currentProviderName;

    public bool IsLoggedIn => _currentProvider?.IsLoggedIn ?? false;
    public ILoginProvider.ProviderType? CurrentProviderName => _currentProviderName;

    public AnimeService(ISaveService saveService, ITokenService tokenService)
    {
        _saveService = saveService;
        
        CacheOptions options = new()
        {
            DefaultTimeToLive = TimeSpan.FromHours(8),
            DiskCachePath = $"{SaveService.MAIN_DIRECTORY}/cache",
            DiskSyncInterval = TimeSpan.FromMinutes(2),
            EnableDiskCache = true
        };

        _cache = new GenericCacheService<int, AnimeDetails, AnimeField>(
            FetchFieldsFromProvider, 
            options
        );
    }

    public void RegisterProvider(ILoginProvider.ProviderType name, IAnimeProvider provider)
    {
        _providers[name] = provider;
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
        _cache.SubscribeToFieldChange(animeId, handler, fields);
    }

    public void UnsubscribeFromFieldChange(int animeId, FieldChangeHandler<AnimeDetails> handler, params AnimeField[] fields)
    {
        _cache.UnsubscribeFromFieldChange(animeId, handler, fields);
    }

    public async Task<AnimeDetails> GetFieldsAsync(int animeId, bool forceFetch = false, params AnimeField[] fields)
    {
        return await _cache.GetOrFetchFieldsAsync(animeId, forceFetch, fields: fields);
    }

    public async Task<AnimeDetails> GetAllFieldsAsync(int animeId)
    {
        return await _cache.GetOrFetchFieldsAsync(
            animeId, 
            fields: (AnimeField[])Enum.GetValues(typeof(AnimeField))
        );
    }

    private async Task<AnimeDetails?> FetchFieldsFromProvider(int animeId, params AnimeField[] fields)
    {
        IAnimeProvider provider = GetCurrentProvider();
        AnimeDetails? details = await provider.FetchAnimeDetailsAsync(animeId, fields);
        
        if (details != null)
        {
            _cache.Update(animeId, details, fields);
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

    public async Task<List<AnimeData>> GetUserAnimeListAsync(AnimeStatus status = AnimeStatus.None)
    {
        List<AnimeData> animeList = await GetCurrentProvider().GetUserAnimeListAsync(status);
        
        foreach (AnimeData anime in animeList)
        {
            _cache.UpdatePartial(
                anime.Details.Id, 
                anime.Details, 
                MAL_NODE_FIELD_TYPES
            );
        }
        
        return animeList;
    }

    public async Task RemoveFromUserListAsync(int animeId)
    {
        await GetCurrentProvider().RemoveFromUserListAsync(animeId);
        
        // Update cache to reflect removal
        AnimeDetails anime = await GetAllFieldsAsync(animeId);
        anime.UserStatus = null;
        _cache.Update(anime.Id, anime, (AnimeField[])Enum.GetValues(typeof(AnimeField)));
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

    public async Task<List<AnimeSearchResult>> SearchAnimeAsync(string query)
    {
        List<AnimeSearchResult> results = await GetCurrentProvider().SearchAnimeAsync(query);
        
        // Update cache with search results
        foreach (AnimeSearchResult result in results)
        {
            _cache.UpdatePartial(
                result.Details.Id,
                result.Details,
                MAL_NODE_FIELD_TYPES
            );
        }
        
        return results;
    }

    public async Task<List<RankingEntry>> GetTopAnimeAsync(RankingCategory category, int limit = 10)
    {
        List<RankingEntry> rankings = await GetCurrentProvider().GetTopAnimeAsync(category, limit);
        
        // Update cache with ranking data
        foreach (RankingEntry entry in rankings)
        {
            _cache.UpdatePartial(
                entry.Details.Id,
                entry.Details,
                MAL_NODE_FIELD_TYPES
            );
        }
        
        return rankings;
    }
}