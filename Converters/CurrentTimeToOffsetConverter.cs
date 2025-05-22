using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aniki.Converters
{
    public class CurrentTimeToOffsetConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var now = DateTime.Now;
            var minutesFromMidnight = now.Hour * 60 + now.Minute;

            // Convert to pixel offset (1440px = 24 hours)
            const double pixelsPerMinute = 1440.0 / (24 * 60);
            return minutesFromMidnight * pixelsPerMinute;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
