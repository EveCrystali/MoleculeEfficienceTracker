using System;
using System.Collections.Generic;
using System.Linq;
using MoleculeEfficienceTracker.Core.Models;

namespace MoleculeEfficienceTracker.Core.Services;

public class MoleculeTracker<TCalculator> where TCalculator : IMoleculeCalculator, new()
{
    private readonly List<DoseEntry> doses;
    private readonly TCalculator calculator;

    public MoleculeTracker()
    {
        doses = new List<DoseEntry>();
        calculator = new TCalculator();
    }

    public void Run()
    {
        Console.WriteLine($"=== Calculateur de {calculator.DisplayName} ===");
        Console.WriteLine("⚠️  À des fins éducatives uniquement - Suivez toujours votre prescription médicale");
        Console.WriteLine();

        while (true)
        {
            ShowMenu();
            var choice = Console.ReadLine();

            switch (choice)
            {
                case "1":
                    AddDose();
                    break;
                case "2":
                    ShowCurrentConcentration();
                    break;
                case "3":
                    ShowAllDoses();
                    break;
                case "4":
                    ExportGraphData();
                    break;
                case "5":
                    return;
                default:
                    Console.WriteLine("Choix invalide.");
                    break;
            }
        }
    }

    private void ShowMenu()
    {
        Console.WriteLine("\n--- Menu ---");
        Console.WriteLine($"1. Ajouter une dose ({calculator.DoseUnit})");
        Console.WriteLine("2. Voir la concentration actuelle");
        Console.WriteLine("3. Voir toutes les doses");
        Console.WriteLine("4. Exporter les données pour graphique");
        Console.WriteLine("5. Quitter");
        Console.Write("Votre choix : ");
    }

    private void AddDose()
    {
        Console.Write($"Dose en {calculator.DoseUnit} : ");
        if (!double.TryParse(Console.ReadLine(), out double doseMg))
        {
            Console.WriteLine("Dose invalide.");
            return;
        }

        Console.Write("Date et heure (yyyy-MM-dd HH:mm) ou [Entrée] pour maintenant : ");
        var timeInput = Console.ReadLine();

        DateTime timeTaken;
        if (string.IsNullOrWhiteSpace(timeInput))
        {
            timeTaken = DateTime.Now;
        }
        else if (!DateTime.TryParseExact(timeInput, "yyyy-MM-dd HH:mm",
                 null, System.Globalization.DateTimeStyles.None, out timeTaken))
        {
            Console.WriteLine("Format de date invalide.");
            return;
        }

        doses.Add(new DoseEntry(timeTaken, doseMg));
        Console.WriteLine($"✅ Dose de {doseMg}{calculator.DoseUnit} ajoutée pour {timeTaken:dd/MM/yyyy HH:mm}");
    }

    private void ShowCurrentConcentration()
    {
        var currentTime = DateTime.Now;
        var concentration = calculator.CalculateTotalConcentration(doses, currentTime);

        Console.WriteLine($"\n📊 Concentration actuelle estimée : {concentration:F2} unités");
        Console.WriteLine($"🕐 Calculé à : {currentTime:dd/MM/yyyy HH:mm}");

        Console.WriteLine("\nContribution par dose :");
        foreach (var dose in doses.OrderByDescending(d => d.TimeTaken))
        {
            var individual = calculator.CalculateSingleDoseConcentration(dose, currentTime);
            if (individual > 0.01)
            {
                Console.WriteLine($"  {dose.TimeTaken:dd/MM HH:mm} - {dose.DoseMg}{calculator.DoseUnit} → {individual:F2}");
            }
        }
    }

    private void ShowAllDoses()
    {
        Console.WriteLine("\n📋 Historique des doses :");
        foreach (var dose in doses.OrderBy(d => d.TimeTaken))
        {
            Console.WriteLine($"  {dose.TimeTaken:dd/MM/yyyy HH:mm} - {dose.DoseMg}{calculator.DoseUnit}");
        }
    }

    private void ExportGraphData()
    {
        if (!doses.Any())
        {
            Console.WriteLine("Aucune dose enregistrée.");
            return;
        }

        var startTime = doses.Min(d => d.TimeTaken).AddHours(-2);
        var endTime = DateTime.Now.AddHours(24);

        var graphPoints = calculator.GenerateGraph(doses, startTime, endTime);

        var fileName = $"{calculator.DisplayName.ToLowerInvariant()}_data_{DateTime.Now:yyyyMMdd_HHmm}.csv";
        using (var writer = new System.IO.StreamWriter(fileName))
        {
            writer.WriteLine("DateTime,Concentration");
            foreach (var point in graphPoints)
            {
                writer.WriteLine($"{point.Time:yyyy-MM-dd HH:mm},{point.Concentration:F4}");
            }
        }

        Console.WriteLine($"✅ Données exportées vers {fileName}");
        Console.WriteLine("Tu peux importer ce fichier dans Excel ou un autre outil pour créer un graphique.");
    }
}
