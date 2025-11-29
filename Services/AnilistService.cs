using System.Net.Http.Headers;
using Aniki.Models.Anilist;
using Aniki.Services.Interfaces;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;

namespace Aniki.Services;

public class AnilistService : IAnilistService
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

        var response = await _client.SendQueryAsync<dynamic>(request);

        if (response.Data == null) return null;
        
        return new Anilist_ViewerData
        {
            Id = response.Data.Viewer.id,
            Name = response.Data.Viewer.name,
            Picture = response.Data.Viewer.avatar.large
        };
    }
}