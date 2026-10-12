# Rapport d'examen du corpus candidat Warsh QPC v10 (L00-04)

Date : 11 octobre 2026  
Intervenant : Antigravity  
Tâche : **L00-04** — Examiner le corpus candidat `data/reference/warsh-qpc-v10/`, notamment ses différences de pages signalées. Conserver son statut candidat ; ne pas utiliser sa pagination pour remplacer la cartographie des images.

---

## 1. Origine et statut du corpus

- **Dépôt source** : `https://github.com/ibnhazm/KFGQPC` (Miroir communautaire des données Uthmanic Warsh de la King Fahd Glorious Quran Printing Complex).
- **Version des données** : Version 0.10 (Update 4.0, 2021-08-05).
- **Révision Git** : `25f2f8886d6ad0b47190f252d9d7fb1e296f3231`.
- **Empreinte SHA-256 (`source.json`)** : `F05D0DC652FD46B38563CACB13242F76F80DB4CC64D25873DA0BCE157253872F`.
- **Statut formel conservé** : **Candidat** (`CandidatePendingComparison`).
  - Ce fichier n'a pas été téléchargé directement d'un canal officiel de l'éditeur du Mushaf MARW.
  - Il constitue une excellente source textuelle pour la recherche, l'autocomplétion et l'aide à la saisie, mais ne possède pas de validation théologique exhaustive dans ce projet.

---

## 2. Concordance canonique et analyse des 62 écarts de pagination

L'analyse comparative entre `data/reference/warsh-qpc-v10/source.json` et les 604 pages de `data/mushaf/editions/marw-newMushaf/20260913-full/` (enregistrée dans `comparison.json`) met en évidence les constats suivants :

1. **Concordance intégrale des références canoniques (100%)** :
   - Nombre de versets : 6 214 versets exactement.
   - Les 114 sourates et tous les numéros d'ayat correspondent parfaitement sans omission ni doublon (`referenceSetMatches: true`).

2. **Divergences de pagination (62 différences au total)** :
   - **4 ayat à cheval sur 2 pages dans QPC** :
     - `4:44` (QPC: 85-86 | MARW: 86)
     - `20:86` (QPC: 317-318 | MARW: 318)
     - `24:36` (QPC: 354-355 | MARW: 355)
     - `24:42` (QPC: 355-356 | MARW: 356)
     Dans l'édition MARW (« newMushaf »), ces 4 ayat sont intégralement situées sur la page indiquée, sans coupure.
   - **58 décalages simples de page (±1 page)** :
     - Par exemple : `5:79` (QPC: 120 | MARW: 121), `6:132` (QPC: 145 | MARW: 144), `55:15` (QPC: 531 | MARW: 532).
     - Ces décalages résultent de différences typographiques et de mise en page entre le Mushaf imprimé de Médine (KFQC) et le Mushaf algérien (MARW).

---

## 3. Règle de conception confirmée pour l'application desktop

- **Principe de séparation stricte** :
  - La cartographie des pages, la pagination et les images réelles pour les vidéos sont gouvernées **exclusivement** par les fichiers de l'édition MARW (`data/mushaf/editions/marw-newMushaf/`).
  - La pagination de `source.json` (QPC) ne doit en aucun cas remplacer ni écraser les numéros de page de l'édition MARW.
  - Dans l'application desktop, le champ `page` du texte QPC sera réaligné ou masqué au profit du numéro de page canonique de l'édition MARW.
  - Le texte Uthmani de `source.json` servira pour la recherche de mots, la normalisation sans tashkeel et l'affichage d'assistance, tandis que l'image vidéo proviendra à 100% des pixels extraits des planches du Mushaf original.

---

## 4. Conclusion

La tâche **L00-04** est validée. Le statut de corpus candidat est acté et les 62 divergences de pagination sont comprises, documentées et neutralisées dans la conception du studio.
