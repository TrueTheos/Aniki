using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aniki.Converters
{
    public class SeederColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int seeders)
            {
                if (seeders == 0) return "#FF5252"; // Red
                if (seeders < 5) return "#FFA726"; // Orange
                if (seeders < 20) return "#FFEB3B"; // Yellow
                return "#66BB6A"; // Green
            }
            return "#E0E0E0"; // Default gray
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
