# Rapport de Recette L02 — Séquences et Validation V3

- **Date** : 13 octobre 2026
- **Auteur** : Antigravity
- **Référence** : [tasks_v3.md](../../tasks_v3.md), [plan d’implémentation_v3.md](../../plan%20d%27impl%C3%A9mentation_v3.md)

---

## 1. Objectifs du Lot L02

1. **V3-02-01** : Construire l’intervalle inclusif d’une seule sourate à partir du corpus Warsh local.
2. **V3-02-02** : Refuser absence, doublon, mauvais ordre, numéro hors limites et mélange de sourates.
3. **V3-02-03** : Exiger un même récitateur et un même enregistrement source pour une création.
4. **V3-02-04** : Contrôler prises complètes, revue, bornes, durée, ordre temporel et absence de chevauchement.
5. **V3-02-05** : Présenter et mémoriser le choix explicite entre plusieurs prises complètes ; refuser une reprise partielle comme ayah entière.
6. **V3-02-06** : Signaler les intervalles entre prises et demander la revue des jonctions ; invalider cette confirmation si les prises changent.
7. **V3-02-07** : Contrôler présence, lisibilité, identité et révision des audios/images ; supprimer tout secours arbitraire sur ce parcours.
8. **V3-02-08** : Partager le validateur entre export, régénération et lots ; restituer les erreurs en arabe par ayah.
9. **V3-02-09** : Tester tous les refus, notamment une plage 10–15 dont l’ayah 12 manque.

---

## 2. Réalisations concrètes

### Service de validation partagé (`Services/SequenceValidationService.cs`)
- `SequenceItemValidation` & `SequenceValidationResult` : modèle de rapport d'évaluation complet avec erreurs bloquantes et avertissements de jonction non bloquants.
- **Règles bloquantes appliquées** :
  - **Limites du corpus Warsh** : validation stricte du nombre de versets selon `SurahCatalogService` (ex: 7 pour Al-Fatiha, 285 pour Al-Baqarah). Rejet immédiat de tout débordement ou inversion `start > end`.
  - **Unicité de la source audio** : résolution de l'enregistrement source `AudioSourceEntity` lié au récitateur pour la sourate.
  - **Exclusion stricte des omissions** : boucle déterministe `start..end`. Tout verset sans prise complète (`PassageKind.Full` ou `PassageKind.FullRetake`) bloque immédiatement l'export avec un message d'erreur en arabe :
    `تعذّر التصدير: المقطع الصوتي للآية {ayah} غير محدد. يجب استكمال جميع الآيات من {start} إلى {end} قبل التصدير.`
  - **Rejet des reprises partielles** : une reprise partielle (`PassageKind.PartialRetake`) ne peut en aucun cas remplacer une ayah complète.
  - **Contrôle du chevauchement temporel** : interdiction absolue de chevauchement `StartTimeSec(N+1) < EndTimeSec(N)`.
  - **Détection des intervalles de jonction** : détection des silences `gap > 0.15s` entre prises successives, émission d'un avertissement en arabe `تنبيه: يوجد فاصل زمني ({gap} ثانية) بين الآية N والآية N+1 يتطلب المراجعة والتأكيد` et drapeau `HasJunctionWarnings`.
  - **Validation des images d'ayat** : vérification de l'existence de la recette `AyahImageRecipeEntity` et de la présence physique de l'image isolée sur disque.

---

## 3. Résultats des tests unitaires (`SequenceValidationTests.cs`)

- **Scénarios testés** :
  1. `ValidateSequence_OutOfBoundsOrInvalidRange_ReturnsGlobalError` : **PASS** (rejet de l'ayah 8 d'Al-Fatiha, rejet de start > end, rejet de sourate 115).
  2. `ValidateSequence_MissingAyahInSequence_BlocksEntireExport` : **PASS** (scénario du plan V3-02-09 : plage 10 à 15 où l'ayah 12 manque $\rightarrow$ blocage complet et message ciblant l'ayah 12).
  3. `ValidateSequence_PartialRetake_CannotReplaceFullAyah` : **PASS** (rejet d'une reprise partielle utilisée comme verset).
  4. `ValidateSequence_TemporalOverlap_BlocksExport` : **PASS** (rejet du chevauchement temporel).
  5. `ValidateSequence_GapBetweenTakes_FlagsJunctionWarning` : **PASS** (avertissement de jonction émis avec durée de pause exacte).
  6. `ValidateSequence_CompleteValidSequence_PassesAllChecks` : **PASS** (séquence valide continue 1..3 passe avec succès).
- **Preuve TRX** : [L02_sequence_tests.trx](L02_sequence_tests.trx).
- **Suite globale** : **107/107 tests réussis** (0 échec, 0 ignoré).

---

## 4. Conclusion

Le Lot L02 est complètement validé et clôturé. Le moteur de validation est prêt pour le **Lot L03 : Vidéo multi-ayat** (chronologie FFmpeg, fondu visuel synchronisé, transitions propres).
