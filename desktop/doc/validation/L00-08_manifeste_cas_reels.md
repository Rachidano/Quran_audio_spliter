# Manifeste de cas réels de contrôle (L00-08)

Date : 11 octobre 2026  
Intervenant : Antigravity  
Tâche : **L00-08** — Préparer un manifeste de cas réels : sourate/ayah, page(s), type de difficulté, source image, source audio si disponible, résultat attendu et état de revue.

---

## 1. Objectif du manifeste

Ce manifeste rassemble 13 cas réels emblématiques issus directement du corpus existant (`data/mushaf/editions/marw-newMushaf/20260913-full/`). Il couvre l'ensemble des défis typographiques, spatiaux et sonores nécessaires à la qualification du prototype (lot 1), du studio d'annotation (lot 6), du moteur d'extraction d'images (lot 7) et de l'export vidéo MP4 (lot 8).

---

## 2. Matrice des 13 cas réels de contrôle

| ID | Référence | Sourate | Page | Type de difficulté | Source image / JSON | Source audio | Résultat attendu | État |
|---|---|---|---|---|---|---|---|---|
| **CASE-01** | `1:1` | الفاتحة | 1 | Ouverture avec Basmala (grille 7 lignes) | `page-001.png` / `page-001.json` | `001/original.mp3` | Cartouche exclu (l.0) ; Basmala incluse (l.1) | À VALIDER |
| **CASE-02** | `1:7` | الفاتحة | 1 | Fin de sourate multiligne en bas de page | `page-001.png` / `page-001.json` | `001/original.mp3` | 2 lignes préservées sans décalage, médaillon (7) intact | À VALIDER |
| **CASE-03** | `2:1` | البقرة | 2 | Ouverture Alif-Lam-Mim avec cartouche et basmala | `page-002.png` / `page-002.json` | `002/original.mp3` | Cartouche et Basmala exclus ; الم seul avec médaillon (1) | À VALIDER |
| **CASE-04** | `2:2` | البقرة | 2 | Début d'ayah partageant une ligne (ذلك الكتاب) | `page-002.png` / `page-002.json` | `002/original.mp3` | Découpage horizontal strict ; aucun mot de 2:1 ni 2:3 | À VALIDER |
| **CASE-05** | `2:4` | البقرة | 2 | Ayah moyenne (2 lignes) avec blanc final | `page-002.png` / `page-002.json` | `002/original.mp3` | Préservation du blanc de ligne sans recentrage forcé | À VALIDER |
| **CASE-06** | `2:255` | البقرة | 42 | Ayat al-Kursi (9 lignes complètes) | `page-042.png` / `page-042.json` | `002/original.mp3` | Bloc compact harmonieux ; netteté 1080p | À VALIDER |
| **CASE-07** | `2:282` | البقرة | 48 | Ayat al-Dayn (15 lignes, page entière) | `page-048.png` / `page-048.json` | `002/original.mp3` | Pleine page sans ornement ; arbitrage lisibilité / multiscène | À VALIDER |
| **CASE-08** | `5:1` | المائدة | 106 | Ouverture en milieu de page (ligne 7) | `page-106.png` / `page-106.json` | URL archive.org | Sourate 4 exclue en haut ; cartouche et basmala exclus | À VALIDER |
| **CASE-09** | `9:1` | التوبة | 187 | Ouverture sans Basmala | `page-187.png` / `page-187.json` | URL archive.org | Cartouche exclu (l.0) ; début immédiat à براءة | À VALIDER |
| **CASE-10** | `55:13` | الرحمن | 531 | Refrain répété (فبأي آلاء ربكما تكذبان) | `page-531.png` / `page-531.json` | URL archive.org | Isolation propre ; test des répétitions audio du studio | À VALIDER |
| **CASE-11** | `112:1` | الإخلاص | 604 | Première ayah sur page multi-sourates (haut) | `page-604.png` / `page-604.json` | `112/original.mp3` | Cartouche et basmala exclus ; قل هو الله أحد isolé | À VALIDER |
| **CASE-12** | `112:4` | الإخلاص | 604 | Dernière ayah suivie du cartouche d'Al-Falaq | `page-604.png` / `page-604.json` | `112/original.mp3` | Aucun pixel du cartouche 113 sous la ligne | À VALIDER |
| **CASE-13** | `114:6` | الناس | 604 | Toute dernière ayah du Mushaf | `page-604.png` / `page-604.json` | URL archive.org | Numéro final (6) intact, ornements de clôture exclus | À VALIDER |

---

## 3. Données brutes archivées

Le manifeste complet sous format JSON est sauvegardé dans :  
`desktop/doc/validation/L00-08_manifeste_cas_reels.json`.

---

## 4. Conclusion

La tâche **L00-08** est validée. Ce jeu de 13 cas servira de banc d'essai systématique pour chaque lot ultérieur, à commencer par le prototype du lot 1.
