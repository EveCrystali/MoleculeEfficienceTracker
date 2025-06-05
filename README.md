# MoleculeEfficienceTracker

MoleculeEfficienceTracker est une application mobile multiplateforme construite avec **.NET MAUI**. Elle permet d'enregistrer des prises de différentes molécules (bromazépam, caféine ou alcool) et d'estimer leur concentration dans le temps.

## Fonctionnalités principales

- **Suivi des doses** : chaque page de molécule permet d'ajouter une dose avec une date et une heure. Les doses sont sauvegardées localement grâce au service `DataPersistenceService` qui stocke les données dans un fichier par molécule【F:Core/Services/DataPersistenceService.cs†L10-L23】.
- **Calcul de concentration** : les classes `IMoleculeCalculator` et ses implémentations calculent la concentration estimée en fonction des paramètres pharmacocinétiques de chaque molécule (demi‑vie, temps d'absorption, etc.)【F:Core/Services/BromazepamCalculator.cs†L11-L26】【F:Core/Services/CaffeineCalculator.cs†L10-L30】【F:Core/Services/AlcoholCalculator.cs†L8-L29】.
- **Graphique interactif** : chaque page affiche l'évolution de la concentration dans un graphique `Syncfusion` mis à jour après chaque ajout ou suppression de dose【F:BaseMoleculePage.cs†L208-L275】.
- **Annotations spécifiques** : la page Caféine ajoute par exemple une ligne indiquant le seuil d'efficacité et affiche le moment où l'effet devient négligeable【F:CaffeinePage.xaml.cs†L40-L98】.
- **Export et nettoyage des données** : les doses enregistrées peuvent être exportées au format JSON ou entièrement supprimées via les boutons prévus dans l'interface【F:BaseMoleculePage.cs†L373-L407】【F:BaseMoleculePage.cs†L410-L425】.

## Structure générale

- `BaseMoleculePage<T>` : page générique gérant l'interface commune (saisie de dose, liste, graphique, etc.). Les pages spécifiques héritent de cette classe et renseignent leur `Calculator` ainsi que quelques paramètres d'affichage.
- `Core/Services` : contient les calculateurs pour chaque molécule (`BromazepamCalculator`, `CaffeineCalculator`, `AlcoholCalculator`), le service de persistance (`DataPersistenceService`) et un service d'alertes.
- `Core/Models` : modèles `DoseEntry` et `ChartDataPoint` utilisés pour stocker les prises et représenter les points du graphique.
- `Converters` : petites classes utilitaires pour formater les unités ou le texte affiché dans l'interface.

## Lancer l'application

Le projet cible .NET 9 avec MAUI (Android, iOS, Windows et MacCatalyst). Il nécessite donc un SDK .NET compatible ainsi que les outils MAUI installés. Sur un poste configuré :

```bash
# Restauration des packages et compilation
dotnet build

# Déploiement (exemple Android)
dotnet maui deploy -f:net9.0-android
```

## Capture d'écran

Ajoutez ici une capture de l'application pour illustrer l'interface (optionnel).

## Licence

Ce projet est fourni à titre éducatif. Utilisez-le librement selon les termes de la licence du dépôt.
