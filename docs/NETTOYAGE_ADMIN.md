# Administration simplifiée — 25 septembre 2026

## Parcours courant

Un même composant `_AdminNavigation.cshtml` définit les menus du site public (utilisateur autorisé) et de l’administration : tableau de suivi, studio audio, revue/publication, référentiel des أوجه.

Les sources audio sont accessibles depuis le studio et le tableau de bord. Le rapport qualité détaillé et les sources audio se trouvent sous « أدوات متقدمة ». Les écrans de correspondance historique et de régions anciennes ont été supprimés à la demande de l’utilisateur.

Les pages `/Admin/Validation`, `/Admin/CorpusMapping` et `/Admin/MushafRegionEditor` sont supprimées (404). Les mutations des anciennes régions graphiques sont retirées du contrôleur. L’ancienne API `/api/admin/Recitations` retourne 410 aux utilisateurs autorisés. Aucun enregistrement ou média n’est supprimé.

## Indicateurs

Le tableau de bord lit `CorpusAyahs` pour `marw-newMushaf` et `AudioSegments` pour l’édition MARW. La couverture publiée compte les références distinctes existantes, ayant un segment FullAyah publié, un asset et un وجه approuvé lorsque renseigné. Portions, continuations isolées, répétitions et doublons ne constituent pas une couverture complète. La présence réelle des fichiers se contrôle séparément dans le rapport qualité.

Le rapport qualité utilise le même décompte canonique ; il ne déduit plus la géométrie MARW des anciens `AyahImageRegions`. Les intervalles audio ne sont comparés qu’au sein d’une même source. Le tableau de bord ne lance plus à chaque visite le contrôle historique et le recalcul des checksums de tous les médias.

Le service historique `CorpusCheckerService` subsiste pour ses usages techniques anciens, mais n’alimente plus l’accueil admin et ne doit pas être interprété comme certification du corpus MARW.

## Publication

Suppression des commandes « publier directement » et « publier tous les brouillons ». L’approbation exige le statut Reviewed. La publication exige Rendered et un asset ; elle ne déclenche plus implicitement FFmpeg. Les commandes visibles suivent : soumettre, écouter/vérifier, approuver, préparer le fichier, publier. Le système ne peut pas certifier qu’une écoute humaine a réellement été effectuée.

## Limites conservées explicitement

- Qualification complète du texte candidat toujours en attente.
- CRUD des sources, voies et contraintes des أوجه à compléter.
- Éditeur des régions anciennes retiré ; une éventuelle édition de géométrie MARW doit utiliser les références actuelles.
- Gestion des utilisateurs et jobs persistants de rendu à construire.
- Cette intervention simplifie et corrige l’administration ; elle ne constitue pas une certification de production.

## Vérification

- 14 tests unitaires ciblés réussis (services admin, qualité et éditeur), dont exclusion des comptes historiques, des doublons, des portions et des publications sans asset/وجه approuvé.
- 18 tests HTTP réussis (accès, revue et qualité) sur la base de test ; approbation directe d’un brouillon et publication avant rendu refusées.
- Correction supplémentaire révélée par les tests : filtre de sourate de la file de revue fondé sur `SourceAudio.Surah`, pas sur une ancienne FK Ayah facultative.
- Vérification Edge : mêmes entrées de menu sur le site et l’admin, total 6 214, accès aux écrans, disparition des raccourcis de publication et redirection de l’ancienne validation.
- Captures locales : `artifacts/admin-cleanup-desktop.png` et `artifacts/admin-cleanup-mobile.png`.
- Site relancé sur http://127.0.0.1:5098. Les avertissements de nullabilité existants subsistent.

## Suite : retrait effectif et corrections du studio

- Les anomalies de référence sont calculées sur les segments actuels et les références MARW, sans liens vers les pages retirées. Les anciennes correspondances restent en base pour traçabilité.
- Un segment rendu/publié sans asset associé est signalé, tout comme un fichier absent sur disque.
- Le calcul des intervalles conserve la fin la plus éloignée : une portion imbriquée ne produit plus un faux intervalle vide.
- Les liens `segmentId` et `timeMs` du rapport positionnent le curseur du studio sans changer les bornes ni enregistrer de modification.
- Affichage corrigé des sept états (Reviewed n’est pas Published), références MARW affichées, notes et noms échappés dans le tableau.
- Raccourcis ignorés pendant la saisie, sur une commande focalisée et lors d’une répétition clavier ; début limité à la durée du fichier ; durée fournie avec les peaks.
- Bouton d’import JSON fictif supprimé : son code annonçait un succès sans persistance. L’export reste disponible. Un import canonique transactionnel reste à intégrer au studio.

Dernière vérification : 15 tests unitaires ciblés et 19 tests HTTP réussis. Edge confirme les anciennes routes en 404, leur absence du menu, la navigation temporelle, les libellés d’état et l’échappement des notes. Les captures sont `artifacts/admin-retired-menu.png` et `artifacts/studio-retirement-verified.png`. Aucune annotation créée par ce contrôle navigateur.
