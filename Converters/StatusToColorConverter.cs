using System.Globalization;
using Aniki.Misc;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Aniki.Converters;

public class StatusToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not AnimeStatusApi status)
            return new SolidColorBrush(Color.Parse("#3A3A3A"));
        
        return status switch
        {
            AnimeStatusApi.watching => new SolidColorBrush(Color.Parse("#4CAF50")), // Green
            AnimeStatusApi.completed => new SolidColorBrush(Color.Parse("#2196F3")), // Blue
            AnimeStatusApi.on_hold => new SolidColorBrush(Color.Parse("#FF9800")), // Orange
            AnimeStatusApi.dropped => new SolidColorBrush(Color.Parse("#F44336")), // Red
            AnimeStatusApi.plan_to_watch => new SolidColorBrush(Color.Parse("#9C27B0")), // Purple
            _ => new SolidColorBrush(Color.Parse("#3A3A3A"))
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}