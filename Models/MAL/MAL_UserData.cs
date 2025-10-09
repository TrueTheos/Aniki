using System.Text.Json.Serialization;

namespace Aniki.Models;

public class MAL_UserData
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Picture { get; set; }
}
