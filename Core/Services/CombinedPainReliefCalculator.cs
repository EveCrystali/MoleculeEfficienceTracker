using System;
using System.Collections.Generic;
using System.Linq;
using MoleculeEfficienceTracker.Core.Models;

namespace MoleculeEfficienceTracker.Core.Services
{
    /// <summary>
    /// Effet analgésique combiné du paracétamol et de l'ibuprofène, sur une échelle
    /// de 0 à 100 %.
    ///
    /// L'ancienne version additionnait deux pourcentages d'effet déjà saturants,
    /// puis divisait par un maximum mesuré au lancement : le total pouvait grimper
    /// à 156 % avant normalisation, et la constante de normalisation dépendait de
    /// l'instant d'instanciation. La combinaison suit désormais l'indépendance de
    /// Bliss — deux mécanismes distincts, l'un central, l'autre périphérique :
    ///
    ///     E = 1 − (1 − E_para)(1 − E_ibu)
    ///
    /// bornée à 100 % par construction, sans facteur d'échelle arbitraire.
    /// </summary>
    public class CombinedPainReliefCalculator : IMoleculeCalculator
    {
        public string DisplayName => "Antidouleur";
        public string DoseUnit => DoseUnits.Milligram;
        public string ConcentrationUnit => "%";

        private readonly ParacetamolCalculator _paraCalc = new();
        private readonly IbuprofeneCalculator _ibuCalc = new();

        // EC50 posée sur le seuil « effet net » de chaque molécule : à ce seuil,
        // l'effet vaut exactement 50 %, ce qui rend l'échelle lisible.
        private readonly PharmacodynamicModel _paraPd = new(ParacetamolCalculator.MODERATE_THRESHOLD);
        private readonly PharmacodynamicModel _ibuPd = new(IbuprofeneCalculator.MODERATE_THRESHOLD);

        public double StrongPercent { get; }
        public double ModeratePercent { get; }
        public double LightPercent { get; }
        public double NegligibleEffect { get; }

        public CombinedPainReliefCalculator()
        {
            StrongPercent = ThresholdPercent(ParacetamolCalculator.STRONG_THRESHOLD, IbuprofeneCalculator.STRONG_THRESHOLD);
            ModeratePercent = ThresholdPercent(ParacetamolCalculator.MODERATE_THRESHOLD, IbuprofeneCalculator.MODERATE_THRESHOLD);
            LightPercent = ThresholdPercent(ParacetamolCalculator.LIGHT_THRESHOLD, IbuprofeneCalculator.LIGHT_THRESHOLD);
            NegligibleEffect = ThresholdPercent(ParacetamolCalculator.NEGLIGIBLE_THRESHOLD, IbuprofeneCalculator.NEGLIGIBLE_THRESHOLD);
        }

        /// <summary>Niveau atteint lorsque l'une des deux molécules seule est à son seuil.</summary>
        private double ThresholdPercent(double paraConcentration, double ibuConcentration)
            => 100.0 * Math.Max(_paraPd.GetEffectFraction(paraConcentration),
                                _ibuPd.GetEffectFraction(ibuConcentration));

        private static bool IsParacetamol(DoseEntry d) =>
            string.Equals(d.MoleculeKey, MoleculeKeys.Paracetamol, StringComparison.OrdinalIgnoreCase);

        private static bool IsIbuprofen(DoseEntry d) =>
            string.Equals(d.MoleculeKey, MoleculeKeys.Ibuprofen, StringComparison.OrdinalIgnoreCase);

        private static double Combine(double fractionPara, double fractionIbu)
            => 100.0 * (1.0 - (1.0 - fractionPara) * (1.0 - fractionIbu));

        public double CalculateSingleDoseConcentration(DoseEntry dose, DateTime time)
        {
            if (IsParacetamol(dose))
                return _paraPd.GetEffectPercent(_paraCalc.CalculateSingleDoseConcentration(dose, time));

            if (IsIbuprofen(dose))
                return _ibuPd.GetEffectPercent(_ibuCalc.CalculateSingleDoseConcentration(dose, time));

            return 0;
        }

