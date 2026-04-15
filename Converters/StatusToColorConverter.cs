using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Aniki.Converters;

public class StatusToColorConverter : IValueConverter
{
    private static readonly SolidColorBrush DefaultBrush = new(Color.Parse("#3A3A3A"));
    
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not AnimeStatus status)
            return DefaultBrush;

        string key = status switch
        {
            AnimeStatus.Watching => "StatusWatching",
            AnimeStatus.Completed => "StatusCompleted",
            AnimeStatus.OnHold => "StatusOnHold",
            AnimeStatus.Dropped => "StatusDropped",
            AnimeStatus.PlanToWatch => "StatusPlanToWatch",
            _ => ""
        };

        if (!string.IsNullOrEmpty(key) &&
            Application.Current is { } app &&
            app.TryGetResource(key, app.ActualThemeVariant, out object? resource) &&
            resource is ISolidColorBrush brush)
        {
            return brush;
        }

        return DefaultBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
