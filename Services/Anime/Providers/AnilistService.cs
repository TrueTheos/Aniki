using System.Net.Http.Headers;
using Aniki.Models.Anilist;
using Aniki.Services.Anime;
using Aniki.Services.Interfaces;
using Avalonia.Media.Imaging;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;

namespace Aniki.Services;

public class AnilistService : IAnimeProvider
{
    private readonly GraphQLHttpClient _client;

    public AnilistService()
    {
        _client = new GraphQLHttpClient("https://graphql.anilist.co", new SystemTextJsonSerializer());
    }

    public void SetToken(string token)
    {
        _client.HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<Anilist_ViewerData?> GetViewerAsync()
    {
        var request = new GraphQLRequest
        {
            Query = @"
                query {
                    Viewer {
                        id
                        name
                        avatar {
                            large
                        }
                    }
                }"
        };

        GraphQLResponse<ViewerResponse> response = await _client.SendQueryAsync<ViewerResponse>(request);

        if (response.Data is null) return null;
        
        return new Anilist_ViewerData
        {
            Id = response.Data.Viewer?.id,
            Name = response.Data.Viewer?.name,
            Picture = response.Data.Viewer?.avatar?.large
        };
    }

    public string ProviderName => "Anilist";
    public bool IsLoggedIn { get; }
    public void Init(string accessToken)
    {
        throw new NotImplementedException();
    }

    public Task<UserData> GetUserDataAsync()
    {
        throw new NotImplementedException();
    }

    public Task<List<AnimeData>> GetUserAnimeListAsync(AnimeStatus status = AnimeStatus.None)
    {
        throw new NotImplementedException();
    }

    public Task RemoveFromUserListAsync(int animeId)
    {
        throw new NotImplementedException();
    }

    public Task SetAnimeStatusAsync(int animeId, AnimeStatus status)
    {
        throw new NotImplementedException();
    }

    public Task SetAnimeScoreAsync(int animeId, int score)
    {
        throw new NotImplementedException();
    }

    public Task SetEpisodesWatchedAsync(int animeId, int episodes)
    {
        throw new NotImplementedException();
    }

    public Task<AnimeDetails?> FetchAnimeDetailsAsync(int animeId, params AnimeField[] fields)
    {
        throw new NotImplementedException();
    }

    public Task<List<AnimeSearchResult>> SearchAnimeAsync(string query)
    {
        throw new NotImplementedException();
    }

    public Task<List<RankingEntry>> GetTopAnimeAsync(RankingCategory category, int limit = 10)
    {
        throw new NotImplementedException();
    }

    public Task<Bitmap?> LoadAnimeImageAsync(int animeId, string? imageUrl)
    {
        throw new NotImplementedException();
    }
}