using System.Globalization;
using Aniki.Models.MAL;
using Avalonia.Data.Converters;

namespace Aniki.Converters;

public class MyListStatusScoreConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || (value is int score && score == 0))
            return "Rate";
        else return (value as MAL_MyListStatus)?.Score.ToString();
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}