using Microsoft.Maui.Controls;
using System;
using System.Globalization;

namespace MoleculeEfficienceTracker.Converters
{
    public class VariationProgressConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double percent && !double.IsNaN(percent))
            {
                percent = Math.Abs(percent);
                return Math.Min(percent / 100.0, 1.0);
            }
            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
