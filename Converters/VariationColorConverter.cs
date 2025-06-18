using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;
using System.Globalization;

namespace MoleculeEfficienceTracker.Converters
{
    public class VariationColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double percent)
            {
                if (double.IsNaN(percent))
                    return Colors.Gray;
                if (percent > 0)
                    return Colors.Red;
                if (percent < 0)
                    return Colors.Green;
                return Colors.Gray;
            }
            return Colors.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
