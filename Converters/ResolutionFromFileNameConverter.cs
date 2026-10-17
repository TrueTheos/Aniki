using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia.Data.Converters;

namespace Aniki.Converters;

internal sealed partial class ResolutionFromFileNameConverter : IValueConverter
{
    [GeneratedRegex(@"\b(\d{3,4}p)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Resolution();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string name) return null;
        Match m = Resolution().Match(name);
        return m.Success ? m.Groups[1].Value.ToUpperInvariant() : null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
