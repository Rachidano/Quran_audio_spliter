# Rapport d'analyse des corrections des ouvertures de sourates (L00-05)

Date : 11 octobre 2026  
Intervenant : Antigravity  
Tâche : **L00-05** — Lire les corrections des ouvertures de sourates et leur liaison aux empreintes des pages. Décrire quelles corrections sont réutilisables et lesquelles demandent une vérification.

---

## 1. Origine et mécanisme des corrections d'ouvertures

Dans la cartographie brute issue de l'API MARW (`ayat-shadows`), la première ayah de chaque sourate englobe souvent l'intégralité du haut de la page ou du bloc texte, incluant ainsi :
- Le cartouche ornemental portant le nom de la sourate et son lieu de révélation.
- La Basmala calligraphiée en tête de sourate (hors Al-Fatiha où elle est comptée).

Pour éviter que ces éléments décoratifs n'apparaissent dans l'image de l'ayah 1 lors de l'export vidéo, une revue visuelle assistée a été formalisée dans :
- Données : `src/Quran.Infrastructure/Data/marw-opening-review.json`
- Implémentation de référence : `src/Quran.Infrastructure/Services/MarwOpeningReview.cs`

Le mécanisme calcule un seuil vertical minimum (`minimumTop`) :
`minimumTop = FirstTextLine * 100.0 / LineCount`
qui est appliqué pour tronquer les rectangles de l'ayah 1 situés au-dessus de la ligne effective de texte.

---

## 2. Vérification cryptographique des 114 ouvertures

Le script `scratch/check_openings.py` a comparé les 114 entrées de `marw-opening-review.json` avec les fichiers réels du disque sous `data/mushaf/editions/marw-newMushaf/20260913-full/` :
- **Total des entrées** : 114 sourates.
- **Concordance `MapSha256`** : 114 / 114 (0 discordance).
- **Concordance `ImageSha256`** : 114 / 114 (0 discordance).
- **Nombre de lignes par page** : 7 lignes pour les pages 1 et 2 (Al-Fatiha et début d'Al-Baqarah) ; 15 lignes pour toutes les autres pages (pages 3 à 604).

---

## 3. Qualification des corrections : réutilisables vs nécessitant vérification

### 3.1 Corrections immédiatement réutilisables dans l'application Desktop
- **Découpage vertical des cartouches et basmalas** : Les 114 valeurs de `FirstTextLine` sont 100% valides et utilisables pour l'édition `newMushaf` révision `20260913-full`.
- **Liaison cryptographique stricte** : La règle liant l'application d'une correction au hash SHA-256 de la page et de la carte doit être conservée dans le moteur desktop (`AyahImageService`). Si une image change, la correction ne s'applique pas silencieusement.
- **Cas spécifiques validés** :
  - **Sourate 1 (الفاتحة)** : Page 1, `LineCount: 7`, `FirstTextLine: 1`. La ligne 0 (cartouche supérieur) est exclue ; la Basmala (ligne 1) est incluse dans l'ayah 1.
  - **Sourate 2 (البقرة)** : Page 2, `LineCount: 7`, `FirstTextLine: 1`. La ligne 0 (cartouche) est exclue ; le texte d'Alif-Lam-Mim commence à la ligne 1.
  - **Sourate 9 (التوبة)** : Page 187, `LineCount: 15`, `FirstTextLine: 1`. Pas de Basmala ; la ligne 0 (cartouche) est exclue, l'ayah 1 commence immédiatement à la ligne 1.
  - **Ouvertures en milieu de page** : Ex. Sourate 5 (المائدة) sur la page 106 (`FirstTextLine: 7` sur 15 lignes), Sourate 113 (الفلق) et 114 (الناس) sur la page 604 (`FirstTextLine: 6` et `10`).

### 3.2 Points nécessitant une vérification visuelle lors du prototype (Lots 1 et 7)
1. **Marges horizontales et liaisons partielles** :
   Le filtre actuel n'opère qu'un découpage vertical (`Y` / `top`). Si une première ayah commence au milieu d'une ligne ou après un ornement sur la même ligne horizontale, un simple seuil en `Y` ne suffit pas à isoler la largeur.
2. **Empiètement des signes diacritiques supérieurs/inférieurs** :
   Pour certaines ouvertures, des voyelles ou des signes de prolongation (madd) dépassent légèrement la limite théorique de la ligne. L'éditeur de masque manuel (prévu au lot 7) permettra à l'utilisateur d'ajuster finement les bornes sans dépendre d'une formule rigide.
3. **Statut éditorial** :
   Ces corrections représentent un guide technique de pré-découpage, pas une certification théologique. L'utilisateur garde le contrôle pour confirmer ou ajuster l'image avant l'export MP4.

---

## 4. Conclusion

La tâche **L00-05** est validée. Le fichier `marw-opening-review.json` est prêt à être intégré dans le socle de données du desktop comme table de corrections de référence pour la révision `20260913-full`.
