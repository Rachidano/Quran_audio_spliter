# Rapport d'environnement, dépendances et outil de packaging (L00-07)

Date : 11 octobre 2026  
Intervenant : Antigravity  
Tâche : **L00-07** — Relever le SDK .NET 9 installé, retenir des dépendances compatibles et fixer le SDK sous `desktop/` sans changer celui du site. Consigner un PC Windows x64 de recette et les conditions hors ligne. Retenir un outil existant pour produire un Setup.exe avec assistant arabe RTL ; vérifier tôt un emballage minimal, sans développer un installateur maison.

---

## 1. SDK .NET 9 et verrouillage

- **SDK installé** : `9.0.312` (`C:\Program Files\dotnet\sdk\9.0.312`).
- **Isolation du sous-projet** : Création du fichier `desktop/global.json` :
  ```json
  {
    "sdk": {
      "version": "9.0.312",
      "rollForward": "latestPatch"
    }
  }
  ```
- **Vérification** : La commande `dotnet --version` exécutée dans `desktop/` renvoie exactement `9.0.312`.
- **Intégrité du site** : Aucun fichier racine ni projet sous `src/` n'a été altéré.

---

## 2. Dépendances techniques compatibles retenues

Pour le projet unique WPF (`net9.0-windows`) et le projet de tests :
- **Framework** : .NET 9 (`net9.0-windows`).
- **Architecture UI** : WPF avec `CommunityToolkit.Mvvm` (v8.4.0) — commandes asynchrones, liaison de données sans surcouche inutile.
- **Base de données locale** : `Microsoft.EntityFrameworkCore.Sqlite` (v9.0.2) / `Microsoft.Data.Sqlite` — gestion SQLite locale avec migrations intégrées.
- **Moteur audio** : `NAudio` (v2.2.1) — lecture, capture de flux et gestion des tampons sonores sous Windows.
- **Traitement vidéo / analyse** : `FFmpeg` et `FFprobe` (version 8.0-essentials présente sous `C:\ffmpeg\bin\`).
- **Tests** : `xunit` (v2.9.3), `xunit.runner.visualstudio` (v3.0.2), `Microsoft.NET.Test.Sdk`.

---

## 3. PC de recette et conditions hors-ligne

- **Plateforme matérielle et OS** :
  - Système d'exploitation : Microsoft Windows 11 Professionnel 64 bits (Version 10.0.26200, build 26200).
  - Architecture processeur : x64.
- **Conditions hors-ligne** :
  - Une fois l'édition du Mushaf (images PNG et fichiers JSON) embarquée ou importée et les fichiers MP3 acquis, l'application fonctionne de manière **100% autonome et hors-ligne**.
  - Aucune dépendance d'API cloud, aucun appel réseau obligatoire pour la navigation, la recherche, l'annotation ou l'export vidéo MP4.

---

## 4. Outil retenu pour l'installateur Windows `Setup.exe`

- **Outil retenu** : **Inno Setup 6.7.3** (`C:\Users\r_kaci\AppData\Local\Programs\Inno Setup 6\ISCC.exe`).
- **Justification du choix** :
  - Standard de référence sous Windows pour créer des exécutables autonomes `Setup.exe`.
  - Prise en charge native complète de la langue arabe via `compiler:Languages\Arabic.isl` avec affichage de droite à gauche (`RightToLeft=yes`).
  - Aucun besoin de développer un installateur personnalisé.
  - Permet d'embarquer facilement la publication `dotnet publish -c Release -r win-x64 --self-contained` incluant le runtime .NET 9, FFmpeg, FFprobe et les polices.
- **Vérification d'emballage minimal réussie** :
  - Script test créé : `desktop/packaging/test_package.iss`.
  - Compilation exécutée avec succès via `ISCC.exe` en 2,98 secondes, générant un `Setup.exe` x64 avec assistant en arabe RTL.

---

## 5. Conclusion

La tâche **L00-07** est validée avec succès. L'environnement de build, la compatibilité .NET 9, les dépendances et la chaîne de fabrication de l'installateur en arabe RTL sont confirmés et opérationnels.
