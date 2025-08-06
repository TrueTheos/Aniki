using Avalonia.Data.Converters;
using System.Globalization;

namespace Aniki.Converters;

public class SeederColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int seeders)
        {
            return seeders switch
            {
                0 => "#FF5252",
                < 5 => "#FFA726",
                < 20 => "#FFEB3B",
                _ => "#66BB6A"
            };
        }
        return "#E0E0E0"; // Default gray
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}