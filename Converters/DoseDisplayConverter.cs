using Microsoft.Maui.Controls;
using System;
using System.Globalization;

namespace MoleculeEfficienceTracker.Converters
{
    public class DoseDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double dose && parameter is string unit)
            {
                // Format the dose value and append the unit
                // Using F1 for dose, adjust format as needed (e.g., F0 if only whole units)
                return $"{dose:F1}{unit}";
            }
            return value; // Fallback
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}