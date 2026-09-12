using System.Globalization;

namespace MoleculeEfficienceTracker.Converters
{
    /// <summary>Vrai si la chaîne porte quelque chose. Sert à masquer les lignes vides.</summary>
    public class IsNotEmptyConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is string s && !string.IsNullOrWhiteSpace(s);

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
