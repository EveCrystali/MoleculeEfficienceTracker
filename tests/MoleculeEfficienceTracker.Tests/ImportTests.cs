using MoleculeEfficienceTracker.Core.Models;
using MoleculeEfficienceTracker.Core.Services;
using Xunit;

namespace MoleculeEfficienceTracker.Tests;

/// <summary>
/// Lecture et fusion d'un document de prises.
///
/// L'enjeu tient en une phrase : les quatre fichiers exportés avant le changement
/// d'identifiant de paquet doivent revenir entiers, et ne revenir qu'une fois.
/// </summary>
public class ImportTests
{
    private const string CamelCaseExport = """
    [
      { "timeTaken": "2026-09-10T08:30:00", "doseMg": 80, "weightKg": 72, "id": "a1", "moleculeKey": "caffeine" },
      { "timeTaken": "2026-09-10T14:00:00", "doseMg": 65, "weightKg": 72, "id": "a2", "moleculeKey": "caffeine" }
    ]
    """;

    /// <summary>Les exports antérieurs sortaient en PascalCase.</summary>
    private const string PascalCaseExport = """
    [
      { "TimeTaken": "2026-09-10T08:30:00", "DoseMg": 80, "WeightKg": 72, "Id": "a1", "MoleculeKey": "Caffeine" }
    ]
    """;

    /// <summary>Les clés historiques : l'alcool s'écrivait « alcool » à la lecture.</summary>
    private const string LegacyKeysExport = """
    [
      { "timeTaken": "2026-09-10T20:00:00", "doseMg": 2, "id": "b1", "moleculeKey": "alcool" },
      { "timeTaken": "2026-09-10T21:00:00", "doseMg": 400, "id": "b2", "moleculeKey": "ibuprofene" }
    ]
    """;

    private const string BackupDocument = """
    {
      "exportedAt": "2026-09-12T09:35:00+02:00",
      "schemaVersion": 2,
      "molecules": {
        "caffeine": [ { "timeTaken": "2026-09-12T07:00:00", "doseMg": 80, "id": "c1" } ],
        "alcohol":  [ { "timeTaken": "2026-09-11T20:00:00", "doseMg": 1.5, "id": "c2" } ]
      }
    }
    """;

    [Fact]
    public void Parse_reads_a_plain_array()
    {
        var parsed = DoseDocument.Parse(CamelCaseExport);

        Assert.Single(parsed);
        Assert.Equal(2, parsed[MoleculeKeys.Caffeine].Count);
        Assert.Equal(80, parsed[MoleculeKeys.Caffeine][0].DoseMg);
    }

    [Fact]
    public void Parse_accepts_pascal_case()
    {
        var parsed = DoseDocument.Parse(PascalCaseExport);

        Assert.Single(parsed[MoleculeKeys.Caffeine]);
        Assert.Equal("a1", parsed[MoleculeKeys.Caffeine][0].Id);
    }

    [Fact]
    public void Parse_normalises_legacy_keys()
    {
        var parsed = DoseDocument.Parse(LegacyKeysExport);

        Assert.True(parsed.ContainsKey(MoleculeKeys.Alcohol));
        Assert.True(parsed.ContainsKey(MoleculeKeys.Ibuprofen));
        Assert.DoesNotContain("alcool", parsed.Keys);
        Assert.DoesNotContain("ibuprofene", parsed.Keys);
    }

    [Fact]
    public void Parse_reads_the_outbound_backup_document()
    {
        var parsed = DoseDocument.Parse(BackupDocument);

        Assert.Equal(2, parsed.Count);
        Assert.Single(parsed[MoleculeKeys.Caffeine]);
        Assert.Single(parsed[MoleculeKeys.Alcohol]);
    }

    [Fact]
    public void Parse_falls_back_to_the_file_name_key()
    {
        const string anonymous = """
        [ { "timeTaken": "2026-09-10T08:30:00", "doseMg": 80, "id": "d1" } ]
        """;

        var parsed = DoseDocument.Parse(anonymous, MoleculeKeys.Caffeine);

        Assert.Single(parsed[MoleculeKeys.Caffeine]);
    }

