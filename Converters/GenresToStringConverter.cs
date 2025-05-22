using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aniki.Converters
{
    public class GenresToStringConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string genreString && !string.IsNullOrEmpty(genreString))
            {
                return genreString;
            }

            if (value is IEnumerable<string> genres)
            {
                return string.Join(", ", genres.Take(2));
            }

            return "";
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
