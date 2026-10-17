using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia.Data.Converters;

namespace Aniki.Converters;

internal sealed partial class YoutubeThumbnailConverter : IValueConverter
{
    [GeneratedRegex(@"(?:embed/|v=|youtu\.be/)([a-zA-Z0-9_-]{6,})")]
    private static partial Regex VideoId();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string url) return null;
        Match m = VideoId().Match(url);
        return m.Success ? $"https://i.ytimg.com/vi/{m.Groups[1].Value}/hqdefault.jpg" : null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
