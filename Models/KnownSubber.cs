namespace Aniki.Models;

public class KnownSubber
{
    public required string Name { get; init; }
    public required string Color { get; init; }

    public bool IsAll => Name == "All";
}
