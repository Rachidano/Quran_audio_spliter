# Lecteur Warsh : corpus MARW complet

## Circuit fonctionnel en base

État actuel : `artifacts/marw-system-final/Quran.Web.dll`, port 5098. Référentiel CRUD partagé, annotations persistées, prévisualisation MARW et choix des passages publiés par وجه intégrés. Voir [le fonctionnement et les limites](SYSTEME_ANNOTATIONS_AWJUH.md). Les descriptions de catalogues statiques et de binaires ci-dessous sont historiques.

## Catalogue v3 — 14 propositions et 4 contraintes

Binaire actif : `artifacts/marw-awjuh-v3/Quran.Web.dll` sur 5098. Dix branches contextuelles de Yā-Sīn/Asbahani ont été ajoutées aux quatre propositions Azraq, ainsi que quatre contraintes dans une collection séparée. Toutes sont PendingReview ; aucun contrôle automatique d’applicabilité n’est activé. Voir [le dépouillement](research/awjuh/EXTRACTION.md). Les branches Yā-Sīn sont exclues du sélecteur de l’atelier Al-Baqara.

## Référentiel des awjuh — première version

Binaire actif sur 5098 : `artifacts/marw-awjuh/Quran.Web.dll`. Catalogue arabe `/mushaf/awjuh`, API `/api/awjuh/catalog`, source unique embarquée `src/Quran.Infrastructure/Data/awjuh-catalog.json`. Quatre propositions documentées dans le seul groupe البدل/اللين de l’Azraq/Shatibiyya ; couverture partielle explicite, autres voies non documentées. Aucune entrée approuvée. L’atelier conserve le code et la révision du catalogue dans les annotations ; le choix ne valide ni le wajh ni son application à l’ayah.

La commande `--replace-legacy-awjuh` a supprimé les sept anciens codes de PostgreSQL. Les rattachements concernés ont été détachés et leurs publications/approbations retirées, sans supprimer les assets audio ni modifier les temps. Le seed ne recrée plus ces codes. Les propositions du catalogue ne sont pas injectées dans la table des awjuh validés. Cette table reste utilisée par les anciens écrans ; leur unification avec le futur circuit de validation reste à réaliser. Le référentiel n’est pas exhaustif ni certifié, et le rattachement automatique aux mots/ayahs n’est pas implémenté.

Tests : deux tests unitaires du catalogue et du retrait idempotent des anciennes associations ; contrôle navigateur `scripts/check-awjuh.cjs` pour filtrage, choix, édition et export. Pour compléter le corpus : documenter les routes détaillées, les sources primaires, les exceptions et les combinaisons, puis enregistrer la revue spécialisée avant approbation.

## Démonstration des annotations utilisateur

Binaire actif : `artifacts/marw-audio-demo/Quran.Web.dll`, port 5098. `/mushaf/audiodemo` utilise une copie intacte du dernier export utilisateur `baqarah-audio-passages-draft(1).json`, servie par `wwwroot/data/baqarah-demo-draft.json`. Les cinq références sont appliquées telles que saisies, sans validation de leur correspondance au texte MARW. L’intervalle 67,986–84,592 s reste sans surlignage. La mention de وجه reste une observation non approuvée. Aucun mélange avec le premier export ni modification des images. `scripts/check-audio-demo.cjs` vérifie les cinq références, la visibilité des pages, le trou, le libellé de wajh, la lecture et le mobile. Compilation réussie.

## Atelier arabe et observations des أوجه

Le binaire actif sur 5098 est `artifacts/marw-audio-ar/Quran.Web.dll`. L’atelier `/mushaf/audioreview` est en arabe et RTL. Le type de segment est distinct de l’observation de wajh : non déterminé, même وجه apparent, autre وجه apparent. Le طريق et la description sont saisis librement, sans catalogue d’awjuh validés ni attribution automatique. Les champs JSON additifs `wajhObservation`, `wajhTariq`, `wajhDescription` conservent ces propositions ; `wajhId` reste null et `wajhStatus` reste Unqualified. Les anciens brouillons restent lisibles. Compilation et tests navigateur (persistance, édition, export, lecture et mobile) réussis.

