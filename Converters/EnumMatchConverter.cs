using System.Globalization;
using Avalonia.Data.Converters;

namespace Aniki.Converters;

public class EnumMatchConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return false;

        if (parameter is string paramString)
        {
            if (Enum.TryParse(value.GetType(), paramString, true, out object? parsedParam))
            {
                return value.Equals(parsedParam);
            }
            return false;
        }

        return value.Equals(parameter);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isMatch && isMatch && parameter != null)
        {
            if (parameter is string paramString)
            {
                if (Enum.TryParse(targetType, paramString, true, out object? parsedEnum))
                {
                    return parsedEnum;
                }
            }
            
            return parameter;
        }

        return Avalonia.Data.BindingOperations.DoNothing;
    }
}