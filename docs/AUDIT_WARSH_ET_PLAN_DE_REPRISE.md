# Audit Warsh et plan de reprise

Analyse du dossier le 12 septembre 2026. Ce document propose une reprise ; il ne certifie pas le corpus et ne remplace pas les spécifications existantes. Aucun import en base ni remplacement des images en service n'a été effectué.

## Conclusion

La base .NET/Razor/PostgreSQL et l'éditeur de segments sont réutilisables. Le point bloquant est la cohérence du référentiel : édition graphique, système de décompte des ayat, texte et coordonnées doivent être identifiés ensemble. Télécharger une image plus nette ne résoudra pas les associations incorrectes.

Preuve visuelle : `data/mushaf/masters/warsh/page_002.jpg` porte le numéro imprimé **١٢ (12)** et montre un passage avancé d'Al-Baqara. Le code l'associe pourtant au début de cette sourate. L'ancienneté bibliographique du scan n'a pas été établie ; l'erreur de page, elle, est directement observable.

## Lecture des quatre documents de projet

| Document | À conserver | À compléter ou corriger |
|---|---|---|
| `specifications_fonctionnelles_techniques_quran_warsh_net9.md` | Séparation texte/audio/application, recherche, validation avant publication, progression Fatiha puis Baqara | Définir le référentiel et son système de décompte ; une référence comme `2:255` doit être interprétée dans ce système |
| `plan_implementation_site_coran_warsh_saber_abdelhakam_net9.md` | Architecture légère, sources originales, import traçable, MVP progressif | Ajouter une étape préalable de qualification des données ; ne pas traiter une liste de formats théoriquement disponibles comme un jeu de données déjà acquis et validé |
| `plan_antigravity_editeur_audio_ayah_wajh_net9.md` | Une ayah peut avoir plusieurs segments, répétitions, portions et awjuh ; les silences ne sont pas la vérité métier | Faire respecter les transitions par le serveur et conserver tous les segments lors de la publication |
| `plan_antigravity_mushaf_warsh_images.md` | Pages fidèles, texte séparé pour la recherche, régions multiples, validation graphique | Rechercher et importer des coordonnées existantes avant de dessiner manuellement ; identifier précisément l'édition et versionner les fichiers |

La démarche documentaire n'est donc pas entièrement mauvaise : plusieurs règles importantes sont déjà écrites. L'implémentation contourne certaines de ces règles. L'annotation manuelle doit rester utile pour corriger des exceptions et sélectionner des portions audio, sans devenir la méthode de fabrication de tout le mushaf.

## Anomalies observées dans le code

1. **Mauvaise correspondance page/fichier.** `MushafImageService.ImportPageAsync` utilise `pageNumber + 10` et `https://archive.org/download/20210620_2021/page/n{leafNum}.jpg`. Le fichier local 002 confirme le décalage. Il faut un manifeste explicite des pages, pas ajuster aveuglément la constante.
2. **Provenance insuffisante.** L'édition créée annonce le Complexe du Roi Fahd, mais le téléchargement réel utilise Archive.org. Éditeur original et distributeur peuvent différer ; l'édition, la source précise et leur correspondance ne sont pas documentées.
3. **Régions prédéfinies introduites par un test.** `tests/Quran.IntegrationTests/MushafImportIntegrationTests.cs` se connecte à `quran_db`, importe des pages et insère des rectangles fixes pour les premières ayat. Il ne vérifie pas leur correspondance visuelle. L'exécution passée de ce test et le contenu actuel de la base n'ont pas été vérifiés.
4. **Absence de filtre de validation graphique.** `GetPageViewerContextAsync` et `GetAyahRegionsAsync` retournent les régions sans filtre de statut. Le plan images exige pourtant de ne publier que les régions validées. Il faut distinguer consultation publique et édition administrative.
5. **Pagination déduite arbitrairement.** Le service suppose les ayat 1 à 5 sur la page 2. Le jeu Warsh communautaire inspecté contient les références 2:1 à 2:4 sur cette page. Un remplacement direct des coordonnées par numéro d'ayah serait donc incorrect.
6. **Référentiel textuel à requalifier.** `warsh_baqarah_1_20.json` isole `الم` comme ayah 1 et `surahs.json` donne 286 ayat à Al-Baqara. Le fichier Fatiha comporte notamment `مَٰلِكِ`. Ces données doivent être comparées à une source Warsh identifiée, caractère par caractère et limite par limite. Le nom `TextWarsh` ne prouve pas leur provenance. Ne pas corriger le texte à la main à partir de cet audit.
7. **Édition absente de l'identité du corpus.** `Ayah` stocke une pagination unique ; l'import met à jour par sourate/numéro, sans version de corpus. Modifier le texte en place peut invalider les mots et associations audio existants. Les fichiers images utilisent aussi un répertoire commun à toutes les éditions.
8. **Dimensions et cache.** L'import impose 750 × 1072, sans mesurer le fichier. Le serveur marque les images `immutable` pendant un an, alors que leurs chemins ne contiennent aucune version. Une correction peut rester invisible dans le navigateur.
9. **Publication audio non conforme au modèle prévu.** `RenderSegmentAsync` passe à Rendering sans exiger Approved. `PublishSegmentAsync` peut déclencher ce rendu ; il publie vers un nom ne distinguant pas les différents segments d'une même ayah/wajh, avec écrasement. La récitation canonique est remplacée, alors que le modèle autorise plusieurs portions et répétitions.
10. **Administration à terminer avant mise en ligne.** Les routes admin examinées ne portent pas de protection d'authentification ; `Program.cs` ne configure pas de mécanisme d'authentification. Le nom `/api/admin` ne constitue pas un contrôle d'accès.

