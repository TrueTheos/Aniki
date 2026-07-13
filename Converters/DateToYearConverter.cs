using System.Globalization;
using Avalonia.Data.Converters;

namespace Aniki.Converters;

internal sealed class DateToYearConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string dateString || string.IsNullOrEmpty(dateString))
            return "N/A";
        
        if (DateTime.TryParse(dateString, out DateTime date))
        {
            return date.Year.ToString(CultureInfo.InvariantCulture);
        }
        
        return dateString.Length >= 4 ? dateString[..4] : "N/A";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}