## État actif : atelier des reprises audio

Le binaire actuel est `artifacts/marw-audio-review/Quran.Web.dll` sur le port 5098. L’atelier <http://127.0.0.1:5098/mushaf/audioreview> permet de repérer les reprises d’Al-Baqara sans modifier l’original. Voir [le suivi des reprises](AUDIO_REPRISES_BAQARA.md). Les limites de la reprise signalée et son éventuel wajh restent à qualifier. Les indications de binaires ci-dessous décrivent les étapes précédentes.

## État actif : revue des 114 débuts terminée

Le lecteur utilise `artifacts/marw-openings-review/Quran.Web.dll` sur le port 5098. La revue visuelle assistée des 114 premiers versets est consignée dans [le rapport complet](research/marw/opening-review-full.md) : 104 corrections supplémentaires et confirmation des dix précédentes. Les 6 100 autres tracés sont inchangés. La galerie `/mushaf/review` affiche les corrections et propose le rapport JSON ; les statuts personnels restent indépendants. Les mentions de binaires et de revue partielle ci-dessous sont historiques.

La galerie des 114 débuts de sourate est disponible sur <http://127.0.0.1:5098/mushaf/review>. Le binaire actif est maintenant `artifacts/marw-gallery/Quran.Web.dll` (même commande de compilation/lancement ci-dessous avec ce dossier de sortie). La galerie fournit les statuts, notes locales et l'export du rapport ; elle ne signifie pas que les 114 débuts ont été relus.

## Revue du surlignage

Dix débuts de sourate ont reçu une correction locale des rectangles d'en-tête sur les pages 1, 2, 50, 106, 603 et 604. Voir [le rapport de revue](research/marw/region-review.md). Les données originales sont conservées. Le binaire actif sur 5098 est désormais `artifacts/marw-reviewed/Quran.Web.dll` ; utiliser `artifacts/marw-reviewed` à la place de `artifacts/marw-full` dans les commandes ci-dessous pour reproduire cette sortie.

## Généralisation du 13 septembre 2026

Le lecteur sur <http://127.0.0.1:5098/mushaf/page/1> affiche les **604 PNG originaux MARW**, avec les délimitations fournies par la même édition. Le paquet actif est `data/mushaf/editions/marw-newMushaf/20260913-full/` : 1 208 fichiers image/JSON, environ 142 Mo, avec manifeste et SHA-256. Les trois pages du premier essai restent conservées dans leur dossier daté.

La navigation permet de saisir une page de 1 à 604 ou de choisir parmi les 114 sourates. Les pages contenant plusieurs sourates proposent des références distinctes et les enregistrements complets déjà disponibles localement. Le texte de l'API preview et la synchronisation audio par ayah ne sont pas intégrés.

L'audit automatique trouve 6 214 références, une séquence continue et aucune coordonnée hors image. Le rapport est dans `docs/research/marw/full-corpus-audit.json`. Cela vérifie la cohérence technique du corpus ; cela ne constitue pas une relecture religieuse de chaque ayah ni une vérification visuelle exhaustive des délimitations.

Contrôles : compilation réussie, 32 tests unitaires sélectionnés réussis, comparaison des SHA-256 des 604 images servies avec le manifeste, présence des 6 214 références, navigation page/sourate, sélection sur la dernière page, affichage mobile et refus des pages hors limites. Scripts : `scripts/audit-marw.py`, `scripts/check-marw-full.cjs` et `scripts/check-warsh-pilot.cjs`. Le téléchargement reproductible utilise `scripts/download-marw.ps1` avec PowerShell 7.

L'instance active sur 5098 utilise `artifacts/marw-full/Quran.Web.dll`. Pour reconstruire et lancer après arrêt de cette instance :

```powershell
dotnet build src/Quran.Web/Quran.Web.csproj -o artifacts/marw-full
$projectRoot = (Get-Location).Path
dotnet artifacts/marw-full/Quran.Web.dll --contentRoot "$projectRoot/src/Quran.Web" --urls http://127.0.0.1:5098 --environment Development --WarshPilot:ReviewMode true
```

