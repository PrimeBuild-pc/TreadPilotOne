using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ThreadPilot
{
    public class BoolToFontWeightConverter : IValueConverter
    {
        public static readonly BoolToFontWeightConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && boolValue)
            {
                return FontWeights.Bold;
            }
            return FontWeights.Normal;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
