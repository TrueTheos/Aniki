using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Aniki.Converters;

public class BoolToViewButtonBackgroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool isActive)
            return new SolidColorBrush(Color.Parse("#2A2A2A"));
        
        bool invert = parameter?.ToString() == "Inverted";
        
        if (invert)
            isActive = !isActive;
        
        return isActive 
            ? new SolidColorBrush(Color.Parse("#E50914")) 
            : new SolidColorBrush(Color.Parse("#2A2A2A"));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}