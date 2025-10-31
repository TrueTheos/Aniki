using System.Globalization;
using Avalonia.Data.Converters;

namespace Aniki.Converters;

public class DateToYearConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string dateString || string.IsNullOrEmpty(dateString))
            return "N/A";
        
        if (DateTime.TryParse(dateString, out var date))
        {
            return date.Year.ToString();
        }
        
        if (dateString.Length >= 4)
        {
            return dateString.Substring(0, 4);
        }
        
        return "N/A";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}