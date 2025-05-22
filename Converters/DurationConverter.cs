using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aniki.Converters
{
    public class DurationConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int minutes)
            {
                if (minutes >= 60)
                {
                    var hours = minutes / 60;
                    var remainingMinutes = minutes % 60;
                    return remainingMinutes > 0 ? $"{hours}h {remainingMinutes}m" : $"{hours}h";
                }

                return $"{minutes}m";
            }

            return "";
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
