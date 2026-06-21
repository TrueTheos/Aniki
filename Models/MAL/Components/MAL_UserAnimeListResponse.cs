namespace Aniki.Models.MAL.Components;

public class MAL_UserAnimeListResponse
{
    public required MAL_AnimeNode[] Data { get; set; }
    public MAL_Paging? Paging { get; set; }
}