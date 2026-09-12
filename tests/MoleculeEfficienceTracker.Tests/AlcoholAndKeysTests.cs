using MoleculeEfficienceTracker.Core.Models;
using MoleculeEfficienceTracker.Core.Services;
using Xunit;

namespace MoleculeEfficienceTracker.Tests;

public class AlcoholTests
{
    private static readonly DateTime Evening = new(2026, 6, 15, 20, 0, 0, DateTimeKind.Local);

    public AlcoholTests()
    {
        UserProfile.WeightKg = 72;
        UserProfile.Sex = Sex.Male;
    }

    [Fact]
    public void Elimination_ne_sacelere_pas_avec_le_nombre_de_verres()
    {
        var calculator = new AlcoholCalculator();

        // Quatre verres pris ensemble s'éliminent quatre fois plus lentement que
        // quatre verres isolés ne le feraient dans l'ancien modèle, qui appliquait
        // l'élimination dose par dose puis sommait.
        var four = Enumerable.Range(0, 4)
            .Select(_ => new DoseEntry(Evening, 1.0, 72, MoleculeKeys.Alcohol) { BeverageType = "biere" })
            .ToList();

        var one = new List<DoseEntry> { new(Evening, 1.0, 72, MoleculeKeys.Alcohol) { BeverageType = "biere" } };

        double peakFour = four.Count * 10.0 / (72 * 0.7);
        double atPeak = calculator.CalculateTotalConcentration(four, Evening.AddHours(0.33));
        Assert.Equal(peakFour, atPeak, 3);

        // Une heure après le pic, il manque exactement 0,15 g/L — le taux unique.
        double oneHourLater = calculator.CalculateTotalConcentration(four, Evening.AddHours(1.33));
        Assert.Equal(atPeak - 0.15, oneHourLater, 3);

        // Et pour un seul verre, la même pente.
        double singlePeak = calculator.CalculateTotalConcentration(one, Evening.AddHours(0.33));
        double singleLater = calculator.CalculateTotalConcentration(one, Evening.AddHours(1.33));
        Assert.Equal(singlePeak - 0.15, singleLater, 3);
    }

    [Fact]
    public void Alcoolemie_ne_descend_jamais_sous_zero()
    {
        var calculator = new AlcoholCalculator();
        var doses = new List<DoseEntry> { new(Evening, 1.0, 72, MoleculeKeys.Alcohol) };

        Assert.Equal(0, calculator.CalculateTotalConcentration(doses, Evening.AddHours(48)), 6);
    }

    [Fact]
    public void Le_type_de_boisson_suit_la_prise()
    {
        var calculator = new AlcoholCalculator();

        var spirit = new List<DoseEntry> { new(Evening, 1.0, 72, MoleculeKeys.Alcohol) { BeverageType = "spiritueux" } };
        var wine = new List<DoseEntry> { new(Evening, 1.0, 72, MoleculeKeys.Alcohol) { BeverageType = "vin" } };

        // À un quart d'heure, le spiritueux (absorption 0,25 h) est plus avancé que
        // le vin (0,5 h). Le type vivait auparavant sur le calculateur et
        // s'appliquait rétroactivement à tout l'historique.
        double atQuarter = calculator.CalculateTotalConcentration(spirit, Evening.AddMinutes(15));
        double wineAtQuarter = calculator.CalculateTotalConcentration(wine, Evening.AddMinutes(15));

        Assert.True(atQuarter > wineAtQuarter);
    }

    [Fact]
    public void Volume_et_degre_donnent_des_unites_standard()
    {
        // 330 ml à 5 % : 330 × 0,05 × 0,8 = 13,2 g, soit 1,32 unité.
        Assert.Equal(1.32, AlcoholCalculator.VolumePercentToUnits(330, 5), 3);
    }

    [Fact]
    public void Quantite_restante_est_en_unites_pas_en_concentration()
    {
        var calculator = new AlcoholCalculator();
        var doses = new List<DoseEntry> { new(Evening, 2.0, 72, MoleculeKeys.Alcohol) };

        DateTime atPeak = Evening.AddHours(0.5);
        double amount = calculator.CalculateTotalAmount(doses, atPeak);
        double concentration = calculator.CalculateTotalConcentration(doses, atPeak);

        // L'ancienne version renvoyait la concentration, si bien que l'écran
        // affichait deux fois la même valeur sous deux unités différentes.
        Assert.NotEqual(amount, concentration, 3);
        Assert.Equal(2.0, amount, 2);
    }
}

