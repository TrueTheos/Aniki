using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Aniki.Converters
{
    public class AnimeTypeToColorConverter : IValueConverter
    {
        private static readonly Dictionary<string, string> TypeColors = new()
        {
            { "TV", "#0078D4" },
            { "MOVIE", "#FF6B35" },
            { "OVA", "#9B59B6" },
            { "ONA", "#E74C3C" },
            { "SPECIAL", "#F39C12" },
            { "MUSIC", "#1ABC9C" },
            { "MANGA", "#95A5A6" }
        };

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string animeType && TypeColors.TryGetValue(animeType.ToUpper(), out string? color))
            {
                return Brush.Parse(color);
            }

            // Default color
            return Brush.Parse("#666666");
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
