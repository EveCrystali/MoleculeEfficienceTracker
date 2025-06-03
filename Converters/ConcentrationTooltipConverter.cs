using Microsoft.Maui.Controls;
using System;
using System.Globalization;

namespace MoleculeEfficienceTracker.Converters
{
    public class ConcentrationTooltipConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double concentration && parameter is string unit)
            {
                return $"📈 {concentration:F2} {unit}";
            }
            return value; // Fallback
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}