Les sections suivantes conservent l'historique du pilote ; leurs chemins de paquet et de binaire ne désignent plus la version active.

## Mise à jour du 13 septembre : MARW intégré au lecteur .NET

Le lecteur `/mushaf/page/1` (également `/mushaf/warsh/page/1`) affiche désormais les trois PNG originaux MARW `newMushaf`, avec leurs propres rectangles. Les sections historiques ci-dessous décrivent la première livraison Quran.ws, conservée comme référence.

Le paquet actif se trouve dans `data/mushaf/editions/marw-newMushaf/20260913/`. `MarwPilotCorpus` contrôle ses SHA-256, lit les dimensions PNG et transforme les coordonnées imbriquées crop → texte → rectangle vers l'image entière. La source est indiquée dans l'interface. Aucun texte de l'API preview MARW ni nouvel extrait audio n'a été importé.

L'instance sur le port 5098 utilise actuellement `artifacts/marw-pilot/Quran.Web.dll`. Pour reconstruire après arrêt de cette instance : `dotnet build src/Quran.Web/Quran.Web.csproj -o artifacts/marw-pilot`, puis employer cette DLL avec les arguments de lancement ci-dessous. Le build standard du projet intègre aussi le paquet MARW.

Contrôles navigateur réussis après bascule : routes et liens, 22 sélections, clic géométrique sur les trois pages, zoom/mobile/clavier, ETag/404, audio source Range. Le test compare également le SHA-256 du PNG servi avec le fichier MARW original. Le comparatif sur 5099 n'est plus nécessaire pour voir les images MARW dans le lecteur.

## Livraison

Le lecteur `/mushaf/warsh/page/1` utilise maintenant un paquet graphique distinct de l'ancien référentiel PostgreSQL. Les trois pages proviennent de `quran-ws/quran-svg`, révision `79a5d21711fc54a98a4f9158eb6865c3be263884`, dossier `mushafs/warsh/kfqc`.

Les sources importées sont dans `data/mushaf/editions/warsh-kfqc-quranws/<revision>/`. Le manifeste, les notices et les SHA-256 sont conservés. Les fichiers sont copiés dans la sortie .NET et dans la publication ; le serveur ne contacte pas GitHub ou Archive.org pour afficher une page.

Le pilote permet :

- de naviguer entre les pages 1, 2 et 3 ;
- de sélectionner les 22 références par clic sur le graphisme ou bouton accessible au clavier ;
- de partager la sélection via `?Reference=2:3` dans le contexte de cette édition ;
- de zoomer sans désaligner les coordonnées ;
- d'écouter la sourate originale déjà présente dans le stockage local.

Les ayat multilignes conservent leurs sous-tracés distincts. Image et surlignage utilisent le même `viewBox`, y compris ses origines négatives. Aucune conversion approximative en grand rectangle n'est appliquée.

## Lancer depuis la racine du projet

Une ancienne instance peut verrouiller `src/Quran.Web/bin`. La sortie séparée ci-dessous permet de préparer et vérifier le pilote sans l'arrêter.

```powershell
dotnet build src/Quran.Web/Quran.Web.csproj -o artifacts/warsh-pilot
dotnet artifacts/warsh-pilot/Quran.Web.dll --check-warsh-pilot
$projectRoot = (Get-Location).Path
dotnet artifacts/warsh-pilot/Quran.Web.dll --contentRoot "$projectRoot/src/Quran.Web" --urls http://127.0.0.1:5098 --environment Development --WarshPilot:ReviewMode true
```

Ouvrir <http://127.0.0.1:5098/mushaf/warsh/page/1>. Arrêter une précédente session de revue sur 5098 avant de relancer sur le même port. L'ancienne instance éventuellement présente sur 5088 ne charge pas automatiquement les nouveaux binaires.

`ReviewMode` utilise un fournisseur temporaire de protection des données en Development pour les sessions locales isolées qui ne peuvent pas déchiffrer les clés Windows DPAPI de l'utilisateur. Il ne s'applique pas en Production. Les sessions de revue ne sont pas un mode de déploiement public.

Le pilote ne dépend pas de PostgreSQL. Les autres écrans historiques, notamment l'accueil, la recherche et l'administration, continuent d'en dépendre.

