using System.Text.Json.Serialization;

namespace Aniki.Models.MAL.Components;

internal sealed class MAL_Author
{
    public MAL_AuthorNode? Node { get; set; }
}

internal sealed class MAL_AuthorNode
{
    [JsonPropertyName("first_name")] public string? FirstName { get; set; }
    [JsonPropertyName("last_name")] public string? LastName { get; set; }

    public string Name => $"{FirstName} {LastName}".Trim();
}
