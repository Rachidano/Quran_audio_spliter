# Procès-verbal de recette — Lot 3 : Base locale et import des ressources (L03-R)

**Date** : 11 octobre 2026  
**Intervenant** : Antigravity  
**Statut** : **VALIDÉ** (18/18 tests automatiques réussis, import complet des 604 pages et 6 214 ayat vérifié)

---

## 1. Périmètre et réalisations du Lot 3

1. **Modèle de données relationnel SQLite (`desktop/src/QuranVideo.Desktop/Data/`)** :
   - `MushafEditionEntity` : Révision, provenance URL, empreinte SHA-256 du manifeste, système de décompte (`Madani / Warsh`).
   - `SurahEntity` : 114 sourates avec noms arabes et latins, nombre d'ayat (dont Baqara = 285 ayat), pages de début et de fin.
   - `AyahEntity` : Identifiant canonique stable `(SurahNumber:AyahNumber)`, séquence globale `VerseNo` (1 à 6 214), numéro de page.
   - `MushafPageEntity` : 604 pages avec chemin relatif, dimensions (1241x1754 px) et empreinte SHA-256.
   - `AyahRegionEntity` : Séparation stricte entre les coordonnées normalisées brutes d'origine (`RawRectLeft`, `RawRectTop`, etc.) et les coordonnées en pixels calculées (`PixelX`, `PixelY`, etc.) avec prise en compte des seuils de rognage d'ouverture.
   - `AyahTextEntity` : Texte source candidat issu du corpus QPC Warsh (`warsh-qpc-v10`) stocké sans altération ni normalisation destructive en place.
   - `AppDbContext` : Contexte EF Core SQLite optimisé avec index uniques canoniques sur `(SurahNumber, AyahNumber)`, `VerseNo` et `(CorpusId, AyahId)`.

2. **Service d'importation partagé (`CorpusImportService`)** :
   - Vérification du manifeste `manifest.json`.
   - Idempotence complète : un import répété de la même édition et révision ne duplique pas les enregistrements et s'exécute immédiatement.
   - Copie sécurisée des fichiers médias dans le dossier de travail (`media/mushaf/pages/`).
   - Transaction SQLite atomique garantissant l'intégrité en cas d'interruption.

---

## 2. Résultats des tests automatiques

Exécution de `dotnet test desktop/QuranVideo.sln` :

```text
Série de tests pour D:\Projets\ITComp\Quran\desktop\tests\QuranVideo.Tests\bin\Debug\net9.0-windows\QuranVideo.Tests.dll (.NETCoreApp,Version=v9.0)
Version de VSTest 17.14.1 (x64)

Démarrage de l'exécution de tests, patientez...
Au total, 1 fichiers de test ont correspondu au modèle spécifié.

Réussi!  - échec : 0, réussite : 18, ignorée(s) : 0, total : 18, durée : 1 s - QuranVideo.Tests.dll (net9.0)
```

### Tests spécifiques du Lot 3 validés :
1. `CorpusImportTests.ImportPackAsync_ImportsEntitiesAndComputesPixelBoundsCorrectly` : Création des tables, calcul des bouding boxes en pixels et vérification des clés étrangères.
2. `CorpusImportTests.ImportPackAsync_IsIdempotent_SecondCallSucceedsWithoutDuplicateErrors` : Idempotence vérifiée sans doublons.
3. `CorpusImportTests.ImportPackAsync_WhenDirectoryMissing_ReturnsClearArabicError` : Gestion d'erreur robuste avec libellé arabe explicite.
4. `CorpusImportTests.ImportPackAsync_WithRealCorpus_ImportsAll604PagesAnd6214Ayahs` : Import de l'édition réelle `data/mushaf/editions/marw-newMushaf/20260913-full/` :
   - 604 pages importées.
   - 114 sourates importées.
   - 6 214 ayat canoniques vérifiées.
   - 285 ayat pour Al-Baqarah (décompte Warsh exact).
   - 6 214 textes sources QPC associés.

---

## 3. Conformité aux critères de recette du plan

| Critère d'acceptation du plan | Constat / Preuve | Statut |
|---|---|---|
| Import complet sans charger la base du site | Réalisé via `CorpusImportService` directement vers SQLite | **VALIDÉ** |
| 604 pages, 114 sourates, 6 214 références uniques | Validé par test unitaire sur la base SQLite réelle | **VALIDÉ** |
| Séparation des textes et des rects bruts | Colonnes brutes et calculées stockées distinctement | **VALIDÉ** |
| Idempotence de l'import | Testé et confirmé | **VALIDÉ** |
| Détection automatique au premier lancement | Configuré via détection de révision | **VALIDÉ** |

---

## 4. Conclusion

Le **Lot 3 (Base locale et import des ressources)** est intégralement validé.
Prochaine étape : **Lot 4 — Bibliothèque, recherche et navigation** (L04-01 à L04-R).
