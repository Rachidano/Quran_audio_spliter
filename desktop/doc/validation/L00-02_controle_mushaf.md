# Rapport de contrôle d'intégrité du Mushaf (L00-02)

Date : 11 octobre 2026  
Intervenant : Antigravity  
Tâche : **L00-02** — Contrôler les 604 paires PNG/JSON et le manifeste de `data/mushaf/editions/marw-newMushaf/20260913-full/` : présence, taille, SHA-256, dimensions et références d’ayat.

---

## 1. Périmètre et méthodologie du contrôle

Le contrôle a été exécuté par recalcul intégral et direct sur le disque (`scratch/audit_lot0_mushaf.py`), sans se fier à des états préexistants :
- **Fichiers ciblés** : 604 fichiers PNG (`page-001.png` à `page-604.png`), 604 fichiers JSON (`page-001.json` à `page-604.json`), et `manifest.json`.
- **Contrôles cryptographiques** : Calcul du hash SHA-256 de chaque fichier et comparaison avec l'entrée correspondante dans `manifest.json`.
- **Contrôles physiques et binaires** : Vérification de la signature PNG (`0x89504E470D0A1A0A`), lecture des dimensions dans le chunk IHDR, et cumul de la taille en octets.
- **Contrôles géométriques et structurels** : Décodage JSON UTF-8-BOM, contrôle `page == n`, `mushafId == 'newMushaf'`, calcul des coordonnées absolues normalisées des rectangles (`cropBox → textBox → rects`), vérification de l'absence de coordonnées hors cadre (`[0, 1]`).
- **Contrôles de numérotation** : Vérification de la séquence globale `verseNo` et des références `(surah, ayah)`.

---

## 2. Résultats détaillés

| Critère | Valeur attendue | Valeur observée | Statut |
|---|---|---|---|
| Fichiers JSON présents | 604 | 604 | Conforme |
| Fichiers PNG présents | 604 | 604 | Conforme |
| Fichiers manquants | 0 | 0 | Conforme |
| Entrées dans `manifest.json` | 1 208 | 1 208 | Conforme |
| Concordance SHA-256 (JSON) | 604 / 604 | 604 / 604 (0 discordance) | Conforme |
| Concordance SHA-256 (PNG) | 604 / 604 | 604 / 604 (0 discordance) | Conforme |
| Taille cumulée PNG | 139 575 979 octets | 139 575 979 octets | Conforme |
| Taille cumulée JSON | 2 146 542 octets | 2 146 542 octets | Conforme |
| Signature binaire PNG | 604 / 604 valides | 604 / 604 valides | Conforme |
| Dimensions des images PNG | Unifiées | **1241 × 1754 px** pour les 604 pages | Conforme |
| Nombre total de lignes d'ayat | 6 214 | 6 214 | Conforme |
| Séquence `verseNo` | 1 à 6 214 consécutifs | 1 à 6 214 sans saut ni doublon | Conforme |
| Rectangles hors cadre (`boundsErrors`) | 0 | 0 (toutes les coordonnées dans `[0.0, 1.0]`) | Conforme |

---

## 3. Constat structurel majeur : découpage des pages

Le parcours complet des 604 pages révèle que :
1. Chaque page contient un nombre entier d'ayat.
2. Il y a exactement 6 214 lignes d'ayat pour les 604 pages, ce qui correspond exactement au nombre total d'ayat du Coran pour cette édition.
3. Le champ `continuousFromPrevPage` est à `false` sur l'ensemble des 6 214 entrées : dans cette édition du Mushaf (« newMushaf »), les pages commencent et s'achèvent toujours sur des frontières d'ayat (mise en page de type huffaz).
4. Les divergences de pagination signalées avec le corpus QPC (qui comporte des mentions telles que `4:44` sur `85-86`) proviennent d'une différence de mise en page entre le Mushaf de Médine (KFQC) et ce Mushaf algérien/maghrébin.

---

## 4. Données brutes archivées

Le fichier JSON complet de synthèse d'audit est disponible sous :  
`desktop/doc/validation/L00-02_mushaf_audit_result.json`.

---

## 5. Conclusion

Tous les critères de la tâche **L00-02** sont validés avec succès et intégrité parfaite. La ressource graphique et spatiale est 100% opérationnelle pour servir de socle à l'extraction pixel du lot 1.
