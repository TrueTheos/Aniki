using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia.Data.Converters;

namespace Aniki.Converters;

public class YoutubeEmbedUrlToWatchUrlConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string embedUrl)
        {
            var match = Regex.Match(embedUrl, @"embed/([a-zA-Z0-9_-]+)");
            if (match.Success)
            {
                string videoId = match.Groups[1].Value;
                return $"https://www.youtube.com/watch?v={videoId}";
            }
        }

        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}