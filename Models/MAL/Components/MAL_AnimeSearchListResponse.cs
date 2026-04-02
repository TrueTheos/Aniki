namespace Aniki.Models.MAL;

public class MAL_AnimeSearchListResponse
{
    public required MAL_SearchEntry[] Data { get; set; }
    public MAL_Paging? Paging { get; set; }
}