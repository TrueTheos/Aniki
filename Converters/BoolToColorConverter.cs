using Avalonia.Data.Converters;
using Avalonia.Media;
using System.Globalization;

namespace Aniki.Converters;

public class BoolToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue && parameter is string colorString)
        {
            var colors = colorString.Split(':');
            if (colors.Length != 2) return Brushes.Transparent;
            
            var trueColor = colors[0];
            var falseColor = colors[1];
            
            var selectedColor = boolValue ? trueColor : falseColor;
            
            return Brush.Parse(selectedColor);
        }
        
        return Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}