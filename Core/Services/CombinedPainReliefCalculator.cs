using System;
using System.Collections.Generic;
using System.Linq;
using MoleculeEfficienceTracker.Core.Models;

namespace MoleculeEfficienceTracker.Core.Services
{
    /// <summary>
    /// Calculator combining Paracetamol and Ibuprofen effects.
    /// The returned "concentration" represents a normalised effect percentage where
    /// 100 % correspond to the simultaneous peak of 1 g paracetamol and 400 mg ibuprofène.
    /// </summary>
    public class CombinedPainReliefCalculator : IMoleculeCalculator
    {
        public string DisplayName => "Antidouleur";
        public string DoseUnit => "mg";
        public string ConcentrationUnit => "%";

        private readonly ParacetamolCalculator _paraCalc = new();
        private readonly IbuprofeneCalculator _ibuCalc = new();
        // EC50 approximations for an Emax model
        private readonly PharmacodynamicModel _paraPd = new(ParacetamolCalculator.MODERATE_THRESHOLD);
        private readonly PharmacodynamicModel _ibuPd = new(12.0);
        private readonly double _maxCombinedEffect;

        public CombinedPainReliefCalculator()
        {
            // Calculer l'effet maximal attendu pour 1 g de paracétamol et 400 mg d'ibuprofène
            DateTime refTime = DateTime.Now;
            var paraDose = new DoseEntry(refTime, 1000, 72, "paracetamol");
            var ibuDose = new DoseEntry(refTime, 400, 72, "ibuprofen");
            var paraPeak = _paraCalc.GenerateGraph(new List<DoseEntry> { paraDose }, refTime, refTime.AddHours(6), 120)
                                      .Max(p => p.Concentration);
            var ibuPeak = _ibuCalc.GenerateGraph(new List<DoseEntry> { ibuDose }, refTime, refTime.AddHours(6), 120)
                                    .Max(p => p.Concentration);
            double effectPara = _paraPd.GetEffectPercent(paraPeak);
            double effectIbu = _ibuPd.GetEffectPercent(ibuPeak);
            _maxCombinedEffect = effectPara + effectIbu;
            if (_maxCombinedEffect <= 0) _maxCombinedEffect = 100.0; // Sécurité
        }

        private bool IsParacetamol(DoseEntry d) =>
            string.Equals(d.MoleculeKey, "paracetamol", StringComparison.OrdinalIgnoreCase);

        private bool IsIbuprofen(DoseEntry d) =>
            string.Equals(d.MoleculeKey, "ibuprofen", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(d.MoleculeKey, "ibuprofene", StringComparison.OrdinalIgnoreCase);

        private IEnumerable<DoseEntry> FilterPara(IEnumerable<DoseEntry> doses) => doses.Where(IsParacetamol);
        private IEnumerable<DoseEntry> FilterIbu(IEnumerable<DoseEntry> doses) => doses.Where(IsIbuprofen);

        public double CalculateSingleDoseConcentration(DoseEntry dose, DateTime time)
        {
            if (string.Equals(dose.MoleculeKey, "paracetamol", StringComparison.OrdinalIgnoreCase))
            {
                double c = _paraCalc.CalculateSingleDoseConcentration(dose, time);
                return _paraPd.GetEffectPercent(c);
            }
            if (string.Equals(dose.MoleculeKey, "ibuprofen", StringComparison.OrdinalIgnoreCase))
            {
                double c = _ibuCalc.CalculateSingleDoseConcentration(dose, time);
                return _ibuPd.GetEffectPercent(c);
            }
            return 0;
        }

        public double CalculateTotalConcentration(List<DoseEntry> doses, DateTime time)
        {
            double paraConc = _paraCalc.CalculateTotalConcentration(FilterPara(doses).ToList(), time);
            double ibuConc = _ibuCalc.CalculateTotalConcentration(FilterIbu(doses).ToList(), time);
            double effectPara = _paraPd.GetEffectPercent(paraConc);
            double effectIbu = _ibuPd.GetEffectPercent(ibuConc);
            double sum = effectPara + effectIbu;
            return 100.0 * sum / _maxCombinedEffect;
        }

        public double CalculateTotalAmount(List<DoseEntry> doses, DateTime time)
        {
            return CalculateTotalConcentration(doses, time);
        }

        public double GetDoseDisplayValueInConcentrationUnit(DoseEntry dose)
        {
            return dose.DoseMg;
        }

        public List<(DateTime Time, double Concentration)> GenerateGraph(List<DoseEntry> doses, DateTime startTime, DateTime endTime, int points = 200)
        {
            var list = new List<(DateTime, double)>();
            var span = endTime - startTime;
            var interval = span.TotalMinutes / points;
            for (int i = 0; i <= points; i++)
            {
                DateTime t = startTime.AddMinutes(i * interval);
                list.Add((t, CalculateTotalConcentration(doses, t)));
            }
            return list;
        }

        public (List<(DateTime Time, double EffectPara)>, List<(DateTime Time, double EffectIbu)>, List<(DateTime Time, double EffectTotal)>) GenerateEffectGraph(List<DoseEntry> doses, DateTime startTime, DateTime endTime, int points = 200)
        {
            var span = endTime - startTime;
            var interval = span.TotalMinutes / points;
            var paraList = new List<(DateTime, double)>();
            var ibuList = new List<(DateTime, double)>();
            var totalList = new List<(DateTime, double)>();
            for (int i = 0; i <= points; i++)
            {
                DateTime t = startTime.AddMinutes(i * interval);
                double paraConc = _paraCalc.CalculateTotalConcentration(FilterPara(doses).ToList(), t);
                double ibuConc = _ibuCalc.CalculateTotalConcentration(FilterIbu(doses).ToList(), t);
                double effectPara = _paraPd.GetEffectPercent(paraConc);
                double effectIbu = _ibuPd.GetEffectPercent(ibuConc);
                paraList.Add((t, effectPara));
                ibuList.Add((t, effectIbu));
                double sum = effectPara + effectIbu;
                totalList.Add((t, 100.0 * sum / _maxCombinedEffect));
            }
            return (paraList, ibuList, totalList);
        }
    }
}