        public double CalculateTotalConcentration(List<DoseEntry> doses, DateTime time)
        {
            List<DoseEntry> para = doses.Where(IsParacetamol).ToList();
            List<DoseEntry> ibu = doses.Where(IsIbuprofen).ToList();
            return CombinedPercent(para, ibu, time);
        }

        private double CombinedPercent(List<DoseEntry> para, List<DoseEntry> ibu, DateTime time)
            => Combine(_paraPd.GetEffectFraction(_paraCalc.CalculateTotalConcentration(para, time)),
                       _ibuPd.GetEffectFraction(_ibuCalc.CalculateTotalConcentration(ibu, time)));

        public double CalculateTotalAmount(List<DoseEntry> doses, DateTime time)
            => CalculateTotalConcentration(doses, time);

        public double GetDoseDisplayValueInConcentrationUnit(DoseEntry dose) => dose.DoseMg;

        public List<(DateTime Time, double Concentration)> GenerateGraph(
            List<DoseEntry> doses, DateTime startTime, DateTime endTime, int points = 200)
        {
            // Le tri des doses est fait une fois, et non à chaque point comme
            // auparavant — ce qui allouait deux listes par point tracé.
            List<DoseEntry> para = doses.Where(IsParacetamol).ToList();
            List<DoseEntry> ibu = doses.Where(IsIbuprofen).ToList();

            double interval = (endTime - startTime).TotalMinutes / points;
            var list = new List<(DateTime, double)>(points + 1);

            for (int i = 0; i <= points; i++)
            {
                DateTime t = startTime.AddMinutes(i * interval);
                list.Add((t, CombinedPercent(para, ibu, t)));
            }

            return list;
        }

        public (List<(DateTime Time, double EffectPara)>,
                List<(DateTime Time, double EffectIbu)>,
                List<(DateTime Time, double EffectTotal)>) GenerateEffectGraph(
            List<DoseEntry> doses, DateTime startTime, DateTime endTime, int points = 200)
        {
            List<DoseEntry> para = doses.Where(IsParacetamol).ToList();
            List<DoseEntry> ibu = doses.Where(IsIbuprofen).ToList();

            double interval = (endTime - startTime).TotalMinutes / points;
            var paraList = new List<(DateTime, double)>(points + 1);
            var ibuList = new List<(DateTime, double)>(points + 1);
            var totalList = new List<(DateTime, double)>(points + 1);

            for (int i = 0; i <= points; i++)
            {
                DateTime t = startTime.AddMinutes(i * interval);
                double fPara = _paraPd.GetEffectFraction(_paraCalc.CalculateTotalConcentration(para, t));
                double fIbu = _ibuPd.GetEffectFraction(_ibuCalc.CalculateTotalConcentration(ibu, t));

                paraList.Add((t, 100.0 * fPara));
                ibuList.Add((t, 100.0 * fIbu));
                totalList.Add((t, Combine(fPara, fIbu)));
            }

            return (paraList, ibuList, totalList);
        }

        public EffectLevel GetCombinedEffectLevel(List<DoseEntry> doses, DateTime time)
        {
            EffectLevel levelPara = _paraCalc.GetEffectLevel(
                _paraCalc.CalculateTotalConcentration(doses.Where(IsParacetamol).ToList(), time));
            EffectLevel levelIbu = _ibuCalc.GetEffectLevel(
                _ibuCalc.CalculateTotalConcentration(doses.Where(IsIbuprofen).ToList(), time));

            return (EffectLevel)Math.Max((int)levelPara, (int)levelIbu);
        }

        public DateTime? PredictEffectEndTime(List<DoseEntry> doses, DateTime currentTime)
        {
            if (!doses.Any()) return currentTime;

            List<DoseEntry> para = doses.Where(IsParacetamol).ToList();
            List<DoseEntry> ibu = doses.Where(IsIbuprofen).ToList();

            for (int minutes = 0; minutes <= 24 * 60; minutes += 15)
            {
                DateTime t = currentTime.AddMinutes(minutes);
                if (_paraCalc.IsEffectNegligible(_paraCalc.CalculateTotalConcentration(para, t)) &&
                    _ibuCalc.IsEffectNegligible(_ibuCalc.CalculateTotalConcentration(ibu, t)))
                    return t;
            }

            return null;
        }
    }
}
