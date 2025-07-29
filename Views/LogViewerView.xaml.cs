using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ThreadPilot.ViewModels;

namespace ThreadPilot.Views
{
    /// <summary>
    /// Interaction logic for LogViewerView.xaml
    /// </summary>
    public partial class LogViewerView : System.Windows.Controls.UserControl
    {
        public LogViewerView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is LogViewerViewModel viewModel)
            {
                await viewModel.InitializeAsync();
            }
        }
    }

    /// <summary>
    /// Converter to convert bytes to megabytes for display
    /// </summary>
    public class BytesToMegabytesConverter : IValueConverter
    {
        public static readonly BytesToMegabytesConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long bytes)
            {
                return bytes / (1024.0 * 1024.0);
            }
            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converter to invert boolean values
    /// </summary>
    public class InverseBooleanConverter : IValueConverter
    {
        public static readonly InverseBooleanConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return false;
        }
    }

    /// <summary>
    /// Converter to invert boolean values for visibility
    /// </summary>
    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public static readonly InverseBooleanToVisibilityConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
