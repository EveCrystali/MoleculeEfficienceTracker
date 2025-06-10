using Microsoft.Maui.Storage;

namespace MoleculeEfficienceTracker.Core.Services
{
    public static class UserPreferences
    {
        private const string WeightKey = "user_weight_kg";
        private const double DefaultWeight = 72.0;
        private const string SexKey = "user_sex";
        private const string DefaultSex = "homme";

        public static double GetWeightKg()
        {
            return Preferences.Get(WeightKey, DefaultWeight);
        }

        public static void SetWeightKg(double weightKg)
        {
            Preferences.Set(WeightKey, weightKg);
        }

        public static string GetSex()
        {
            return Preferences.Get(SexKey, DefaultSex);
        }

         public static void SetSex(string sex)
        {
            Preferences.Set(SexKey, sex);
        }

    }
}
