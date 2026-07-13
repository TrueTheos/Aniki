namespace Aniki.Models.MAL.Components;

internal sealed class MAL_AnimeSearchListResponse
{
    public required MAL_SearchEntry[] Data { get; set; }
    public MAL_Paging? Paging { get; set; }
}