using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SpaceBusinessPOS.Converters
{
    // Converts a string value to bool for RadioButton IsChecked
    public class StringEqualConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value?.ToString() == parameter?.ToString();

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => (bool)value ? parameter?.ToString() ?? "" : Binding.DoNothing;
    }

    // Bool to Visibility
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (bool)value ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => (Visibility)value == Visibility.Visible;
    }

    // Inverse Bool to Visibility
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (bool)value ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => (Visibility)value == Visibility.Collapsed;
    }

    // Not bool
    public class NotConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => !(bool)value;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => !(bool)value;
    }

    // Status to color brush
    public class StatusColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value?.ToString() switch
            {
                "completed" => "#22C55E",
                "refunded" => "#EF4444",
                "pending" => "#F59E0B",
                _ => "#9CA3AF"
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    // Profit positive/negative color
    public class ProfitColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal d) return d >= 0 ? "#22C55E" : "#EF4444";
            return "#9CA3AF";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}
