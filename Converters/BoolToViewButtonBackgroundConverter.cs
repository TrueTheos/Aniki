using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Aniki.Converters;

public class BoolToViewButtonBackgroundConverter : IValueConverter
{
    private static readonly SolidColorBrush InactiveBrush = new(Color.Parse("#2A2A2A"));
    private static readonly SolidColorBrush ActiveBrush = new(Color.Parse("#DC143C"));
    
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool isActive)
            return InactiveBrush;
        
        bool invert = parameter?.ToString() == "Inverted";
        
        if (invert)
            isActive = !isActive;

        if (!isActive)
            return InactiveBrush;

        if (Application.Current is { } app &&
            app.TryGetResource("AccentRed", app.ActualThemeVariant, out object? resource) &&
            resource is ISolidColorBrush brush)
        {
            return brush;
        }
        
        return ActiveBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
