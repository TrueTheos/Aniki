namespace Aniki.Models.Anilist;

public class MediaListCollectionResponse
{
    public MediaListCollection? MediaListCollection { get; set; }
}

public class MediaListCollection
{
    public List<MediaList>? lists { get; set; }
}

public class MediaList
{
    public List<MediaListEntry>? entries { get; set; }
}

public class MediaListEntry
{
    public int id { get; set; }
    public string? status { get; set; }
    public int? score { get; set; }
    public int? progress { get; set; }
    public AnilistMedia media { get; set; } = null!;
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
    public List<AnilistMedia>? media { get; set; }
}

public class AnilistMedia
{
    public int id { get; set; }
    public AnilistTitle? title { get; set; }
    public AnilistCoverImage? coverImage { get; set; }
    public string? status { get; set; }
    public string? description { get; set; }
    public int? episodes { get; set; }
    public int? meanScore { get; set; }
    public int? popularity { get; set; }
    public AnilistStudios? studios { get; set; }
    public AnilistDate? startDate { get; set; }
    public List<string>? genres { get; set; }
    public int? favourites { get; set; }
    public AnilistTrailer? trailer { get; set; }
    public AnilistRelations? relations { get; set; }
    public AnilistMediaListStatus? mediaListEntry { get; set; }
    public AnilistStats? stats { get; set; }
}

public class AnilistMediaListStatus
{
    public string? status { get; set; }
    public float? score { get; set; }
    public int? progress { get; set; }
}

public class AnilistStats
{
    public List<AnilistStatusDistribution>? statusDistribution { get; set; }
}

public class AnilistStatusDistribution
{
    public string status { get; set; } = "";
    public int amount { get; set; }
}

public class AnilistTitle
{
    public string? romaji { get; set; }
    public string? english { get; set; }
    public string? native { get; set; }
}

public class AnilistCoverImage
{
    public string? large { get; set; }
    public string? medium { get; set; }
}

public class AnilistStudios
{
    public List<AnilistStudio>? nodes { get; set; }
}

public class AnilistStudio
{
    public int id { get; set; }
    public string name { get; set; } = "";
}

public class AnilistDate
{
    public int? year { get; set; }
    public int? month { get; set; }
    public int? day { get; set; }
}

public class AnilistTrailer
{
    public string? id { get; set; }
    public string? site { get; set; }
}

public class AnilistRelations
{
    public List<AnilistRelationEdge>? edges { get; set; }
}

public class AnilistRelationEdge
{
    public string? relationType { get; set; }
    public AnilistMedia? node { get; set; }
}