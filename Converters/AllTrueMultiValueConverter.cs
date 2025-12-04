using System.Globalization;
using Avalonia.Data.Converters;

namespace Aniki.Converters;

public class AllTrueMultiValueConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values == null || values.Count == 0)
            return false;

        foreach (object? value in values)
        {
            if (value == Avalonia.Data.BindingOperations.DoNothing ||
                value == Avalonia.AvaloniaProperty.UnsetValue ||
                value == null)
            {
                return false;
            }

            if (value is bool boolValue)
            {
                if (!boolValue)
                    return false;
            }
            else
            {
                if (value is string str && bool.TryParse(str, out bool parsed))
                {
                    if (!parsed)
                        return false;
                }
                else
                {
                    return false;
                }
            }
        }

        return true;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException("ConvertBack is not supported for AllTrueMultiValueConverter");
    }
}