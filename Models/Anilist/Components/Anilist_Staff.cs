namespace Aniki.Models.Anilist.Components;

internal sealed class Anilist_Staff
{
    public List<Anilist_StaffNode>? Nodes { get; set; }
}

internal sealed class Anilist_StaffNode
{
    public Anilist_StaffName? Name { get; set; }
}

internal sealed class Anilist_StaffName
{
    public string? Full { get; set; }
}
