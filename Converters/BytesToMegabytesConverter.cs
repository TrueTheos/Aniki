using System.Globalization;
using Avalonia.Data.Converters;

namespace Aniki.Converters;

public class BytesToMegabytesConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is long bytes)
        {
            double megabytes = (double)bytes / (1024 * 1024);
            return $"{megabytes:F2} MB";
        }
        return "0.00 MB";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}