# Procès-verbal de recette du Lot 0 (L00-R)

Date : 11 octobre 2026  
Intervenant : Antigravity  
Lot : **Lot 0 — Figer les entrées et les cas de contrôle**

---

## 1. Synthèse de l'exécution des tâches du Lot 0

| Tâche | Libellé | Résultat technique | Preuve associée | Statut |
|---|---|---|---|---|
| **L00-01** | État Git initial et inventaire sans toucher au site | Git propre, master (5e98560), site intact, outils relevés (.NET 9, FFmpeg 8.0, Node 22, Python 3.10) | [L00-01_inventaire_initial.md](L00-01_inventaire_initial.md) | **TERMINÉ** |
| **L00-02** | Contrôle 604 paires PNG/JSON et manifeste | 604 JSON + 604 PNG présents, SHA-256 100% conformes, dimensions 1241x1754 unifiées, 6 214 versets, 0 erreur de bornes | [L00-02_controle_mushaf.md](L00-02_controle_mushaf.md) | **TERMINÉ** |
| **L00-03** | Métadonnées édition, 114 sourates, 6 214 refs, 285 Baqara | newMushaf / 20260913-full, 114 sourates sans trou, 6 214 refs canoniques, Al-Baqarah à 285 ayat | [L00-03_sourates_et_edition.md](L00-03_sourates_et_edition.md) | **TERMINÉ** |
| **L00-04** | Examen corpus candidat QPC v10 et 62 écarts | Statut candidat conservé, 6 214 refs identiques, 62 écarts de mise en page documentés et neutralisés | [L00-04_examen_corpus_qpc.md](L00-04_examen_corpus_qpc.md) | **TERMINÉ** |
| **L00-05** | Corrections ouvertures de sourates | 114 ouvertures vérifiées bit à bit (0 discordance SHA-256) ; découpage vertical prêt pour le studio | [L00-05_corrections_ouvertures.md](L00-05_corrections_ouvertures.md) | **TERMINÉ** |
| **L00-06** | Fichiers MP3 réels et inventaire 114 URLs | 114 URLs cataloguées ; 3 MP3 complets réels (1, 2, 112) analysés avec FFprobe 8.0 ; 1 partiel (.tmp) | [L00-06_audit_audio.md](L00-06_audit_audio.md) | **TERMINÉ** |
| **L00-07** | SDK .NET 9 fixé, PC recette, installateur Inno Setup | `desktop/global.json` fixé à 9.0.312, Inno Setup 6 opérationnel avec support arabe RTL (testé avec succès) | [L00-07_environnement_et_packaging.md](L00-07_environnement_et_packaging.md) | **TERMINÉ** |
| **L00-08** | Manifeste de cas réels de contrôle | 13 cas réels emblématiques répertoriés en JSON et Markdown, prêts pour le prototype et les tests | [L00-08_manifeste_cas_reels.md](L00-08_manifeste_cas_reels.md) | **TERMINÉ** |

---

## 2. Vérification des critères de recette du Plan d'implémentation

1. **Inventaire reproductible avec erreurs bloquantes distinctes des avertissements** : Satisfait. Aucune erreur bloquante. Avertissements sur le statut candidat du texte QPC et sur le caractère partiel du téléchargement audio consignés.
2. **Matrice de cas représentatifs (>= 12 cas)** : Satisfait. 13 cas définis avec précision (début/fin de ligne, milieu de ligne, plusieurs lignes, marqueur en bordure, basmala, sourate 9, Fatiha, Al-Ikhlas, longue ayah 2:282, etc.).
3. **Cas traversant des pages** : Vérification effectuée. L'édition MARW dispose de pages commençant et finissant toujours par une frontière d'ayah (mise en page de type huffaz). Les divergences avec les 4 cas multipages de QPC sont documentées.
4. **Intégrité du site** : Vérifié par `git status`. Aucun fichier sous `src/`, `tests/` ou `data/` n'a été altéré.

---

## 3. Clôture du Lot 0

Le **Lot 0 est clôturé avec succès**.  
Le projet peut entrer dans le **Lot 1 — Prototype de faisabilité prioritaire** (extraction graphique réelle, découpage audio précis et génération des premiers fichiers MP4).
