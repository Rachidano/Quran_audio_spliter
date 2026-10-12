# Procès-verbal de recette — Lot 6 : Studio d'annotation humain (L06-R)

**Date** : 11 octobre 2026  
**Intervenant** : Antigravity  
**Statut** : **VALIDÉ** (50/50 tests automatiques réussis, studio interactif, gestion des reprises sans awjuh, auto-enchaînement, annulation et export/import sécurisé par SHA-256)

---

## 1. Périmètre et réalisations du Lot 6

1. **Intégration complète du Studio (`L06-01`, `StudioView.xaml`, `StudioViewModel.cs`)** :
   - Présentation simultanée : lecteur audio NAudio, courbe de forme d'onde avec curseur interactif, visualisation de la page du Mushaf extraite des pixels réels, description complète de la cible (`سورة البقرة — الآية 4`), et bornes temporelles éditables.
   - Respect strict de l'orientation RTL et de l'expérience utilisateur arabophone.

2. **Définition de début au curseur et formatage (`L06-02`)** :
   - Bouton explicite : `تحديد البداية عند المؤشر` avec raccourci clavier `F7` facultatif.
   - Affichage en minutes et secondes (`mm:ss.ff`).
   - Micro-ajustements fins (+/- 0.1s) et réglages avancés accessibles dans un panneau repliable sans surcharger l'interface courante.
   - Règle de non-rétroactivité : le passage précédent n'est jamais modifié par le réglage d'une nouvelle ayah.

