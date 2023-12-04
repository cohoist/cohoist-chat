using System.Globalization;

namespace CohoistChat.Maui
{
    public class StringToDateTimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stringValue)
            {
                _ = DateTime.TryParse(stringValue, out DateTime dateTimeValue);
                return dateTimeValue;
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            //if (value is DateTime dateTimeValue)
            //{
            //    return dateTimeValue.ToString();
            //}
            //return value;
            return null; //Because this will only be used in one-way bindings
        }
    }
}