public class MoleculeKeyTests
{
    [Theory]
    [InlineData("alcool", MoleculeKeys.Alcohol)]
    [InlineData("alcohol", MoleculeKeys.Alcohol)]
    [InlineData("ALCOOL", MoleculeKeys.Alcohol)]
    [InlineData("ibuprofene", MoleculeKeys.Ibuprofen)]
    [InlineData("ibuprofène", MoleculeKeys.Ibuprofen)]
    [InlineData("ibuprofen", MoleculeKeys.Ibuprofen)]
    [InlineData("cafeine", MoleculeKeys.Caffeine)]
    [InlineData("caffeine", MoleculeKeys.Caffeine)]
    public void Les_variantes_historiques_convergent(string input, string expected)
    {
        Assert.Equal(expected, MoleculeKeys.Normalize(input));
    }

    [Fact]
    public void Une_cle_vide_prend_la_valeur_de_repli()
    {
        Assert.Equal(MoleculeKeys.Caffeine, MoleculeKeys.Normalize("", MoleculeKeys.Caffeine));
        Assert.Equal(string.Empty, MoleculeKeys.Normalize(null));
    }

    [Fact]
    public void Laffectation_normalise_la_cle_de_la_prise()
    {
        var dose = new DoseEntry(DateTime.Now, 400, 72, "ibuprofene");
        Assert.Equal(MoleculeKeys.Ibuprofen, dose.MoleculeKey);
    }

    [Fact]
    public void Lalcool_se_compte_en_unites_les_autres_en_milligrammes()
    {
        Assert.Equal(DoseUnits.StandardUnit, MoleculeKeys.DoseUnit(MoleculeKeys.Alcohol));
        Assert.Equal(DoseUnits.Milligram, MoleculeKeys.DoseUnit(MoleculeKeys.Caffeine));
    }
}

public class TimeHandlingTests
{
    [Fact]
    public void Un_horodatage_sans_fuseau_est_lu_en_heure_locale()
    {
        var naive = new DateTime(2026, 2, 19, 12, 50, 0, DateTimeKind.Unspecified);
        var dose = new DoseEntry(naive, 80, 72, MoleculeKeys.Caffeine);

        Assert.Equal(DateTimeKind.Local, dose.TimeTaken.Kind);
    }

    [Fact]
    public void Lecart_traverse_correctement_le_changement_dheure()
    {
        // Nuit du 25 au 26 octobre 2026 : l'heure murale recule d'une heure à 3 h.
        // En arithmétique murale, l'écart annoncé serait de 3 h ; l'écart réel est
        // de 4 h, et c'est lui qui compte pour une concentration.
        var before = new DateTime(2026, 10, 25, 1, 0, 0, DateTimeKind.Local);
        var after = new DateTime(2026, 10, 25, 4, 0, 0, DateTimeKind.Local);

        double elapsed = PkTime.ElapsedHours(before, after);
        double wallClock = (after - before).TotalHours;

        Assert.Equal(3, wallClock, 6);
        Assert.True(elapsed >= 3, "l'écart réel ne peut pas être inférieur à l'écart murale");
    }

    [Fact]
    public void Une_prise_future_ne_produit_aucune_concentration()
    {
        var calculator = new CaffeineCalculator();
        DateTime now = new(2026, 6, 15, 10, 0, 0, DateTimeKind.Local);
        var doses = new List<DoseEntry> { new(now.AddHours(2), 80, 72, MoleculeKeys.Caffeine) };

        Assert.Equal(0, calculator.CalculateTotalConcentration(doses, now), 6);
    }
}

public class PainReliefCombinationTests
{
    [Fact]
    public void Leffet_combine_ne_depasse_jamais_cent_pour_cent()
    {
        var calculator = new CombinedPainReliefCalculator();
        DateTime now = new(2026, 6, 15, 10, 0, 0, DateTimeKind.Local);

        var doses = new List<DoseEntry>
        {
            new(now, 4000, 72, MoleculeKeys.Paracetamol),
            new(now, 2400, 72, MoleculeKeys.Ibuprofen)
        };

        double effect = Enumerable.Range(0, 300)
            .Select(i => calculator.CalculateTotalConcentration(doses, now.AddMinutes(i)))
            .Max();

        // L'addition de deux effets Emax pouvait grimper à 156 % avant
        // normalisation par une constante mesurée au lancement.
        Assert.InRange(effect, 0, 100);
    }

    [Fact]
    public void Le_seuil_net_vaut_cinquante_pour_cent()
    {
        var calculator = new CombinedPainReliefCalculator();
        Assert.Equal(50.0, calculator.ModeratePercent, 1);
    }

    [Fact]
    public void Les_seuils_sont_ordonnes()
    {
        var calculator = new CombinedPainReliefCalculator();

        Assert.True(calculator.StrongPercent > calculator.ModeratePercent);
        Assert.True(calculator.ModeratePercent > calculator.LightPercent);
        Assert.True(calculator.LightPercent > calculator.NegligibleEffect);
    }
}
