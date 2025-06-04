using Microsoft.Maui.Controls;
using System;
using System.Globalization;

namespace MoleculeEfficienceTracker.Converters
{
    public class DoseLabelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string unit)
            {
                return $"Dose ({unit}):";
            }
            return "Dose:"; // Fallback
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}