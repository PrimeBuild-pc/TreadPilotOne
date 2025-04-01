using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ThreadPilot.Helpers
{
    public class BytesToMbConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long bytes)
            {
                return Math.Round((double)bytes / (1024 * 1024), 1);
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class AffinityMaskConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long mask)
            {
                var cores = new System.Text.StringBuilder();
                for (int i = 0; i < 16; i++)
                {
                    if ((mask & (1L << i)) != 0)
                    {
                        if (cores.Length > 0) cores.Append(", ");
                        cores.Append(i);
                    }
                }
                return $"CPU {cores}";
            }
            return "Unknown";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToFontWeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? FontWeights.Bold : FontWeights.Normal;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}