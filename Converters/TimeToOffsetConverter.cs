using Avalonia.Data.Converters;
using System.Globalization;

namespace Aniki.Converters;

public class TimeToOffsetConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateTime dateTime)
        {
            // Calculate minutes from midnight
            int minutesFromMidnight = dateTime.Hour * 60 + dateTime.Minute;

            // Convert to pixel offset (1440px = 24 hours, so 1px = 1 minute)
            const double pixelsPerMinute = 1440.0 / (24 * 60);
            return minutesFromMidnight * pixelsPerMinute;
        }

        return 0.0;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}