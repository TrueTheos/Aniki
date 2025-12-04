using System.Globalization;
using Avalonia.Data.Converters;

namespace Aniki.Converters;

public class StringToFormattedIntConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && int.TryParse(s, out int result))
        {
            return result.ToString("N0", culture);
        }
        return "0";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
