using Microsoft.Maui.Storage;

namespace MoleculeEfficienceTracker.Core.Services
{
    public static class UserPreferences
    {
        private const string WeightKey = "user_weight_kg";
        private const double DefaultWeight = 72.0;

        public static double GetWeightKg()
        {
            return Preferences.Get(WeightKey, DefaultWeight);
        }

        public static void SetWeightKg(double weightKg)
        {
            Preferences.Set(WeightKey, weightKg);
        }
    }
}
