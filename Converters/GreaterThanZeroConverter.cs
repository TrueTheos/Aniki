using Avalonia.Data.Converters;
using System.Globalization;

namespace Aniki.Converters;

public class GreaterThanZeroConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int i)
            return i > 0;

        if (value is string s && int.TryParse(s, out int x))
            return x > 0;

        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}