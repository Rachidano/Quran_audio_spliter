# Revue visuelle des 114 débuts de sourate

Revue réalisée par Codex le 13 septembre 2026 sur les PNG originaux de `MARW newMushaf`, instantané `20260913-full`. Périmètre : le premier verset de chaque sourate, son début, ses lignes de texte et son marqueur de fin.

## Résultat

Les 114 débuts ont été examinés sur 29 planches. Les dix corrections précédentes sont confirmées ; les 104 autres sélections nécessitaient de retirer un titre, une basmala ou un espace précédent. Les premières lignes de texte sont identifiables dans les 114 cas ; aucun cas n'a été laissé sans décision sur cette limite verticale. Ce résultat constitue une revue graphique assistée, pas une certification religieuse ou une validation exhaustive du texte et des diacritiques.

Les corrections conservent la géométrie horizontale et les marqueurs MARW. Elles coupent les rectangles à la première ligne de texte observée. Un rectangle intermédiaire peut contenir à la fois l'en-tête et plusieurs lignes du verset : sa partie textuelle est conservée, plutôt que de supprimer tout le rectangle.

Cas particuliers examinés : première page à sept lignes ; 2:1 sur deux lignes ; 9:1 sans basmala ; 4:1 sur trois lignes ; 60:1 et 65:1 sur cinq lignes ; marqueur seul sur la ligne suivante ; plusieurs débuts de sourate sur une même page (notamment 599–604).

## Preuves et limites

Les 29 planches avant correction se trouvent dans `artifacts/marw-openings/`, les 29 planches après dans `artifacts/marw-openings-after/`. Les 114 sélections avant ont été examinées visuellement. Après correction, les planches 1, 3, 15, 17, 27 et 29 ont été réexaminées visuellement (22 débuts), et les 114 ont passé les contrôles de géométrie décrits ci-dessous.

Les rectangles de ligne peuvent encore contenir des espaces latéraux ou des ornements : ce ne sont pas des contours de lettres. La présente revue ne valide ni les 6 100 autres ayat, ni le corpus textuel, ni les associations audio. Les délimitations autres que le premier verset restent à qualifier séparément.

## Données appliquées

Le fichier `src/Quran.Infrastructure/Data/marw-opening-review.json` contient les 114 observations avec numéro de sourate, page, nombre de lignes de la zone MARW, indice de la première ligne de texte et SHA-256 de l'image et de la carte. Il est embarqué dans l'assembly .NET. Le serveur refuse d'appliquer une correction si les fichiers ne correspondent pas aux empreintes relues.

Les indices de ligne commencent à zéro dans le JSON. La galerie affiche une numérotation commençant à un. Les observations manuelles sont conservées dans `scripts/marw-reviewed-start-lines.json` ; `scripts/build-marw-opening-review.py` construit le paquet à partir de ces observations, sans inférer les limites depuis le numéro ou la fin des versets.

Le rapport structuré actif est consultable dans le site : <http://127.0.0.1:5098/mushaf/review?handler=Report>. La [galerie](http://127.0.0.1:5098/mushaf/review) distingue la revue assistée de la contre-vérification personnelle. Les notes et statuts du navigateur ne sont pas remplacés par des validations automatiques.

## Contrôles reproductibles

- `scripts/check-marw-opening-review.cjs` : 114 limites de début et de fin, sélection des premières lignes et marqueurs, exclusion de l'en-tête ; comparaison numérique des 6 100 autres tracés avec les cartes source, tous inchangés. Résultats : `artifacts/marw-opening-checks.json`.
- `scripts/check-marw-full.cjs` : 604 images vérifiées par SHA-256, 6 214 références, navigation et mobile.
- `scripts/check-marw-gallery.cjs` : galerie, rapport actif, sauvegarde personnelle, export et invalidation des annotations liées à un ancien tracé.
- 33 tests unitaires sélectionnés réussis, dont le refus d'une correction sur une autre image, carte ou page.

Compilation : `dotnet build src/Quran.Web/Quran.Web.csproj -o artifacts/marw-openings-review`. Les 11 avertissements de nullabilité du service historique `MushafImageService` persistent.
