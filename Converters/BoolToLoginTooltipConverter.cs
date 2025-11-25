using System.Globalization;
using Avalonia.Data.Converters;

namespace Aniki.Converters;

public class BoolToLoginTooltipConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool isLoggedIn && !isLoggedIn 
            ? "You need to be logged in" 
            : null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}