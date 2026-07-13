namespace Aniki.Models.Anilist.Components;

internal sealed class ViewerResponse
{
    public AnilistViewer? Viewer { get; set; }
}

internal sealed class AnilistViewer
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public AnilistAvatar? Avatar { get; set; }
}

internal sealed class AnilistAvatar
{
    public string? Large { get; set; }
}