    [Fact]
    public void Parse_ignores_entries_without_a_dose()
    {
        const string empty = """
        [ { "timeTaken": "2026-09-10T08:30:00", "doseMg": 0, "moleculeKey": "caffeine" } ]
        """;

        Assert.Empty(DoseDocument.Parse(empty));
    }

    [Fact]
    public void Merge_adds_everything_to_an_empty_store()
    {
        var incoming = DoseDocument.Parse(CamelCaseExport)[MoleculeKeys.Caffeine];

        (List<DoseEntry> merged, int added, int skipped) =
            DoseDocument.Merge(new List<DoseEntry>(), incoming);

        Assert.Equal(2, added);
        Assert.Equal(0, skipped);
        Assert.Equal(2, merged.Count);
    }

    /// <summary>Réimporter le même fichier ne doit rien ajouter.</summary>
    [Fact]
    public void Merge_is_idempotent()
    {
        var incoming = DoseDocument.Parse(CamelCaseExport)[MoleculeKeys.Caffeine];

        (List<DoseEntry> once, _, _) = DoseDocument.Merge(new List<DoseEntry>(), incoming);
        (List<DoseEntry> twice, int added, int skipped) =
            DoseDocument.Merge(once, DoseDocument.Parse(CamelCaseExport)[MoleculeKeys.Caffeine]);

        Assert.Equal(0, added);
        Assert.Equal(2, skipped);
        Assert.Equal(2, twice.Count);
    }

    /// <summary>
    /// Un export qui aurait régénéré ses identifiants reste reconnu : molécule,
    /// minute et quantité suffisent à identifier une prise.
    /// </summary>
    [Fact]
    public void Merge_detects_a_duplicate_whose_identifier_changed()
    {
        var existing = new List<DoseEntry>
        {
            new(new DateTime(2026, 9, 10, 8, 30, 0), 80, 72, MoleculeKeys.Caffeine) { Id = "original" }
        };

        var incoming = new List<DoseEntry>
        {
            new(new DateTime(2026, 9, 10, 8, 30, 0), 80, 72, MoleculeKeys.Caffeine) { Id = "regenerated" }
        };

        (_, int added, int skipped) = DoseDocument.Merge(existing, incoming);

        Assert.Equal(0, added);
        Assert.Equal(1, skipped);
    }

    [Fact]
    public void Merge_keeps_the_newest_first()
    {
        var incoming = new List<DoseEntry>
        {
            new(new DateTime(2026, 9, 10, 8, 0, 0), 80, 72, MoleculeKeys.Caffeine) { Id = "tôt" },
            new(new DateTime(2026, 9, 10, 18, 0, 0), 65, 72, MoleculeKeys.Caffeine) { Id = "tard" }
        };

        (List<DoseEntry> merged, _, _) = DoseDocument.Merge(new List<DoseEntry>(), incoming);

        Assert.Equal("tard", merged[0].Id);
    }

    /// <summary>Les noms réels des fichiers exportés, accents translittérés compris.</summary>
    [Theory]
    [InlineData("04edd2bb-alcool_export_20260912_0935.json", MoleculeKeys.Alcohol)]
    [InlineData("bfca6319-antidouleur_export_20260912_0935.json", MoleculeKeys.PainRelief)]
    [InlineData("5c75821d-bromaz_pam_export_20260912_0935.json", MoleculeKeys.Bromazepam)]
    [InlineData("75c96669-caf_ine_export_20260912_0935.json", MoleculeKeys.Caffeine)]
    [InlineData("quelque-chose.json", "")]
    public void GuessKeyFromFileName_recognises_exported_names(string fileName, string expected)
        => Assert.Equal(expected, DoseDocument.GuessKeyFromFileName(fileName));
}
