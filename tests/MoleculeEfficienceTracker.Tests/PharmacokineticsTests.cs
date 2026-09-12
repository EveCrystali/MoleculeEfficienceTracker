using MoleculeEfficienceTracker.Core.Models;
using MoleculeEfficienceTracker.Core.Services;
using Xunit;

namespace MoleculeEfficienceTracker.Tests;

/// <summary>
/// Vérifications du modèle pharmacocinétique.
///
/// Le dépôt n'avait aucun test. C'est précisément ce qui a permis à trois
/// constantes d'absorption de rester fausses pendant quinze mois, et à un seuil
/// « fort » d'être inatteignable par la dose qui le nommait.
/// </summary>
public class PharmacokineticsTests
{
    private static readonly DateTime Reference = new(2026, 6, 15, 8, 0, 0, DateTimeKind.Local);

    private static List<DoseEntry> One(double amount, DateTime when, string key, double weight = 72)
        => new() { new DoseEntry(when, amount, weight, key) };

    // ===== Invariants communs =====

    public static IEnumerable<object[]> AllCalculators()
    {
        yield return new object[] { new CaffeineCalculator(), MoleculeKeys.Caffeine, 80.0 };
        yield return new object[] { new BromazepamCalculator(), MoleculeKeys.Bromazepam, 3.0 };
        yield return new object[] { new ParacetamolCalculator(), MoleculeKeys.Paracetamol, 1000.0 };
        yield return new object[] { new IbuprofeneCalculator(), MoleculeKeys.Ibuprofen, 400.0 };
    }

    [Theory]
    [MemberData(nameof(AllCalculators))]
    public void Concentration_est_nulle_avant_la_prise(IMoleculeCalculator calculator, string key, double dose)
    {
        List<DoseEntry> doses = One(dose, Reference, key);

        Assert.Equal(0, calculator.CalculateTotalConcentration(doses, Reference.AddHours(-1)), 6);
    }

    [Theory]
    [MemberData(nameof(AllCalculators))]
    public void Concentration_decroit_apres_le_pic(IMoleculeCalculator calculator, string key, double dose)
    {
        List<DoseEntry> doses = One(dose, Reference, key);

        double at6h = calculator.CalculateTotalConcentration(doses, Reference.AddHours(6));
        double at12h = calculator.CalculateTotalConcentration(doses, Reference.AddHours(12));

        Assert.True(at12h < at6h, $"{calculator.DisplayName} : {at12h} devrait être sous {at6h}");
    }

    [Theory]
    [MemberData(nameof(AllCalculators))]
    public void Deux_doses_valent_le_double_dune_seule(IMoleculeCalculator calculator, string key, double dose)
    {
        var single = One(dose, Reference, key);
        var pair = new List<DoseEntry>
        {
            new(Reference, dose, 72, key),
            new(Reference, dose, 72, key)
        };

        DateTime at = Reference.AddHours(3);

        Assert.Equal(2 * calculator.CalculateTotalConcentration(single, at),
                     calculator.CalculateTotalConcentration(pair, at), 6);
    }

    // ===== Constantes d'absorption corrigées =====

    [Fact]
    public void Cafeine_culmine_a_quarante_cinq_minutes()
    {
        var calculator = new CaffeineCalculator();

        // La constante disait « pic à 45 min » mais alimentait ka = ln2/T : la
        // courbe culminait en réalité à 2 h 25.
        Assert.Equal(0.75, calculator.PeakDelayHours, 2);

        DateTime peak = calculator.GetPeakTime(Reference);
        List<DoseEntry> doses = One(80, Reference, MoleculeKeys.Caffeine);

        double atPeak = calculator.CalculateTotalConcentration(doses, peak);
        Assert.True(atPeak > calculator.CalculateTotalConcentration(doses, peak.AddMinutes(-20)));
        Assert.True(atPeak > calculator.CalculateTotalConcentration(doses, peak.AddMinutes(20)));
    }

    [Theory]
    [InlineData(35, 0.674)]
    [InlineData(65, 1.252)]
    [InlineData(80, 1.541)]
    public void Pic_de_cafeine_pour_soixante_douze_kilos(double dose, double expectedPeak)
    {
        var calculator = new CaffeineCalculator();

        double peak = calculator.ConcentrationAfterHours(dose, 72, calculator.PeakDelayHours);

        Assert.Equal(expectedPeak, peak, 2);
    }