## Ressources communautaires vérifiées

| Source | Contenu constaté | Usage proposé |
|---|---|---|
| [quran-ws/quran-svg](https://github.com/quran-ws/quran-svg) | Pages SVG et couche de polygones ; dossier `mushafs/warsh/kfqc` avec SVG et JSON | Premier candidat pour un pilote visuel : graphisme et coordonnées livrés ensemble |
| [QuranHub/quran-pages-images](https://github.com/QuranHub/quran-pages-images) | Images Hafs/Warsh ; `ayat/warsh/data/data.csv` et `quran.db` ; emplacements d'ayat annoncés | Alternative bitmap proche de l'architecture actuelle ; vérifier précisément le couple image/données choisi |
| [adelpro/open-mushaf-native](https://github.com/adelpro/open-mushaf-native) | Application avec ressources `mushaf-elmadina-warsh-azrak` | Référence d'interaction et d'organisation des ressources ; pas besoin de porter toute l'application React Native |
| [quranpedia/qiraat-ayah-map](https://github.com/quranpedia/qiraat-ayah-map) | Correspondances entre systèmes de décompte | Aide à établir une migration explicite ; ne pas présumer une relation un-à-un |
| [Ressources du Complexe](https://qurancomplex.gov.sa/techquran/dev/) | Source officielle visée par les spécifications | Obtenir et archiver le paquet exact et son identification ; l'accès Web a échoué pendant cet audit |

Le dépôt SVG distingue les conditions des illustrations et celles des métadonnées dans `NOTICE.md`. QuranHub annonce GPL-3.0 ; Open Mushaf Native annonce MIT. Conserver les notices du paquet réellement retenu et distinguer code, données et illustrations. Ces constats décrivent les dépôts, sans constituer une validation juridique des droits des tiers.

Je n'ai pas établi quelle est la **dernière édition officielle imprimée** de Warsh. Une mise à jour GitHub n'est pas une nouvelle édition du mushaf. Le choix doit porter sur une édition identifiée et adaptée au projet, puis sur la qualité de sa numérisation.

## Échantillon local reproductible

Dans [research/warsh-svg-sample](research/warsh-svg-sample/manifest.json), trois pages SVG et leurs JSON sont téléchargés à la révision fixe `79a5d21711fc54a98a4f9158eb6865c3be263884`. Le manifeste conserve les URL, la date réelle de récupération et les SHA-256. Les notices originales sont conservées.

Vérification structurelle effectuée :

| Page | Références JSON | viewBox SVG |
|---|---|---|
| 001 | 1:1 à 1:7 | -53.8766 -213.6427 345 550 |
| 002 | 2:1 à 2:4 | -53.8766 -213.6427 345 550 |
| 003 | 2:5 à 2:15 | -6 0 345 550 |

Les origines du viewBox sont différentes : une conversion vers des coordonnées normalisées doit utiliser `(x - minX) / width` et `(y - minY) / height`. Ne pas appliquer ces polygones au scan Archive.org. Un grand rectangle englobant une ayah multilignes pourrait sélectionner des mots voisins ; conserver les polygones ou importer plusieurs rectangles par ligne.

L'échantillon n'a pas encore reçu de validation religieuse ni de contrôle visuel exhaustif des polygones. Il ne contient aucun audio réassocié et n'est pas utilisé par le site en service.

## Plan concret pour terminer le MVP

1. **Figer l'existant.** Initialiser un suivi Git (ce dossier n'est actuellement pas un dépôt), sauvegarder PostgreSQL et les métadonnées audio, inventorier les associations existantes. Sortir les imports modifiant `quran_db` de la suite de tests ordinaire.
2. **Qualifier une édition pilote.** Identifier riwaya, tariq, décompte, éditeur, édition, pagination et révision du paquet. Vérifier les trois pages de l'échantillon, les marqueurs imprimés et les limites de texte. Établir ensuite la comparaison avec le référentiel actuel.
3. **Versionner le référentiel.** Ajouter une identité de corpus et de décompte ; rattacher les ayat à ce corpus. Conserver la pagination dans une relation propre à l'édition. Modéliser la basmala/isti'adha comme introductions audio lorsque nécessaires, sans les utiliser pour décaler un index canonique.
4. **Importer un ensemble cohérent.** Commande explicite et idempotente important manifeste, pages, texte et géométrie dans une nouvelle édition inactive. Vérifier les fichiers, dimensions, références et coordonnées. Aucun téléchargement automatique à la consultation publique ; aucune mutation silencieuse de l'ancien corpus.
5. **Raccorder le lecteur.** Afficher les pages et polygones validés de cette édition. Le clic fournit la référence dans le bon corpus, puis les segments disponibles. Gérer plusieurs régions et les ayat traversant une page. Réserver le dessin manuel aux corrections et portions particulières.
6. **Migrer les associations audio.** Conserver anciens identifiants et timestamps ; produire un rapport de correspondance. Revalider les cas de fusion/scission de versets et les sélections de mots. Publier des fichiers identifiés par segment/version et restituer les segments dans l'ordre.
7. **Faire respecter le workflow.** Exiger l'approbation côté serveur avant rendu, puis rendu réussi avant publication ; invalider l'approbation si texte associé, portion, wajh ou timestamps changent. Protéger les mutations administratives.
8. **Valider puis étendre.** Fatiha complète, début d'Al-Baqara incluant une ayah multilignes et un changement de page, puis Al-Baqara complète avant 114 sourates. La qualification des awjuh de l'enregistrement reste une tâche d'écoute ; les coordonnées communautaires ne la remplacent pas.

Critères du pilote : page affichée conforme au numéro imprimé, chaque clic conforme au marqueur et au texte de l'édition, aucun brouillon visible publiquement, audio correspondant au texte et au wajh sélectionnés, répétitions conservées, changement d'édition sans déplacement des anciens repères.

## Vérification technique réalisée

SDK présent : 9.0.312. Les tests unitaires sélectionnés ont compilé et réussi : **22 réussites, 0 échec**.

Commande :

```powershell
dotnet test tests/Quran.UnitTests/Quran.UnitTests.csproj --no-restore --filter 'FullyQualifiedName!~AudioIngestionIntegrationTests&FullyQualifiedName!~AudioProcessorTests&FullyQualifiedName!~ArchiveOrgInventoryServiceTests' --verbosity minimal
```

Les tests avec téléchargement, traitement de fichiers audio ou imports dans PostgreSQL n'ont pas été lancés. Ces 22 tests ne valident ni le contenu religieux, ni les images, ni le fonctionnement complet du site. Le présent travail ajoute seulement cet audit et l'échantillon de recherche ; la migration et les corrections applicatives restent à réaliser.
