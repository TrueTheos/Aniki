namespace Aniki.Models.Anilist;

public class ViewerResponse
{
    public AnilistViewer? Viewer { get; set; }
}

public class AnilistViewer
{
    public int id { get; set; }
    public string? name { get; set; }
    public AnilistAvatar? avatar { get; set; }
}

public class AnilistAvatar
{
    public string? large { get; set; }
}
