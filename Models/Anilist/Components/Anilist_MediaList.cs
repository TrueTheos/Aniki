namespace Aniki.Models.Anilist.Components;

internal sealed class Anilist_MediaList
{
    public bool IsCustomList { get; set; }
    public List<Anilist_MediaListEntry>? Entries { get; set; }
}
