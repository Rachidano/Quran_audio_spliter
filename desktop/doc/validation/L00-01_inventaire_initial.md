# Rapport d'inventaire initial et état Git (L00-01)

Date : 11 octobre 2026  
Intervenant : Antigravity  
Tâche : **L00-01** — Relever l’état Git initial et lire les besoins et conventions du projet existant ; établir un inventaire sans modification du site.

---

## 1. État Git initial

- **Branche active** : `master`
- **Dernier commit du dépôt** : `5e98560` (`feat(search): paginate all matching ayahs`)
- **Statut de l'arbre de travail** :
  - Aucun fichier existant du site ou du dépôt n'a été modifié.
  - Le répertoire `desktop/` est le seul élément nouveau non suivi.
  - La commande `git status` confirme que le site web et ses données sont strictement intacts.

---

## 2. Conventions et contraintes applicables

1. **Intégrité absolue du site existant** :
   - Aucun fichier sous `src/`, `tests/`, `data/`, `docs/`, `scripts/` ne doit être modifié ou déplacé pour les besoins du desktop.
   - Aucune dépendance d'exécution vis-à-vis du serveur web ASP.NET Core ni de PostgreSQL.
   - Les répertoires existants servent de sources immuables en lecture seule.

2. **Cible et architecture de l'application desktop** :
   - Solution isolée sous `desktop/`.
   - Cible : `net9.0-windows` (WPF, C# .NET 9).
   - Architecture sans suringénierie : un projet applicatif principal (`QuranVideo.Desktop`) et un projet de tests (`QuranVideo.Tests`).
   - Données : SQLite local (`Microsoft.Data.Sqlite` / EF Core SQLite) pour les données structurées et dossiers locaux pour les médias.
   - Fonctionnement autonome hors-ligne après acquisition locale des ressources.

3. **Exigences d'interface et d'installation** :
   - Interface utilisateur entièrement en **arabe RTL**.
   - Navigation simple en 3 entrées principales : `المكتبة` (Bibliothèque), `إعداد الفيديو` (Préparation vidéo), `فيديوهاتي` (Mes vidéos).
   - Paramètres avancés et techniques masqués par défaut (pas de formulaires de débits, de codecs ou de millisecondes imposés à l'utilisateur ordinaire).
   - Packaging final : un installateur Windows `Setup.exe` incluant le runtime .NET 9, FFmpeg/FFprobe et les polices, avec assistant d'installation en arabe RTL.

4. **Exigences métier religieuses et graphiques** :
   - Récitation selon la riwayat **Warsh** (‘an Nafi’).
   - Pas de recomposition du texte des vidéos par une police, OCR ou modèle génératif : extraction directe des **pixels du Mushaf original**.
   - Préservation stricte de la géométrie relative des lignes et des blancs d'indentation.
   - Gestion des reprises audio sans modélisation complexe ni classification théologique des أوجه.

---

## 3. Inventaire des ressources existantes inspectées

### 3.1 Édition du Mushaf (marw-newMushaf / 20260913-full)
- **Emplacement** : `data/mushaf/editions/marw-newMushaf/20260913-full/`
- **Contenu** : 604 pages PNG et 604 fichiers JSON de cartographie des régions, accompagnés de `manifest.json`.
- **Statut** : 1 208 fichiers présents, totalisant ~139,5 Mo pour les images. Les empreintes SHA-256 et dimensions seront recalculées à la tâche L00-02.

### 3.2 Corpus textuel de recherche
- **Emplacement** : `data/reference/warsh-qpc-v10/`
- **Contenu** : `source.json` (6 214 entrées), `manifest.json`, `README-source.md`, `comparison.json`.
- **Rôle** : Base candidate pour la recherche textuelle et la saisie assistée dans l'interface desktop. Ne gouverne pas la pagination ni le rendu des vidéos.

### 3.3 Audio et récitations
- **Inventaire distant** : `data/reference/archive_org_114_inventory.json` (114 URLs de sourates pour le récitateur Saber Abdelhakam).
- **Fichiers locaux réels présents** dans `data/audio/sources/` :
  - Sourate 001 : `001/original.mp3` (1 129 009 octets)
  - Sourate 002 : `002/original.mp3` (247 766 723 octets)
  - Sourate 003 : `003/original.mp3.tmp` (43 646 487 octets — téléchargement partiel interrompu)
  - Sourate 112 : `112/original.mp3` (571 451 octets)
- Constat : Seules 3 sourates complètes (1, 2, 112) sont actuellement présentes localement.

### 3.4 Algorithmes et logique à réutiliser
- **Calcul de géométrie** : `src/Quran.Infrastructure/Services/MarwPilotCorpus.cs` implémente la formule :
  `x = W * [cL/100 + cW/100 * (tL/100 + tW/100 * rL/100)]`
  `w = W * cW/100 * tW/100 * rW/100` (et calcul similaire pour Y et H).
- **Corrections des ouvertures de sourates** : `src/Quran.Infrastructure/Data/marw-opening-review.json` et `MarwOpeningReview.cs`.
- **Normalisation de recherche arabe** : `src/Quran.Application/Common/QuranSearchQuery.cs`.
- **Polices** : `src/Quran.Web/wwwroot/fonts/` (polices arabes adaptées pour l'interface WPF).

---

## 4. Outils et environnement de la machine de développement

- **Système d'exploitation** : Windows (x64)
- **SDK .NET** : `9.0.312` installé (`dotnet --version` = `9.0.312`)
- **FFmpeg / FFprobe** : version 8.0-essentials (`C:\ffmpeg\bin\ffmpeg.exe`, `C:\ffmpeg\bin\ffprobe.exe`)
- **Node.js** : `v22.14.0`
- **Python** : `3.10.0`
- **PowerShell** : `7.5.0`
- **Gestionnaire de paquets** : `winget` disponible pour l'installation d'Inno Setup 6 (outil retenu pour fabriquer le Setup.exe avec assistant arabe RTL).

---

## 5. Conclusion de la tâche L00-01

L'état Git initial est relevé, les besoins et conventions sont validés, l'inventaire des ressources et des algorithmes existants est établi sans aucune modification du site web. La tâche L00-01 est prête à être clôturée, ouvrant la voie à la tâche L00-02 (contrôle d'intégrité et recalcul des empreintes des 604 paires PNG/JSON du Mushaf).