## Rejouer l'import

Le paquet est déjà importé. Pour reproduire l'opération :

```powershell
$projectRoot = (Get-Location).Path
dotnet artifacts/warsh-pilot/Quran.Web.dll --import-warsh-pilot "$projectRoot/docs/research/warsh-svg-sample" "$projectRoot/data/mushaf/editions/warsh-kfqc-quranws"
dotnet build src/Quran.Web/Quran.Web.csproj -o artifacts/warsh-pilot
```

La commande valide le paquet avant écriture, copie dans un dossier temporaire puis finalise le dossier de révision. Relancer avec les mêmes fichiers ne modifie pas le paquet existant. Un paquet altéré, une géométrie SVG/JSON différente ou une révision inconnue est refusé. Aucune ligne PostgreSQL ni association audio n'est créée ou modifiée.

## Contrôles réalisés

- Build du projet Web réussi ; avertissements de nullabilité présents dans le service historique.
- 32 tests unitaires sélectionnés réussis, dont 10 nouveaux tests concernant le paquet et la désactivation de l'ancien import.
- Contrôle navigateur Chromium : 22 sélections par boutons, clic géométrique sur chacune des trois pages, lien de sélection, remise à zéro, clavier, viewport mobile de 390 px, zoom à 175 %, absence de débordement horizontal global.
- API : page hors pilote et référence absente refusées ; image SVG versionnée ; ETag et réponse 304 ; mauvaise révision refusée.
- Audio original : réponse HTTP 206 à une requête Range.
- Captures de revue : `artifacts/warsh-pilot-checks/`.

Tests unitaires reproductibles :

```powershell
dotnet test tests/Quran.UnitTests/Quran.UnitTests.csproj --no-restore --filter 'FullyQualifiedName!~AudioIngestionIntegrationTests&FullyQualifiedName!~AudioProcessorTests&FullyQualifiedName!~ArchiveOrgInventoryServiceTests'
```

Contrôle navigateur : avec Playwright et Chromium installés, exécuter `node scripts/check-warsh-pilot.cjs`. `PLAYWRIGHT_MODULE` peut indiquer le module Playwright existant ; `PILOT_URL` permet de changer l'URL de revue. Ce test ne modifie ni la base ni les fichiers audio.

## Ancien système

L'ancien lecteur Razor a été conservé dans `docs/recovery-before-warsh-pilot/` avant son remplacement. Le service historique est également sauvegardé à cet endroit.

`MushafImageService.ImportPageAsync` refuse désormais l'import Archive.org erroné. Lire une page absente ne crée plus d'édition et ne télécharge plus d'image. Le test d'intégration historique qui écrivait des rectangles prédéfinis dans `quran_db` est explicitement désactivé.

Les images et régions historiques restent présentes pour examen. Les anciennes API et l'éditeur graphique administratif n'ont pas été migrés : ils ne doivent pas servir à valider le nouveau paquet. Le lecteur pilote utilise uniquement `/api/mushaf/warsh-pilot/...` et n'utilise aucun `AyahId` historique.

## Limites et prochain lot

Cette livraison constitue un pilote graphique, pas la validation religieuse du corpus complet ni la confirmation de la dernière édition imprimée. La date bibliographique et le système de décompte doivent encore être documentés à partir de l'édition de référence.

Le texte Unicode historique n'est pas réutilisé dans le pilote. Les références de la nouvelle page 2 sont 2:1 à 2:4 ; les anciennes données isolent `الم` comme première ayah. Un raccordement automatique par numéro ferait écouter le mauvais extrait.

Le lecteur indique donc que le raccordement ayah/audio est en cours de revue. L'audio disponible est **la sourate entière, sans synchronisation avec la sélection**. Les anciens segments, timestamps et awjuh sont conservés et n'ont pas été réaffectés.

Prochain lot : obtenir un texte Warsh sourcé compatible avec cette édition ; créer l'identité du corpus et sa table de correspondance ; inventorier puis revalider les associations audio de Fatiha et du début d'Al-Baqara. Les transitions d'approbation/publication et la protection de l'administration restent les lots suivants décrits dans l'audit.
