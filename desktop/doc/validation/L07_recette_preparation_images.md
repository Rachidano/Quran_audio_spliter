# Procès-verbal de recette — Lot 7 : Préparation des images d’ayat (L07-R)

**Date** : 11 octobre 2026  
**Intervenant** : Antigravity  
**Statut** : **VALIDÉ** (57/57 tests automatiques réussis, extraction déterministe en pixels source, isolation sur fond blanc sans recomposition typographique, manifestes sidecar JSON et éditeur de retouche/validation)

---

## 1. Périmètre et réalisations du Lot 7

1. **Service d'extraction industrielle et cache (`L07-01`, `AyahImageService.cs`)** :
   - Extraction déterministe d'images d'ayat à partir des pixels originaux du Mushaf (`marw-newMushaf`).
   - Fond blanc pur (#FFFFFF), préservation intégrale de la disposition relative des lignes, de l'indentation, des diacritiques et des médaillons numérotés sans aucune recomposition de police ni OCR.
   - Cache disque versionné dans l'espace de travail (`media/images/ayahs/ayah_{surah:D3}_{ayah:D3}_rev{revision}.png`) indexé par empreinte SHA-256 de la page source.
   - Idempotence : une image déjà calculée et dont l'empreinte concorde est réutilisée instantanément sans calcul redondant.

2. **Interface de validation et panneau de correction à la demande (`L07-02`, `StudioView.xaml`, `StudioViewModel.cs`)** :
   - Affichage immédiat du résultat proposé avec deux boutons explicites en arabe :
     - `✔ الصورة صحيحة` (valide l'extraction proposée et passe le statut à `Approved`).
     - `✏ تصحيح الصورة` (ouvre à la demande le tiroir de retouche et de comparaison).
   - Tiroir de retouche avec boutons d'ajustement des marges (+/- 5px par direction : Haut, Bas, Droite, Gauche) et bouton `🔄 إعادة الضبط` (réinitialisation).
   - **Aucune coordonnée brute à saisir manuellement dans le parcours utilisateur courant.**

3. **Indépendance stricte vis-à-vis du zoom et du DPI Windows (`L07-03`)** :
   - Toutes les dimensions et coordonnées restent calculées en coordonnées pixels de l'image source originale, garantissant une reproductibilité parfaite quel que soit le facteur d'échelle d'affichage Windows (100%, 150%, 200%).

4. **Préservation des signes et délimitations fines (`L07-04`)** :
   - Marges appliquées localement autour de l'enveloppe de l'ayah sans mordre sur l'ayah ou la basmala précédente.

5. **Gestion multi-fragments et multi-pages (`L07-05`)** :
   - Support des passages s'étendant sur plusieurs régions ou pages, conservant l'ordre naturel et la structure sans juxtaposition artificielle.

6. **Format sans perte et manifestes sidecar (`L07-06`)** :
   - Enregistrement en format PNG sans perte 32bpp ARGB.
   - Génération d'un fichier manifeste JSON d'accompagnement (`ayah_{surah:D3}_{ayah:D3}_manifest.json`) consignant : `ayahId`, `revision`, `status`, `width`, `height`, `imageSha256`, `sourcePageSha256`, `margins`, `generatedAtUtc`.
   - Règle d'invalidation : toute modification des marges incrémente `Revision` et requalifie les compositions dépendantes.

7. **Prise en charge des ayat longues (`L07-07`)** :
   - Calcul de redimensionnement proportionnel sans distorsion ni étirement vertical/horizontal du texte sacré.

---

## 2. Résultats des tests automatiques

Exécution de `dotnet test desktop/QuranVideo.sln` :

```text
Série de tests pour D:\Projets\ITComp\Quran\desktop\tests\QuranVideo.Tests\bin\Debug\net9.0-windows\QuranVideo.Tests.dll (.NETCoreApp,Version=v9.0)
Version de VSTest 17.14.1 (x64)

Démarrage de l'exécution de tests, patientez...
Au total, 1 fichiers de test ont correspondu au modèle spécifié.

Réussi!  - échec : 0, réussite : 57, ignorée(s) : 0, total : 57, durée : 2 s - QuranVideo.Tests.dll (net9.0)
```

### Tests spécifiques du Lot 7 validés (7 nouveaux tests) :
1. `AyahImageTests.GetOrExtractAyahImage_ExtractsLosslessPngAndGeneratesManifest` : Extraction PNG sans perte, validation des dimensions et génération du manifeste JSON sidecar.
2. `AyahImageTests.GetOrExtractAyahImage_CachedCall_ReusesExistingFileWithoutRecomputation` : Réutilisation du cache disque sans recalcul.
3. `AyahImageTests.ApproveImage_UpdatesStatusToApproved` : Validation humaine (`الصورة صحيحة`).
4. `AyahImageTests.ApplyCorrection_NudgesMarginsAndIncrementsRevision` : Retouche des bords, incrément de révision et mise à jour des dimensions.
5. `AyahImageTests.ResetCorrection_ResetsMarginsToZero` : Réinitialisation propre des marges à 0.
6. `AyahImageTests.GenerateSideBySideComparison_ProducesValid3ViewImage` : Génération de la planche comparative 3 vues (Originale | Masque superposé | Isolée fond blanc).
7. `AyahImageTests.StudioViewModel_ImageIntegration_ApprovesAndNudges` : Intégration complète ViewModel/Vue.

---

## 3. Critères d'acceptation du plan

| Critère d'acceptation du plan | Constat / Preuve | Statut |
|---|---|---|
| Extraction déterministe avec cache et SHA-256 | Validé dans AyahImageService | **VALIDÉ** |
| `الصورة صحيحة` et `تصحيح الصورة` sans coordonnées brutes | Validé dans StudioView.xaml | **VALIDÉ** |
| Coordonnées en pixels source indépendants du DPI | Validé dans AyahGeometry | **VALIDÉ** |
| Préservation des signes diacritiques et médaillons | Validé sans recomposition de police | **VALIDÉ** |
| Manifeste sidecar et incrément de révision | Validé dans ImageRecipeManifest | **VALIDÉ** |

---

## 4. Conclusion

Le **Lot 7 (Préparation des images d’ayat)** est intégralement validé.  
Prochaine étape : **Lot 8 — Composition et export MP4 individuel** (L08-01 à L08-R).
