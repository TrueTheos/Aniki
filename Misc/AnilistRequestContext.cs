namespace Aniki.Misc;

internal static class AnilistRequestContext
{
    private static readonly AsyncLocal<string?> Current = new();

    public static string? Operation
    {
        get => Current.Value;
        set => Current.Value = value;
    }
}
