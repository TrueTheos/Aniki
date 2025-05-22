using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aniki.Converters
{
    public class TimeUntilConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is DateTime airingTime)
            {
                var now = DateTime.Now;
                var timeUntil = airingTime - now;

                if (timeUntil.TotalMinutes <= 0)
                {
                    return "Aired";
                }

                if (timeUntil.TotalDays >= 1)
                {
                    return $"{(int)timeUntil.TotalDays}d {timeUntil.Hours}h";
                }

                if (timeUntil.TotalHours >= 1)
                {
                    return $"{(int)timeUntil.TotalHours}h {timeUntil.Minutes}m";
                }

                return $"{timeUntil.Minutes}m";
            }

            return "";
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
