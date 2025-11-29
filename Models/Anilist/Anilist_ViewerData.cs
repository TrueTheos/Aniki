namespace Aniki.Models.Anilist;

public class Anilist_ViewerData
{
    public int? Id { get; set; }
    public string? Name { get; set; }
    public string? Picture { get; set; }
}

public class ViewerResponse
{
    public ViewerData? Viewer { get; set; }
}

public class ViewerData
{
    public int id { get; set; }
    public string? name { get; set; }
    public AvatarData? avatar { get; set; }
}

public class AvatarData
{
    public string? large { get; set; }
}
