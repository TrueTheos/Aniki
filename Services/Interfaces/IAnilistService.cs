using Aniki.Models.Anilist;

namespace Aniki.Services.Interfaces;

public interface IAnilistService
{
    void SetToken(string token);
    Task<Anilist_ViewerData?> GetViewerAsync();
}