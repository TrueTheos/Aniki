namespace Aniki.Models.Anilist;

public class ViewerResponse
{
    public AnilistViewer? Viewer { get; set; }
}

public class AnilistViewer
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public AnilistAvatar? Avatar { get; set; }
}

public class AnilistAvatar
{
    public string? Large { get; set; }
}
