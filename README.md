# 🧬 MoleculeEfficienceTracker

[![.NET MAUI](https://img.shields.io/badge/.NET%20MAUI-9.0-blue)](https://dotnet.microsoft.com/apps/maui)
[![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Android-blue)](https://dotnet.microsoft.com/apps/maui)
[![License](https://img.shields.io/badge/License-Proprietary-red)](LICENSE.md)
[![Status](https://img.shields.io/badge/Status-WIP-yellow)](https://github.com/votre-username/MoleculeEfficienceTracker)

## 📖 Description

MoleculeEfficienceTracker est une application mobile multiplateforme développée avec **.NET MAUI** qui permet de calculer et visualiser en temps réel la concentration de différentes molécules dans l'organisme. L'application utilise des modèles pharmacocinétiques scientifiques pour estimer l'évolution des concentrations après chaque prise.

**⚠️ AVERTISSEMENT MÉDICAL IMPORTANT**

> **L'auteur de cette application n'est pas médecin ni professionnel de santé.**
> 
> Cette application est fournie **uniquement à des fins éducatives et informatives**.
> Elle ne fournit pas de conseils médicaux, de diagnostic ou de traitement.
> Les informations ne remplacent en aucun cas une consultation médicale professionnelle.
> 
> **Consultez toujours un professionnel de santé qualifié pour toute question médicale.**
> 
> L'utilisateur utilise cette application à ses propres risques. Le développeur décline toute responsabilité pour les dommages directs ou indirects résultant de l'utilisation de cette application.

## ✨ Fonctionnalités

### 🧪 Molécules supportées
- **Bromazépam** : Demi-vie 14h, absorption 2h, biodisponibilité 84%, dosage en mg
- **Caféine** : Demi-vie 5h, absorption 45min, système d'unités (1 unité = 80mg Nespresso)
- **Alcool** : Élimination linéaire 1 unité/heure, absorption 45min
- **Paracétamol** : Demi-vie 3h, absorption 30min, biodisponibilité 92%, dosage en mg *(en développement)*
- **Ibuprofène** : Demi-vie 2h, absorption 30min, biodisponibilité 90%, dosage en mg *(en développement)*

### 📊 Fonctionnalités principales
- **Suivi des doses** : Enregistrement avec date/heure précise
- **Calculs pharmacocinétiques** : Modèle 1 compartiment avec absorption/élimination du 1er ordre
- **Graphiques temps réel** : Visualisation interactive avec annotations (Syncfusion Charts)
- **Seuils d'efficacité** : Prédictions personnalisées (ex: seuil caféine à 35mg)
- **Sauvegarde automatique** : Persistance JSON locale
- **Export de données** : Sauvegarde au format JSON
- **Interface intuitive** : Navigation par onglets avec design moderne

### 🔬 Modèle mathématique

L'application utilise le modèle pharmacocinétique standard :

```

C(t) = (D × ka / (ka - ke)) × (e^(-ke×t) - e^(-ka×t))

```

Où :
- `C(t)` = Concentration au temps t
- `D` = Dose administrée
- `ka` = Constante d'absorption
- `ke` = Constante d'élimination

## 🚀 Installation

### Prérequis

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [.NET MAUI Workload](https://docs.microsoft.com/dotnet/maui/get-started/installation)
- Visual Studio 2022 17.8+ ou Visual Studio Code avec extensions C#

### Plateformes supportées

| Plateforme | Version minimale | Status |
|------------|------------------|--------|
| Android | API 21 (Android 5.0) | ✅ Testé |
| Windows | Windows 10 version 1809+ | ✅ Testé |


### Étapes d'installation

1. **Cloner le repository**
```

git clone https://github.com/votre-username/MoleculeEfficienceTracker.git
cd MoleculeEfficienceTracker

```

2. **Restaurer les packages**
```

dotnet restore

```

3. **Compiler le projet**
```

dotnet build

```

4. **Déployer sur votre plateforme**
```


# Android

dotnet build -f net9.0-android
dotnet maui deploy -f net9.0-android

# Windows

dotnet build -f net9.0-windows10.0.19041.0
dotnet run -f net9.0-windows10.0.19041.0

# iOS (nécessite macOS)

dotnet build -f net9.0-ios

```

## 🏗️ Architecture

```

MoleculeEfficienceTracker/
├── Core/
│   ├── Models/           \# DoseEntry, ChartDataPoint
│   └── Services/         \# Calculateurs, DataPersistence
├── Pages/               \# BromazepamPage, CaffeinePage, etc.
├── Converters/          \# Formatage UI
└── Resources/           \# Images, styles

```

### Composants principaux

- **`BaseMoleculePage<T>`** : Page générique commune à toutes les molécules
- **`IMoleculeCalculator`** : Interface pour les calculs pharmacocinétiques
- **`DataPersistenceService`** : Sauvegarde/chargement JSON automatique
- **Calculateurs spécialisés** : Un par molécule avec paramètres spécifiques

## 📱 Utilisation

1. **Sélectionner une molécule** via les onglets
2. **Ajouter une dose** en spécifiant la quantité et l'heure
3. **Visualiser la concentration** en temps réel sur le graphique
4. **Consulter l'historique** des doses prises
5. **Exporter les données** si nécessaire

## 🔮 Roadmap

- [ ] Page "Charge totale d'intoxication" (toutes molécules)
- [ ] Extension Ibuprofène (etc.)
- [?] Calcul d'interactions médicamenteuses
- [ ] Prédictions optimisées

## 🤝 Contribution

Ce projet est actuellement en développement privé. Les contributions externes ne sont pas acceptées pour le moment.

## 📄 Licence

Ce logiciel est protégé par une licence propriétaire. Voir [LICENSE.md](LICENSE.md) pour plus de détails.

## ⚖️ Limitation de responsabilité

L'utilisation de cette application se fait aux risques et périls de l'utilisateur. Le développeur ne peut être tenu responsable des conséquences de son utilisation, notamment en matière de santé ou de décisions médicales.

---

**Développé avec ❤️ et .NET MAUI**
