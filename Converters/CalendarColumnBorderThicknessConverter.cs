using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace Aniki.Converters;

public class CalendarColumnBorderThicknessConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        int index = value is int i ? i : 0;
        return index == 0
            ? new Thickness(1, 0, 1, 0)
            : new Thickness(0, 0, 1, 0);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