    [Fact]
    public void Seuil_fort_du_bromazepam_est_atteignable_par_sa_dose_de_reference()
    {
        var calculator = new BromazepamCalculator();
        List<DoseEntry> doses = One(4.5, Reference, MoleculeKeys.Bromazepam);

        double peak = Enumerable.Range(0, 400)
            .Select(i => calculator.CalculateTotalConcentration(doses, Reference.AddMinutes(i)))
            .Max();

        // L'ancien seuil valait 0,0525 mg/L pour un pic réel de 0,0316 : le niveau
        // « fort » ne pouvait jamais être affiché.
        Assert.True(peak >= BromazepamCalculator.STRONG_THRESHOLD * 0.99,
            $"pic {peak:F4} contre seuil {BromazepamCalculator.STRONG_THRESHOLD:F4}");
    }

    [Fact]
    public void Seuil_fort_de_libuprofene_est_atteignable_par_sa_dose_de_reference()
    {
        var calculator = new IbuprofeneCalculator();
        List<DoseEntry> doses = One(400, Reference, MoleculeKeys.Ibuprofen);

        double peak = Enumerable.Range(0, 240)
            .Select(i => calculator.CalculateTotalConcentration(doses, Reference.AddMinutes(i)))
            .Max();

        Assert.True(peak >= IbuprofeneCalculator.STRONG_THRESHOLD * 0.99,
            $"pic {peak:F2} contre seuil {IbuprofeneCalculator.STRONG_THRESHOLD:F2}");
    }

    // ===== Point de contrôle chiffré du plan =====

    [Fact]
    public void Deux_cafes_du_matin_laissent_un_demi_milligramme_au_coucher()
    {
        var calculator = new CaffeineCalculator();
        DateTime day = new(2026, 6, 15, 0, 0, 0, DateTimeKind.Local);

        var doses = new List<DoseEntry>
        {
            new(day.AddHours(8.5), 80, 72, MoleculeKeys.Caffeine),
            new(day.AddHours(9.5), 80, 72, MoleculeKeys.Caffeine)
        };

        double atBedTime = calculator.CalculateTotalConcentration(doses, day.AddHours(23));

        Assert.Equal(0.506, atBedTime, 2);
    }

    // ===== Heure limite du dernier café =====

    [Fact]
    public void Heure_limite_recule_quand_le_socle_du_matin_augmente()
    {
        var calculator = new CaffeineCalculator();
        DateTime day = new(2026, 6, 15, 0, 0, 0, DateTimeKind.Local);
        DateTime bedTime = day.AddHours(23);
        DateTime now = day.AddHours(10);

        var light = new List<DoseEntry> { new(day.AddHours(8), 80, 72, MoleculeKeys.Caffeine) };
        var heavy = new List<DoseEntry>
        {
            new(day.AddHours(8), 80, 72, MoleculeKeys.Caffeine),
            new(day.AddHours(9), 80, 72, MoleculeKeys.Caffeine)
        };

        DateTime? cutoffLight = calculator.LatestIntakeTimeBefore(light, bedTime, 80, 72, now);
        DateTime? cutoffHeavy = calculator.LatestIntakeTimeBefore(heavy, bedTime, 80, 72, now);

        Assert.NotNull(cutoffLight);
        Assert.NotNull(cutoffHeavy);
        Assert.True(cutoffHeavy < cutoffLight,
            "plus le socle du matin est lourd, plus le dernier café doit être avancé");
    }

    [Fact]
    public void Aucune_heure_limite_si_le_socle_depasse_deja_le_seuil()
    {
        var calculator = new CaffeineCalculator();
        DateTime day = new(2026, 6, 15, 0, 0, 0, DateTimeKind.Local);

        var doses = Enumerable.Range(0, 6)
            .Select(i => new DoseEntry(day.AddHours(14 + i * 0.5), 160, 72, MoleculeKeys.Caffeine))
            .ToList();

        Assert.Null(calculator.LatestIntakeTimeBefore(doses, day.AddHours(23), 80, 72, day.AddHours(10)));
    }
}
