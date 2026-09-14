# Qualification initiale de la source MARW

Examen le 13 septembre 2026 du lien fourni par l'utilisateur :
https://mushaf.marw.dz/web/ar/mushaf/newMushaf/1?surah=1&ayah=4&menu=masahif

## Résultat

L'édition `newMushaf` constitue un candidat concret pour remplacer le graphisme du pilote. Les pages PNG observées présentent les points orientaux souhaités. Une carte de rectangles par ayah est disponible publiquement pour le navigateur : il n'est pas nécessaire de reconstruire ces coordonnées à partir d'OCR ou de marqueurs pour commencer.

Le site annonce pour cette édition : Warsh de Nafi par la voie d'al-Azraq, décompte médinois final de 6214 ayat, 604 pages, 15 lignes dans les pages ordinaires. Ces informations viennent des libellés de son propre client JavaScript ; l'identification bibliographique complète de « Nouveau mushaf » reste à compléter. Ne pas généraliser ce décompte aux autres éditions du site : les libellés Shirifi annoncent un décompte différent.

## Ressources observées

- Image : `GET https://mushaf.marw.dz/web/api/v1/masahif/newMushaf/pages/2.png`
- Carte : `GET https://mushaf.marw.dz/web/api/v1/masahif/newMushaf/ayat-shadows/2`
- Les premières cartes sont également embarquées dans le script JSON `initial-state` de la page HTML.
- Prévisualisation de texte : `POST https://mushaf.marw.dz/web/api/v1/quran/verses/preview`, corps `{"mushafId":"newMushaf","verseNos":[1,2,3]}`. Il s'agit d'une lecture, pas d'une mutation du site.

Ces chemins ont été observés dans les ressources chargées par le lecteur. Ils ne sont pas présumés constituer une API publique stable contractuellement.

Trois images et leurs cartes sont archivées localement, ainsi que la carte de la dernière page, quelques prévisualisations textuelles et les ressources HTML/JS examinées. Aucun téléchargement complet des 604 images n'a été effectué. Le manifeste conserve les URL et empreintes des six fichiers du pilote de comparaison. Les images originales ne sont pas modifiées ; leur transparence est affichée sur fond blanc.

## Délimitations

Chaque réponse contient `boxes.cropBox`, `boxes.textBox`, `rows`, `rects`, `surah`, `ayah`, `verseNo` et `continuousFromPrevPage`.

Les rectangles sont exprimés en pourcentage de la zone de texte, elle-même relative au recadrage. Pour obtenir des coordonnées normalisées sur l'image originale :

```text
x = (crop.left + crop.width × (text.left + text.width × rect.left / 100) / 100) / 100
y = (crop.top  + crop.height × (text.top + text.height × rect.top / 100) / 100) / 100
w = crop.width × text.width × rect.width / 1 000 000
h = crop.height × text.height × rect.height / 1 000 000
```

Les 22 références des trois premières pages sont 1:1–1:7, 2:1–2:4 et 2:5–2:15. Elles concordent avec les références graphiques du pilote Quran.ws sur cet échantillon, sans que cela prouve l'identité de toutes les autres pages. Tous les rectangles transformés de cet échantillon tiennent dans leurs images. La carte 604 est également accessible ; cela ne constitue pas un audit de toutes les pages intermédiaires.

Une sélection peut regrouper plusieurs rectangles. Les cas `continuousFromPrevPage` devront être préservés lors d'un import complet. Les premières zones peuvent inclure un espace de basmala ou d'en-tête conformément au comportement source ; elles restent à examiner dans la validation graphique.

## Incohérence textuelle détectée

L'appel de prévisualisation avec `mushafId=newMushaf` renvoie notamment :

- `verseNo=1`, sourate 1, ayah 1 : basmala ;
- `verseNo=8`, sourate 2, ayah 1 : `الم` seul.

Sur la page graphique 2, le marqueur 1 termine un passage plus long, et la zone 2 couvre le passage commençant par `الذين يؤمنون`. Le texte de prévisualisation ne correspond donc pas aux mêmes limites que l'image/carte observée. Il ne faut pas l'utiliser comme corpus Warsh validé ni comme fondement du raccordement audio. La cause interne de ce décalage n'a pas été déterminée.

Cette observation porte sur l'endpoint interrogé et l'échantillon reçu ; elle ne permet pas de conclure à une erreur de l'image du mushaf ou de toutes les fonctions du site.

## Comparaison locale

Ouvrir [comparison.html](comparison.html). Choisir page 1, 2 ou 3, puis cliquer sur une référence ou une zone à droite. À gauche figure le graphisme du pilote actuel ; à droite, l'image MARW avec sa propre carte. Aucun texte coranique n'est recomposé et aucun audio n'est associé.

Reconstruction hors réseau : `python scripts/build-marw-comparison.py`. Le script n'utilise que la bibliothèque standard Python. Le résultat normalisé est également disponible dans `normalized-sample.json`.

## Décision proposée

Privilégier le couple **images MARW newMushaf + cartes MARW correspondantes** pour le prochain pilote. Conserver le paquet Quran.ws comme référence de comparaison. Réutiliser le composant image + surcouche du projet .NET, en ajoutant un adaptateur de ce format.

Avant bascule : vérifier visuellement les 22 références, identifier complètement l'édition, qualifier un texte compatible indépendamment de l'API preview, puis préparer les correspondances audio. La numérotation historique de la base ne sera pas réaffectée automatiquement.

Les ressources sont accessibles au navigateur, mais aucune licence explicite de redistribution par un tiers n'a été identifiée dans les éléments examinés. La mention d'une validation ministérielle ne suffit pas à établir ces conditions. Les fichiers locaux et le comparatif restent un échantillon de recherche ; aucune publication extérieure ni demande à un tiers n'a été effectuée.

Le site .NET actuellement lancé, sa base et ses associations audio n'ont pas été modifiés par cette recherche.
