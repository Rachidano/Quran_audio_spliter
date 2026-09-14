# Inventaire des sources pour les أوجه de Warsh

Mise à jour : voir [le dépouillement visuel](EXTRACTION.md) pour les dix branches et quatre contraintes extraites du chapitre Asbahani. Les états ci-dessous décrivent l’inventaire initial.

Recherche du 13 septembre 2026. Cette livraison est un inventaire documentaire, pas un référentiel exhaustif d’awjuh. Aucun nouveau وجه n’a été approuvé ni ajouté automatiquement aux quatre propositions existantes.

## Sources retrouvées

| Source | Utilisation envisagée | Accès vérifié |
|---|---|---|
| [الشاطبية](https://tajweed.quranpedia.net/document/show/1703), al-Shatibi | Texte de base du périmètre Shatibiyya | Notice consultée ; pièce jointe non lisible par l’outil web |
| [النشر](https://quranpedia.net/book/20635), Ibn al-Jazari | Arbre des transmissions, règles et cas particuliers ; distinguer citations et choix retenus | Notice consultée, [numérisation universitaire](https://sites.dlib.nyu.edu/viewer/books/columbia_aco000602/display?lang=ar) localisée ; pas de lecture intégrale |
| [Tableaux Azraq/Tayyiba](https://quranpedia.net/book/20887), Umar b. Yusuf Hammad | Extraire les dépendances entre performances et les colonnes de transmission | PDF de 3 pages téléchargé ; texte extrait mal encodé |
| [تنقيح كتاب التحرير](https://quranpedia.net/book/20899), Bashshar b. Mustafa Itani | Examiner les contraintes spécifiques de l’Asbahani | Chapitre de 5 pages téléchargé, commençant p. imprimée 181 ; début inspecté, non transcrit intégralement |
| [Étude des انفرادات de l’Asbahani](https://tafsir.net/researchs/12057) | Identifier les points qui nécessitent une décision critique explicite | Résumé consulté ; étude complète à examiner |
| [Étude du symbole ج dans la Tayyiba](https://jqrs.qurancomplex.gov.sa/wp-content/uploads/jqrs-docs/28/03-رمز-«الجيم»-لورش-في-طيبة-النشر.pdf) | Vérifier l’attribution avant de classer un passage sous Warsh/voie précise | Localisée par recherche seulement |

Les notices de bibliothèque ne suffisent pas pour transcrire les règles. La notice des tableaux présente notamment des années grégorienne/hégirienne à réconcilier avec l’original ; aucune date d’édition n’est déduite ici.

## Couverture réelle

- Azraq/Shatibiyya : quatre propositions secondaires déjà présentes ; inventaire des autres règles, du farsh et des contraintes non réalisé.
- Azraq/Tayyiba : tableaux trouvés ; aucune ligne promue en règle exploitable.
- Asbahani/Tayyiba : chapitre spécialisé trouvé ; aucune règle promue.
- Aucun chiffre global « tous les awjuh » n’est établi.

Les trois codes actuels sont des périmètres de classement, pas une description complète des sous-voies. Il faut représenter la chaîne et le livre source de chaque attribution. Une variante locale et une combinaison de plusieurs règles ne doivent pas être comptées comme des objets équivalents.

## Extraction à réaliser

1. Fixer l’édition et relever les numéros de pages imprimés et PDF.
2. Reconstituer visuellement les en-têtes et les cellules des tableaux ; ne pas importer leur texte illisible.
3. Enregistrer séparément règle, réalisation, contexte, exception et contrainte de combinaison. Conserver le lien entre chaque affirmation et sa source exacte.
4. Identifier les mots par leur texte et leur position dans l’édition de référence. Ne pas transférer automatiquement les numéros d’ayah d’un ouvrage vers MARW.
5. Consigner les désaccords des sources. Une extraction informatique reste une proposition jusqu’à revue spécialisée.

Pour déclarer un périmètre complet, chaque chapitre et chaque ligne de sa source choisie devront avoir un état explicite : intégré, hors périmètre motivé ou désaccord à résoudre. Le nombre d’entrées seul ne mesure pas l’exhaustivité.

## Fichiers

`source-inventory.json` : métadonnées, accès réellement obtenu, couverture et empreintes SHA-256.

`azraq-tayyiba-tables.pdf` et `asbahani-tahrirat.pdf` : copies de travail des pièces publiques, gardées dans la documentation de recherche et non publiées dans le site. Les liens publics ne présument pas d’une licence de redistribution.
