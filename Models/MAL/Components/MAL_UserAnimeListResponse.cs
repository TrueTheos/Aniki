namespace Aniki.Models.MAL.Components;

internal sealed class MAL_UserAnimeListResponse
{
    public required MAL_AnimeNode[] Data { get; set; }
    public MAL_Paging? Paging { get; set; }
}