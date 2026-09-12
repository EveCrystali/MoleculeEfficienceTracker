# MoleculeEfficienceTracker

Application .NET MAUI de suivi pharmacocinétique personnel : elle enregistre les
prises et estime, pour chaque molécule, ce qu'il en reste dans l'organisme à un
instant donné.

> **Ceci n'est pas un dispositif médical.**
>
> L'auteur n'est ni médecin ni professionnel de santé. Les concentrations
> affichées sont des estimations issues de modèles moyens de population, jamais
> une mesure. Elles ne remplacent aucun avis médical et ne valent aucune
> autorisation — en particulier pas celle de conduire.

## État

Reprise et modernisation complètes, septembre 2026. La version précédente datait
d'août 2025 et tournait sur .NET 9, sorti de support depuis mai 2026.

Ce qui a changé, et pourquoi :

- **Les données ne s'effacent plus en silence.** Une lecture qui échoue met le
  fichier de côté et remonte l'erreur au lieu de rendre une liste vide que la
  saisie suivante écrasait. L'écriture est atomique.
- **Une seule clé par molécule.** L'alcool était écrit `alcohol` et lu `alcool` :
  la ligne Alcool de la page de synthèse affichait zéro depuis l'origine. Même
  flottement entre `ibuprofen` et `ibuprofene`.
- **Constantes d'absorption corrigées.** Elles étaient documentées comme le délai
  du pic mais injectées comme demi-vies d'absorption : le pic de la caféine
  tombait à 2 h 25 au lieu de 45 minutes.
- **Seuils atteignables.** Le niveau « fort » du bromazépam se situait 11 % au-dessus
  de ce que sa dose de référence peut produire.
- **Alcool à compartiment unique.** L'élimination d'ordre zéro était appliquée dose
  par dose puis sommée : quatre verres s'éliminaient quatre fois plus vite.
- **Saisie en un geste.** Trois doses en accès direct, plus un raccourci d'écran
  d'accueil Android qui enregistre sans ouvrir l'application.
- **Une phrase avant la courbe.** L'écran caféine répond « dernier café avant
  telle heure pour dormir à telle heure » ; le graphique documente, il ne tranche pas.
- **Lisibilité.** Palette rouge / orange / bleu, sans opposition rouge-vert, et
  chaque seuil se distingue aussi par son motif de trait. Thème sombre réellement
  rendu.

## Molécules

| Molécule | Demi-vie | Pic | Vd | Biodisponibilité | Unité de saisie |
|---|---|---|---|---|---|
| Caféine | 5 h | 45 min | 0,65 L/kg | 100 % | mg |
| Bromazépam | 14 h | 2 h 03 | 1,0 L/kg | 84 % | mg |
| Paracétamol | 2,5 h | 30 min | 0,95 L/kg | 92 % | mg |
| Ibuprofène | 2 h | 30 min | 0,15 L/kg | 90 % | mg |
| Alcool | élimination 0,15 g/L·h | selon la boisson | 0,7 ou 0,6 L/kg | — | unité (10 g) |

Modèle à un compartiment avec absorption du premier ordre pour les quatre
premières :

```
C(t) = (F · D · ka) / (Vd · (ka − ke)) · (e^(−ke·t) − e^(−ka·t))
```

L'alcool suit Widmark : montée linéaire jusqu'à la fin de l'absorption, puis
élimination à taux constant appliquée **au total**, jamais dose par dose.

## Construire

Prérequis : SDK .NET 10, charge de travail `maui-android`, JDK 17, Android SDK
API 36.

```bash
dotnet workload install maui-android

# Tests de la couche de calcul — ils ne demandent ni Android ni émulateur
dotnet test tests/MoleculeEfficienceTracker.Tests/MoleculeEfficienceTracker.Tests.csproj

# Application
dotnet build MoleculeEfficienceTracker.csproj -f net10.0-android36.0 -c Release
```

Sous Windows, la cible `net10.0-windows10.0.19041.0` s'ajoute automatiquement.
Il n'y a **pas** de cible iOS ni macOS : les dossiers de plateforme correspondants
n'existent pas.

### Licence Syncfusion

Les graphiques utilisent Syncfusion, qui affiche un bandeau d'essai sans clé
enregistrée. La clé n'est jamais écrite dans le dépôt : elle est lue à la
compilation dans la variable d'environnement `SYNCFUSION_LICENSE_KEY` et déposée
en métadonnée d'assemblage.

```bash
SYNCFUSION_LICENSE_KEY="votre-clé" dotnet build ...
```

En intégration continue, le secret de dépôt du même nom suffit.

### APK

Le workflow **APK Android** produit un APK signé et le publie en artefact. Sans
les secrets `ANDROID_KEYSTORE_BASE64`, `ANDROID_KEYSTORE_PASSWORD` et
`ANDROID_KEY_ALIAS`, la signature est éphémère : il faut alors désinstaller la
version précédente avant d'installer la nouvelle.

## Architecture

```
Core/
  Models/      DoseEntry, MoleculeKeys, UserProfile, ChartDataPoint, ResidualLoadSnapshot
  Services/    calculateurs, persistance, migration, statistiques, notifications
  Design/      EffectPalette — la palette et les motifs de trait
  Extensions/  ObservableRangeCollection
Controls/      MoleculePanelView — le corps commun à toutes les pages molécules
Converters/
Platforms/     Android (raccourci d'écran d'accueil inclus), Windows
Resources/     styles, polices, icônes
tests/         couche de calcul, sans dépendance à la plateforme
```

`BaseMoleculePage<TCalculator>` porte la logique commune ; `MoleculePanelView`
porte la vue commune. Les trois écrans molécules partageaient auparavant jusqu'à
91 % de leurs lignes de XAML, recopiées à la main.

## Données

Un fichier JSON par molécule dans le répertoire de données de l'application. Une
migration versionnée s'exécute au premier lancement : elle sauvegarde, réunifie les
clés, reconstruit les fuseaux horaires absents, arrondit les flottants et écarte
les double-saisies. L'export et l'import partagent enfin la même convention de
nommage — un fichier exporté n'était pas relisible par l'application.

## Licence

Logiciel propriétaire. Voir [LICENSE.md](LICENSE.md).