3. **Enregistrement de fin et auto-enchaînement (`L06-03`)** :
   - Bouton explicite : `حفظ والانتقال للتالية` avec raccourcis `F8` ou `Entrée`.
   - Préremplissage automatique : le début suivant prend exactement la valeur de la fin enregistrée (`nextStart = currentEnd`), puis l'index de l'ayah s'incrémente.
   - Protection de fin de sourate : arrivée à la dernière ayah de la sourate (ex: ayah 7 de la Fatiha ou ayah 285 d'Al-Baqarah), la session se clôture avec le message `اكتملت جميع آيات سورة ... بنجاح!` sans créer d'index hors limites ni de référence orpheline.

4. **Conservation de position lors du changement manuel (`L06-04`)** :
   - La navigation manuelle d'ayah en ayah (`الآية السابقة`, `الآية التالية` ou depuis la bibliothèque) préserve la position de lecture courante et les bornes sans réinitialisation intempestive.
   - Persistance de l'état de session dans SQLite (`ayah_passages`) et `config.json`.

5. **Gestion des reprises sans catalogue d'Awjuh (`L06-05`, `L06-06`)** :
   - Options avancées repliées par défaut (`خيارات إضافية`).
   - Typologie claire des passages :
     - `آية كاملة` (Full)
     - `مقطع جزئي` (Portion)
     - `متابعة / وصل` (Continuation)
     - `إعادة كاملة` (Full Retake)
     - `إعادة جزئية` (Partial Retake)
   - **Aucun catalogue d'Awjuh (أوجه) ni sélecteur théologique**, conformément aux règles strictes du projet.
   - Pour le raccourci `F9` (`حفظ المقطع المُعاد`) : affichage explicite et modifiable de la cible (ex: `هدف الإعادة: سورة البقرة — الآية 4`).
   - Règle de synchronisation : après enregistrement de l'ayah 4, la cible principale avance à 5 tandis que la cible de reprise reste explicitement sur l'ayah 4 terminée. Le changement manuel de l'ayah principale synchronise initialement la cible de reprise.

6. **Aperçu, validation humaine et annulation (`L06-07`)** :
   - Bouton `معاينة المقطع` pour écouter le passage délimité.
   - Bouton `اعتماد المقطع` pour marquer la validation humaine explicite.
   - Annulation du dernier enregistrement `تراجع` (`Ctrl+Z`) avec pile d'annulation et restauration de l'état antérieur.
   - Incrément systématique de `Revision` lors de la modification des bornes d'un passage pour invalider les compositions dépendantes.

7. **Protections contre les erreurs de manipulation (`L06-08`)** :
   - Verrou anti-double-clic (`_isOperationBusy`).
   - Neutralisation des raccourcis clavier (`Space`, `F7`, `F8`, `F9`, `Ctrl+Z`) lorsque le focus est dans un champ de saisie (`TextBox`).
   - Rejet immédiat de toute durée négative ou nulle (`EndTimeSec <= StartTimeSec`).
   - Rejet de toute borne dépassant la durée totale du fichier audio source.

8. **Exportation et importation sécurisées des annotations (`L06-09`)** :
   - Format JSON versionné (`formatVersion: "1.0"`).
   - Inscription de l'édition du Mushaf (`editionCode: "newMushaf"`), du numéro de sourate, de l'identifiant audio et de l'empreinte SHA-256 du fichier source.
   - **Contrôle d'intégrité strict à l'import** : rejet immédiat en arabe si l'empreinte SHA-256 du fichier audio ne correspond pas au fichier actuel (`الملف غير متطابق: البصمة الصوتية (SHA-256) للتسجيل لا تطابق الملف الحالي`).

---

## 2. Résultats des tests automatiques

Exécution de `dotnet test desktop/QuranVideo.sln` :

```text
Série de tests pour D:\Projets\ITComp\Quran\desktop\tests\QuranVideo.Tests\bin\Debug\net9.0-windows\QuranVideo.Tests.dll (.NETCoreApp,Version=v9.0)
Version de VSTest 17.14.1 (x64)

Démarrage de l'exécution de tests, patientez...
Au total, 1 fichiers de test ont correspondu au modèle spécifié.

Réussi!  - échec : 0, réussite : 50, ignorée(s) : 0, total : 50, durée : 1 s - QuranVideo.Tests.dll (net9.0)
```

### Tests spécifiques du Lot 6 validés (15 nouveaux tests) :
1. `AnnotationTests.SavePassage_ValidBounds_SavesSuccessfully` : Sauvegarde et chaînage du passage.
2. `AnnotationTests.SavePassage_NegativeStartTime_ReturnsFailure` : Rejet des temps négatifs.
3. `AnnotationTests.SavePassage_EndTimeBeforeOrEqualStartTime_ReturnsFailure` : Rejet des durées nulles/inversées.
4. `AnnotationTests.SavePassage_EndTimeExceedsMaxAudioDuration_ReturnsFailure` : Rejet des dépassements de durée audio.
5. `AnnotationTests.SavePassage_UpdatingBounds_IncrementsRevisionForInvalidation` : Incrément de révision à la modification des bornes.
6. `AnnotationTests.SavePassage_SavingRetake_PreservesKindAndDoesNotReplaceFull` : Préservation des reprises et prises multiples.
7. `AnnotationTests.UndoLastSave_RevertsInsertedPassage` : Annulation du dernier enregistrement (Ctrl+Z).
8. `AnnotationTests.ValidatePassage_TogglesUserValidationState` : Bascule de l'état de validation humaine.
9. `AnnotationTests.ExportAndImportAnnotations_ValidData_RoundtripSucceeds` : Export et réimport atomique JSON.
10. `AnnotationTests.ImportAnnotations_MismatchedAudioSha256_RejectedWithArabicMessage` : Rejet par contrôle SHA-256 strict.
11. `AnnotationTests.ImportAnnotations_IncompatibleFormatVersion_Rejected` : Rejet des versions de format non supportées.
12. `AnnotationTests.StudioViewModel_SaveEndAndAdvance_AdvancesAyahAndPreFillsNextStart` : Enchaînement et préremplissage F8.
13. `AnnotationTests.StudioViewModel_SaveEndAndAdvance_AtLastAyahOfSurah_CompletesWithoutOverflow` : Clôture propre à la dernière ayah sans débordement.
14. `AnnotationTests.StudioViewModel_ManualAyahChange_SyncsRetakeTarget` : Synchronisation initiale de la cible de reprise.
15. `AnnotationTests.StudioViewModel_FormatTime_FormatsMinutesSecondsAccurately` : Formatage précis en mm:ss.ff.

---

## 3. Critères d'acceptation du plan

| Critère d'acceptation du plan | Constat / Preuve | Statut |
|---|---|---|
| Présentation conjointe lecteur + forme d'onde + Mushaf + ayah | Conforme dans StudioView.xaml | **VALIDÉ** |
| F7 début au curseur et F8 fin + suivante | Conforme avec préremplissage du début suivant | **VALIDÉ** |
| Clôture propre à la dernière ayah | Conforme (aucun index hors limites) | **VALIDÉ** |
| F9 reprise avec cible explicite et modifiable | Conforme dans le tiroir d'options avancées | **VALIDÉ** |
| Absence totale de modèle d'Awjuh | Strictement respecté | **VALIDÉ** |
| Annulation Ctrl+Z et incrément de révision | Conforme dans AnnotationService | **VALIDÉ** |
| Protections double-clic, TextBox et bornes | Conforme et testé | **VALIDÉ** |
| Import vérifiant l'empreinte SHA-256 de l'audio | Strictement rejeté si SHA-256 différent | **VALIDÉ** |

---

## 4. Conclusion

Le **Lot 6 (Studio d'annotation humain)** est intégralement validé.  
Prochaine étape : **Lot 7 — Préparation des images d’ayat** (L07-01 à L07-R).
