# Revue des délimitations — 13 septembre 2026

Mise à jour : la revue des 114 débuts est terminée. Le [rapport complet](opening-review-full.md) remplace l'état partiel décrit ci-dessous. Les dix exceptions initiales ont été remplacées par des limites explicites pour chaque sourate, liées aux empreintes exactes des fichiers source.

## Galerie de contrôle

Ouvrir <http://127.0.0.1:5098/mushaf/review> : 114 fiches construites depuis le corpus actif, chacune avec le PNG original, le premier verset surligné, les liens vers le lecteur et MARW, un statut et une observation. Recherche par numéro/nom arabe/page, filtre de statut et masquage du surlignage disponibles.

Toutes les fiches commencent « À examiner » : la galerie n'est pas un rapport de validation automatique. Statuts et notes sont sauvegardés dans le navigateur, sans écriture en base ; le bouton d'export produit un rapport JSON complet. La sauvegarde est propre à l'origine (hôte/port) utilisée. Si l'image ou le tracé change, l'ancienne validation n'est plus appliquée. Exporter avant de changer de navigateur ou d'effacer ses données.

Contrôle reproductible : `scripts/check-marw-gallery.cjs` (Playwright). Vérifie 114 fiches, filtres, visibilité du surlignage, persistance des notes, export, invalidation d'une ancienne empreinte et largeur mobile. Ce test ne valide pas le contenu religieux des sélections.

Source locale examinée : `data/mushaf/editions/marw-newMushaf/20260913-full/`, PNG et JSON originaux avec manifeste SHA-256.

## Diagnostic

Sur 113:1, la carte source contient trois rectangles : fin vide de la ligne précédente, titre/basmala, puis texte du verset. La conversion crop → texte → image n'est pas responsable du débordement. Les rectangles décrivent une plage entre marqueurs, qui peut traverser des éléments sans texte d'ayah.

## Corrections locales explicites

Dans `MarwPilotCorpus`, les cas suivants sont corrigés uniquement au moment de construire la surcouche. Les PNG, les JSON archivés et les références ne sont pas modifiés.

| Page | Référence | Traitement vérifié sur l'image |
| --- | --- | --- |
| 1 | 1:1 | Garder le rectangle final ; exclure la basmala |
| 2 | 2:1 | Exclure seulement le premier rectangle de basmala ; conserver les deux lignes du verset |
| 50 | 3:1 | Garder le rectangle final ; exclure titre et basmala |
| 106 | 5:1 | Garder le rectangle final ; exclure titre et basmala |
| 603 | 109:1, 110:1, 111:1 | Garder le rectangle final ; exclure espaces précédents, titres et basmala |
| 604 | 112:1, 113:1, 114:1 | Garder le rectangle final ; exclure espaces précédents, titres et basmala |

Ce sont dix corrections propres à cet instantané. Ne pas généraliser la suppression des premiers rectangles à toutes les sourates : des premiers versets comportent plusieurs lignes. Les zones restent des rectangles de ligne, pas des contours de glyphes ; les marges latérales peuvent contenir des espaces ou des ornements.

## Vérification

`scripts/check-marw-regions.cjs` contrôle des positions repérées sur l'image originale : texte sélectionnable, titres et fin vide précédente exclus sur 604 ; seuils de séparation des en-têtes pour les autres corrections ; conservation du verset multiligne 2:1. Il produit les captures dans `artifacts/marw-region-review/` pour revue visuelle.

Échantillon examiné : pages 1, 2, 3, 50, 106, 300, 603 et 604. Les sélections 2:5 (page 3) et 18:53 (page 300) suivent les lignes et marqueurs observés sans correction. Cela ne vaut pas validation de toutes les ayat de ces pages ni du corpus entier.

Compilation et contrôles navigateur réussis. `scripts/check-marw-full.cjs` vérifie également les 604 images servies par SHA-256, les 6 214 références, la navigation et le mobile après correction. Les autres débuts de sourate restent à examiner avant qualification graphique complète. Aucun raccordement audio ou import de texte n'est réalisé par cette modification.
