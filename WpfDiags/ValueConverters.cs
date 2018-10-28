using System;
using System.Globalization;
using System.Windows.Data;
using KaosFormat;

namespace AppView
{
    public class ComparisonConverter : IValueConverter
    {
        public object Convert (object value, Type targetType, object param, System.Globalization.CultureInfo culture)
         => value.Equals (param);

        public object ConvertBack (object value, Type targetType, object param, System.Globalization.CultureInfo culture)
         => value.Equals (true) ? param : Binding.DoNothing;
    }

    public class HashToggle : IValueConverter
    {
        public object Convert (object value, Type targetType, object param, System.Globalization.CultureInfo culture)
         => ((int) value & (int) param) != 0;

        public object ConvertBack (object value, Type targetType, object param, System.Globalization.CultureInfo culture)
         => value.Equals (true) ? (Hashes) param : (Hashes) ~ (int) param;
    }

    public class ValidationToggle : IValueConverter
    {
        public object Convert (object value, Type targetType, object param, System.Globalization.CultureInfo culture)
         => ((int) value & (int) param) != 0;

        public object ConvertBack (object value, Type targetType, object param, System.Globalization.CultureInfo culture)
         => value.Equals (true) ? (Validations) param : (Validations) ~(int) param;
    }

    public class DataTypeConverter : IValueConverter
    {
        public object Convert (object value, Type targetType, object parameter, CultureInfo culture)
         => value?.GetType();

        public object ConvertBack (object value, Type targetType, object parameter, CultureInfo culture)
         => throw new NotImplementedException();
    }
}
