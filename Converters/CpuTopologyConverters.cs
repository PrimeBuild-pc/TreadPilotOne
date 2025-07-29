using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ThreadPilot.Models;

namespace ThreadPilot.Converters
{
    /// <summary>
    /// Converter for CPU core type to color
    /// </summary>
    public class CoreTypeToColorConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2) return System.Windows.Media.Brushes.Black;

            var coreType = values[0] as CpuCoreType? ?? CpuCoreType.Unknown;
            var isHyperThreaded = values[1] as bool? ?? false;

            return coreType switch
            {
                CpuCoreType.PerformanceCore => isHyperThreaded ? System.Windows.Media.Brushes.DarkBlue : System.Windows.Media.Brushes.Blue,
                CpuCoreType.EfficiencyCore => isHyperThreaded ? System.Windows.Media.Brushes.DarkGreen : System.Windows.Media.Brushes.Green,
                CpuCoreType.Zen or CpuCoreType.ZenPlus or CpuCoreType.Zen2 or CpuCoreType.Zen3 or CpuCoreType.Zen4 =>
                    isHyperThreaded ? System.Windows.Media.Brushes.DarkRed : System.Windows.Media.Brushes.Red,
                _ => isHyperThreaded ? System.Windows.Media.Brushes.DarkGray : System.Windows.Media.Brushes.Black
            };
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converter for boolean to color (success/failure indication)
    /// </summary>
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool success)
            {
                return success ? System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.Red;
            }
            return System.Windows.Media.Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converter for boolean to visibility
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool visible)
            {
                return visible ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            }
            return System.Windows.Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converter for affinity mask to readable string
    /// </summary>
    public class AffinityMaskConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long mask)
            {
                if (mask == 0) return "None";
                
                var cores = new System.Collections.Generic.List<int>();
                for (int i = 0; i < 64; i++)
                {
                    if ((mask & (1L << i)) != 0)
                    {
                        cores.Add(i);
                    }
                }
                
                if (cores.Count == 0) return "None";
                if (cores.Count == 1) return $"Core {cores[0]}";
                if (cores.Count <= 4) return $"Cores {string.Join(", ", cores)}";
                
                return $"Cores {cores[0]}-{cores[cores.Count - 1]} ({cores.Count} cores)";
            }
            return "Unknown";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converter for bytes to megabytes
    /// </summary>
    public class BytesToMbConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long bytes)
            {
                return (bytes / (1024.0 * 1024.0)).ToString("F1");
            }
            return "0.0";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converter for string to visibility (empty/null = collapsed)
    /// </summary>
    public class StringToVisibilityConverter : IValueConverter
    {
        public static readonly StringToVisibilityConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.IsNullOrEmpty(value as string) ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
