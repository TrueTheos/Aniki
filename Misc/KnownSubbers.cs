namespace Aniki.Misc;

public static class KnownSubbers
{
    private static readonly string[] SubberNames =
    [
        "ToonsHub",
        "Kaizoku",
        "ASW",
        "DKB",
        "VARYG",
        "Shiniori-Raws",
        "BakeSubs",
        "Tsundere-Raws",
        "KAF",
        "SubsPlease",
        "Erai-raws",
        "HorribleSubs",
        "EMBER",
        "YuiSubs",
        "Commie",
        "gg",
        "Doki",
        "UTW",
        "Chihiro",
        "Hatsuyuki",
        "FFF",
        "Vivid",
        "Mezashite",
        "GJM",
        "Kametsu",
        "Asenshi",
        "Cthuyuu",
        "Nii-sama",
        "TakeSubs",
        "Tenshi",
        "MTBB",
        "LostYears",
        "SCY",
        "GST",
        "Rasetsu",
        "kBaraka",
        "Neonime",
        "YURI",
        "Pog42",
        "Frenchie",
        "AnimeRG",
        "BlurayDesuYo",
        "Hi10",
        "Exiled-Destiny",
        "CoalGuys",
        "Mazui",
        "Hadena",
        "ayako",
        "Coalgirls",
        "Elysium",
        "Beatrice-Raws",
        "SCM",
        "Reaktor",
        "neko-raws",
    ];

    private static readonly (string Name, string Color)[] Subbers = BuildSubbers();

    private const string DEFAULT_COLOR = "#888888";
    public const string REST_OF_FILE_NAME_COLOR = "#E0E0E0";

    public static IReadOnlyList<KnownSubber> All { get; } = BuildList();

    public static string GetColor(string? releaseGroup)
    {
        if (string.IsNullOrWhiteSpace(releaseGroup))
            return DEFAULT_COLOR;

        foreach ((string name, string color) in Subbers)
        {
            if (string.Equals(name, releaseGroup, StringComparison.OrdinalIgnoreCase))
                return color;
        }

        return DEFAULT_COLOR;
    }

    private static (string Name, string Color)[] BuildSubbers()
    {
        int count = SubberNames.Length;
        var result = new (string Name, string Color)[count];

        for (int i = 0; i < count; i++)
            result[i] = (SubberNames[i], ColorFromIndex(i, count));

        return result;
    }

    private static IReadOnlyList<KnownSubber> BuildList()
    {
        var list = new List<KnownSubber>
        {
            new() { Name = "All", Color = REST_OF_FILE_NAME_COLOR }
        };

        foreach ((string name, string color) in Subbers)
            list.Add(new KnownSubber { Name = name, Color = color });

        return list;
    }

    private static string ColorFromIndex(int index, int count)
    {
        double hue = index * 360.0 / count;
        return HslToHex(hue, 0.68, 0.58);
    }

    private static string HslToHex(double h, double s, double l)
    {
        double c = (1 - Math.Abs(2 * l - 1)) * s;
        double x = c * (1 - Math.Abs(h / 60 % 2 - 1));
        double m = l - c / 2;

        double r, g, b;
        switch ((int)(h / 60) % 6)
        {
            case 0: r = c; g = x; b = 0; break;
            case 1: r = x; g = c; b = 0; break;
            case 2: r = 0; g = c; b = x; break;
            case 3: r = 0; g = x; b = c; break;
            case 4: r = x; g = 0; b = c; break;
            default: r = c; g = 0; b = x; break;
        }

        int ri = (int)Math.Round((r + m) * 255);
        int gi = (int)Math.Round((g + m) * 255);
        int bi = (int)Math.Round((b + m) * 255);

        return $"#{ri:X2}{gi:X2}{bi:X2}";
    }
}
