using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Aniki.Converters;

internal sealed class HexToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
            return Brush.Parse(hex);

        return Brush.Parse("#E0E0E0");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
