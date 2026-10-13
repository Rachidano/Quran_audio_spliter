# Rapport de Recette L00 — Point de départ V2

- **Date** : 13 octobre 2026
- **Auteur** : Antigravity
- **Référence** : [tasks_v3.md](../../tasks_v3.md), [plan d’implémentation_v3.md](../../plan%20d%27impl%C3%A9mentation_v3.md)

---

## 1. État Git et versions (V3-00-01)

- **Branche** : `master` synchronisée avec `origin/master`.
- **Dernier commit** : `ec34e02` — *feat(desktop): v2.0.0 - Gestion vidéos (suppr/renom), export MP3, orientation & filtre par sourate, recherche textuelle intégrale*.
- **Tag officiel** : `v2.0.0` créé et poussé sur GitHub.
- **Fichiers suivis** : Arborescence `desktop/` exclusivement. Le site web et les répertoires externes restent strictement intouchés.

---

## 2. Exécution de la suite de tests unitaires V2 (V3-00-02)

- **Commande** : `dotnet test desktop/QuranVideo.sln --logger "trx;LogFileName=v2_baseline_tests.trx" --results-directory "desktop/doc/validation/v3"`
- **Résultat** : **97 réussis**, 0 échec, 0 ignoré.
- **Durée** : ~9 secondes.
- **Fichier de preuve** : [v2_baseline_tests.trx](v2_baseline_tests.trx).
- **Couverture des domaines V2** :
  - Géométrie des versets (5 tests)
  - Espace de travail & verrous (5 tests)
  - Import et intégrité du corpus (4 tests)
  - Normalisation & recherche textuelle plein texte (17 tests)
  - Moteur audio et flux NAudio (4 tests)
  - Annotations & CRUD passages (19 tests)
  - Recettes et masques d'images (7 tests)
  - Composition MP4 (5 tests)
  - Sauvegarde et restauration (2 tests)
  - Liens audios & catalogue 114 sourates (12 tests)
  - Gestion des vidéos & renommage (4 tests)
  - Exportation en masse dédupliquée & audio MP3 (8 tests)
  - Navigation et Shell (5 tests)

---

## 3. Contrôle des cinq évolutions V2 (V3-00-03)

1. **Gestion des vidéos (فيديوهاتي)** : Suppression physique confirmée, renommage avec filtre de caractères interdits, support visuel MP4 et MP3.
2. **Export en masse (التصدير بالدفعة)** : Orientation 16:9 / 9:16 exclusive bidirectionnelle, filtrage dynamique par sourate sélectionnée pour éviter toute confusion.
3. **Extraction audio seule (MP3)** : Moteur `ExtractAyahAudioAsync` opérationnel sans rendu vidéo lourd dans le Studio et l'Export en masse.
4. **Recherche textuelle intégrale** : Table `ayah_texts` alimentée par `source.json` embarqué, double normalisation Alif Khanfariya opérationnelle.
5. **Studio — Arrêt d'annotation** : Verrou de segment borné dans NAudio arrêtant la lecture à `EndTimeSec` sans continuer la sourate.

---

## 4. Inventaire et copie de sauvegarde témoin (V3-00-04)

Un instantané représentatif de l'espace de travail V2 réel a été copié dans :
`desktop/doc/validation/v3/baseline_workspace_v2/`

### Fichiers sauvegardés :
- `data/quran.db` (3 932 160 octets)
- `config.json`
- `data/audio_settings.json`

### Inventaire des tables et données existantes :
| Table | Lignes réelles | Rôle | Statut pour V3 |
|---|---|---|---|
| `mushaf_editions` | 1 | Édition officielle (Mushaf d'Algérie / Warsh) | À préserver |
| `mushaf_pages` | 604 | Métadonnées des 604 pages | À préserver |
| `surahs` | 114 | Les 114 sourates | À préserver |
| `ayahs` | 6 214 | Les 6 214 versets | À préserver |
| `ayah_regions` | 12 782 | Coordonnées de découpe des boîtes | À préserver |
| `ayah_texts` | 6 214 | Textes intégraux normalisés | À préserver |
| `reciters` | 2 | Récitateurs (Cheikh Saber Abdelhakam, etc.) | À préserver |
| `recitation_collections` | 2 | Collections de récitation | À préserver |
| `audio_sources` | 2 | Sources audios enregistrées | À préserver |
| `audio_download_links` | 228 | Liens personnalisés des 114 sourates | À préserver |
| `audio_catalog_preferences` | 1 | Récitateur actif | À préserver |
| `ayah_image_recipes` | 47 | Marges et révisions d'images ajustées | À préserver |
| `ayah_passages` | 2 | Annotations temporelles utilisateurs | À préserver |
| `video_compositions` | 2 | Compositions unitaires existantes | À préserver |
| `batch_export_jobs` | 2 | File d'attente d'exports | À préserver |

---

## 5. Synthèse et feu vert pour L01 (V3-00-05)

- Aucune anomalie bloquante identifiée.
- La base témoin est isolée pour tester la migration non destructive du schéma dans le lot `L01`.
- Le point de départ est stable, propre et reproductible.
