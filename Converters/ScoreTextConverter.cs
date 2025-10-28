using System.Globalization;
using Avalonia.Data.Converters;

namespace Aniki.Converters;

public class ScoreTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || (value is int score && score == 0))
            return "Rate";
        if (value != null) return value.ToString();
        return "";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}