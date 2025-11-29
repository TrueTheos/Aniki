using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Aniki.Converters;

public class StatusToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not AnimeStatus status)
            return new SolidColorBrush(Color.Parse("#3A3A3A"));
        
        return status switch
        {
            AnimeStatus.Watching => new SolidColorBrush(Color.Parse("#4CAF50")),
            AnimeStatus.Completed => new SolidColorBrush(Color.Parse("#2196F3")),
            AnimeStatus.OnHold => new SolidColorBrush(Color.Parse("#FF9800")),
            AnimeStatus.Dropped => new SolidColorBrush(Color.Parse("#F44336")),
            AnimeStatus.PlanToWatch => new SolidColorBrush(Color.Parse("#9C27B0")),
            _ => new SolidColorBrush(Color.Parse("#3A3A3A"))
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}