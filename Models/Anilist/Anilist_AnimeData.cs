namespace Aniki.Models.Anilist;

public class MediaListCollectionResponse
{
    public MediaListCollection? MediaListCollection { get; set; }
}

public class MediaListCollection
{
    public List<MediaList>? Lists { get; set; }
}

public class MediaListResponse
{
    public MediaListEntry MediaList { get; set; } = null!;
}

public class MediaList
{
    public List<MediaListEntry>? Entries { get; set; }
}

public class MediaListEntry
{
    public int Id { get; set; }
    public string? Status { get; set; }
    public int? Score { get; set; }
    public int? Progress { get; set; }
    public AnilistMedia Media { get; set; } = null!;
}

public class MediaResponse
{
    public AnilistMedia? Media { get; set; }
}

public class PageResponse
{
    public AnilistPage? Page { get; set; }
}

public class AnilistPage
{
    public List<AnilistMedia>? Media { get; set; }
}

public class AnilistMedia
{
    public int Id { get; set; }
    public AnilistTitle? Title { get; set; }
    public AnilistCoverImage? CoverImage { get; set; }
    public string? Status { get; set; }
    public string? Description { get; set; }
    public int? Episodes { get; set; }
    public int? MeanScore { get; set; }
    public int? Popularity { get; set; }
    public AnilistStudios? Studios { get; set; }
    public AnilistDate? StartDate { get; set; }
    public List<string>? Genres { get; set; }
    public int? Favourites { get; set; }
    public AnilistTrailer? Trailer { get; set; }
    public AnilistRelations? Relations { get; set; }
    public AnilistMediaListStatus? MediaListEntry { get; set; }
    public AnilistStats? Stats { get; set; }
}

public class AnilistMediaListStatus
{
    public string? Status { get; set; }
    public float? Score { get; set; }
    public int? Progress { get; set; }
}

public class AnilistStats
{
    public List<AnilistStatusDistribution>? StatusDistribution { get; set; }
}

public class AnilistStatusDistribution
{
    public string Status { get; set; } = "";
    public int Amount { get; set; }
}

public class AnilistTitle
{
    public string? Romaji { get; set; }
    public string? English { get; set; }
    public string? Native { get; set; }
}

public class AnilistCoverImage
{
    public string? Large { get; set; }
    public string? Medium { get; set; }
}

public class AnilistStudios
{
    public List<AnilistStudio>? Nodes { get; set; }
}

public class AnilistStudio
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public class AnilistDate
{
    public int? Year { get; set; }
    public int? Month { get; set; }
    public int? Day { get; set; }
}

public class AnilistTrailer
{
    public string? Id { get; set; }
    public string? Site { get; set; }
    public string? Thumbnail { get; set; }
}

public class AnilistRelations
{
    public List<AnilistRelationEdge>? Edges { get; set; }
}

public class AnilistRelationEdge
{
    public string? RelationType { get; set; }
    public AnilistMedia? Node { get; set; }
}