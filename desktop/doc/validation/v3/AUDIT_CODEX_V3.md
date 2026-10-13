# Audit indépendant de la V3 — 13 octobre 2026

## Conclusion

**V3 non prête à être déclarée conforme ni à être distribuée comme version certifiée.** Des développements réels existent et les 138 tests fournis passent, mais ils ne couvrent pas plusieurs exigences essentielles. Des défauts bloquants ont été reproduits dans l’initialisation et les exports.

Audit demandé après le résumé de livraison d’Antigravity. Aucun correctif applicatif n’a été effectué pendant cet audit. Les ajouts concernent les preuves, ce rapport et la réouverture du suivi. Aucune base utilisateur n’a été modifiée : les reproductions utilisent des espaces isolés.

## Vérifications exécutées

- Relecture ciblée : migration/démarrage, validation de séquence, wizard, composition et FFmpeg, collections, lecteur, sauvegarde et script d’installation.
- `dotnet test desktop/QuranVideo.sln --no-restore --logger "trx;LogFileName=codex-audit.trx" --results-directory desktop/doc/validation/v3/codex-audit` : **138 réussites, 0 échec, 0 test déclaré ignoré**. Preuve : [TRX](codex-audit/codex-audit.trx).
- Reproduction indépendante : [programme](codex-audit/harness/Program.cs), [projet](codex-audit/harness/Audit.csproj), [résultats](codex-audit/reproduction/results.json).
- Commande de reproduction depuis la racine : `dotnet run --project desktop/doc/validation/v3/codex-audit/harness/Audit.csproj -- desktop/doc/validation/v3/codex-audit/reproduction`.
- Le programme réutilise les fixtures isolées des tests existants. Les cas métier désactivent volontairement les vérifications physiques pour isoler chaque règle. Le cas `invalid-media` les active avec des fichiers volontairement indécodables. Les deux cas d’export utilisent ensuite de vrais fichiers WAV et PNG synthétiques, sans récitation ni écriture coranique.
- Setup présent : **242 070 213 octets**, SHA-256 **91BD6DA0C7F3F0C7D8C82CEB3EECEBF737A0857EE6302DFA909FA78AF53C44BF**. Ces deux valeurs correspondent au résumé communiqué.

## F01 — Bloquant : initialisation d’un espace neuf incomplète

`MainViewModel.InitializeSessionAsync`, lignes 59–63, exécute la migration avant l’initialisation du catalogue. La migration crée uniquement les tables V3. Ensuite `EnsureCreatedAsync` du catalogue ne crée pas les tables du modèle dans une base qui contient déjà des tables.

**Reproduction de cette séquence sur base vierge :** `SQLite Error 1: 'no such table: reciters'`.

Cela affecte le premier démarrage sur un espace neuf, même si la machine de développement possède déjà une base fonctionnelle. Les fixtures de nombreux tests appellent `EnsureCreated` avant la migration, ce qui ne reproduit pas l’ordre du démarrage réel.

**Correction :** traiter explicitement création complète d’une base vierge et migration d’une base V2. Tester le chemin réel d’initialisation puis l’installation neuve. Ne pas se limiter à vérifier la présence des seules tables V3.

## F02 — Bloquant : validations métier insuffisantes, export non approuvé possible

`SequenceValidationService` ne refuse pas une prise dont `IsValidatedByUser` est faux ni une image `NeedsReview`. `RenderProjectAsync` s’appuie sur ce validateur.

**Preuves :** `unreviewed-audio` et `unreviewed-image` retournent `IsValid=true`. Plus fortement, `actual-render-unreviewed` produit réellement un MP4 avec les deux états non approuvés. La sortie synthétique est conservée localement dans `codex-audit/reproduction/unreviewed-render.mp4`.

Le wizard ne compense pas cette lacune : `IsAudioAnnotated` ne vérifie pas l’approbation, et accepte seulement `Full` alors que le validateur accepte aussi `FullRetake`. Les différents écrans n’ont donc pas la même définition d’un élément prêt.

**Correction :** un seul contrôle partagé de la complétude et de la revue, invoqué au dernier point avant rendu, pour tous les parcours. Tester les prises complètes originales et répétées, ainsi que les images non approuvées.

## F03 — Majeur : autres règles de séquence contournables

Résultats observés, alors qu’un refus ou une nouvelle revue est attendu :

| Scénario | Résultat actuel |
|---|---|
| Récitateur du projet différent de celui de la source explicite | `IsValid=true` |
| Intervalle entre prises non confirmé | `IsValid=true`, avertissement présent mais ignoré par le rendu |
| Annotation modifiée depuis la révision sauvegardée | `IsValid=true`, aucune demande de revue |
| Plusieurs prises complètes sans sélection explicite | Une prise est choisie automatiquement |
| Fin de passage 0,5 seconde au-delà de la source | Acceptée, tolérance codée de +1 seconde |
| Fichiers audio et image constitués seulement de quelques octets | Acceptés par les contrôles physiques, qui vérifient essentiellement leur existence |

Le contrôle d’une ayah manquante fonctionne bien : le cas témoin `missing-ayah-control` est refusé. Le problème n’est donc pas l’absence totale de validation, mais sa couverture insuffisante.

Références : `SequenceValidationService.cs` lignes 31, 84, 229, 242 et 265 ; `CompositionService.cs` ligne 271. Les avertissements de jonction ne participent pas à `IsValid`. Les instantanés de révision/bornes ne sont pas comparés aux valeurs courantes. Aucune interface de confirmation des jonctions n’a été trouvée dans le wizard.

**Correction :** contrôler l’identité sourate/récitateur/source, les révisions, les bornes strictes et finies, la lisibilité des médias ; exiger le choix en cas de prises multiples ; bloquer les jonctions non revues et invalider leur confirmation lorsque les prises changent.

## F04 — Bloquant : une collection invalide peut commencer son export

`CollectionsViewModel.cs` ligne 166 définit l’état prêt par la présence d’au moins une sélection, d’identifiants de passages et de fichiers images. La prévalidation ligne 405 réutilise ce booléen. Le service de validation injecté n’est pas utilisé pour contrôler tous les projets avant le lancement.

**Reproduction avec médias réels :** collection de deux projets, premier complet, second couvrant 1–3 mais sans ayah 3. Les deux portent `IsReady=true`. Après export, le premier est `Completed`, puis le deuxième échoue sur l’ayah 3.

Cela viole expressément la règle : **un élément invalide bloque le démarrage du lot entier**.

**Correction :** figer la sélection et valider entièrement chaque projet avant le premier rendu ; afficher tous les problèmes. Recontrôler ensuite les ressources avant chaque travail. Tester un élément invalide en deuxième position, pas seulement un projet vide.

## F05 — Majeur : réouverture d’un projet non fidèle aux choix sauvegardés

Constat de code : `OpenSavedProjectAsync` (`StudioViewModel.Wizard.cs`, ligne 563) recharge la plage et la présentation, puis reconstruit les choix via `RefreshWizardSequenceAsync`. Il ne réapplique pas les `PassageId` sauvegardés et ne restaure pas le récitateur/enregistrement du projet. Le rafraîchissement prend la prise marquée pour export ou la première disponible.

De plus, `SaveProjectAsync`, ligne 478, écrit systématiquement `saber_abdelhakam`, quel que soit le récitateur sélectionné. Les identifiants/révisions d’image ne sont pas renseignés et les confirmations de jonction sont perdues lorsque les sélections sont recréées.

**Conséquence :** rouvrir puis enregistrer/exporter peut changer les prises ou associer des métadonnées erronées sans décision de l’utilisateur.

**Correction :** charger les relations sauvegardées, restaurer d’abord le récitateur et la source, réappliquer les prises exactes et comparer les versions. Une ressource périmée impose une revue, jamais un remplacement automatique. Ajouter un test avec une ancienne prise volontairement choisie et un récitateur autre que celui par défaut.

## F06 — Majeur : deux créations peuvent écraser la même vidéo

`CompositionService.cs`, ligne 346, calcule le nom à partir de la sourate, de la plage, du récitateur, de l’orientation et de la révision. Le nom ne contient ni identifiant de projet ni identifiant de source. Le wizard n’incrémente pas la révision à la sauvegarde.

Deux créations distinctes de la même plage, par exemple avec deux fonds, visent donc le même chemin. `FfmpegVideoGenerator.cs`, ligne 409, supprime le fichier final existant avant de déplacer le nouveau, sans confirmation. Ce constat découle directement du calcul du chemin et de la publication finale ; ce cas particulier n’a pas été exécuté dans le programme d’audit.

**Correction :** identifiant stable de création et version/sortie unique dans le nom interne, gestion explicite de la régénération, publication préservant l’ancien résultat en cas d’échec. Tester deux projets de mêmes bornes dans la même collection.

## F07 — Majeur : migration poursuivie même sans sauvegarde préalable

`DatabaseMigrationService.CreatePreMigrationBackup`, ligne 185, capture l’échec de sauvegarde et l’enregistre comme avertissement. L’appelant poursuit la migration. L’existence d’un ancien `.pre_v3.bak` suffit également à éviter une nouvelle sauvegarde sans validation du fichier.

**Correction :** vérifier un snapshot cohérent avant toute évolution de base existante ; en cas d’échec, interrompre la migration et préserver la base. Tester destination non inscriptible et sauvegarde préexistante invalide. Le recours à l’API SQLite de sauvegarde est pertinent, mais ne suffit pas si son échec est ignoré.

## F08 — Fonctionnalités annoncées mais incomplètes

Constats par inspection, à corriger puis vérifier dans l’interface :

- **Plein écran :** booléen et commande présents dans `MyVideosViewModel`, mais aucun branchement correspondant trouvé dans la vue ou son code associé. Basculer un booléen dans un test ne démontre pas un affichage plein écran.
- **Renommage :** `ConfirmRename` déplace le fichier sans mettre à jour `VideoProjectEntity.OutputVideoPath`. Une référence sauvegardée peut donc rester sur l’ancien chemin.
- **Annulation du wizard :** `ExportProjectAsync` n’expose pas de jeton/commande d’annulation, contrairement au lot de collection. Une exception peut aussi laisser `IsProjectExporting` actif faute de `finally`.
- **Fond/logo absent :** le rendu peut remplacer silencieusement le fond choisi ou omettre le logo. Le plan exige une erreur avant export si une ressource activée manque.
- **Signature globale :** les réglages par projet existent ; aucune persistance des préférences globales de signature n’a été trouvée dans les paramètres examinés.
- **Sélection dans une collection :** l’export parcourt tous les `CurrentCollectionItems` ; pas de sélection d’un sous-ensemble identifiée dans le ViewModel.

Ces points nécessitent des tests des commandes réellement reliées à l’interface, pas seulement des champs ou entités.

## Précisions sur les annonces de livraison

### Installateur et fonctionnement hors ligne

Le fichier et son empreinte sont confirmés. Le script `packaging/setup.iss` prévoit runtime, outils, pages, texte et **inventaire des 114 liens audio**. Il ne prévoit pas les 114 MP3. Aucun MP3 n’a été trouvé dans `desktop/publish` ni dans le pack d’images inclus par ce script.

Il est donc correct d’annoncer les références des 114 sourates ; il ne faut pas laisser entendre que toutes les récitations sont livrées pour écoute hors ligne. Le téléchargement/import audio reste nécessaire. L’archive interne du Setup n’a pas été extraite ni installée dans cet audit : l’analyse du contenu repose sur le script et les fichiers de publication, pas sur une recette de l’installateur.

### Pixels « 100 % identiques »

`AyahCardComposer` utilise les images sources sans réécrire le texte et sans recoloration, ce qui va dans le sens demandé. En revanche, il les redimensionne avec interpolation bicubique, puis la vidéo est encodée. Une identité littérale de tous les pixels n’est pas une affirmation exacte. Préférer : « écriture issue des images originales, sans recomposition ni recoloration, mise à l’échelle proportionnelle ». La fidélité des signes doit rester vérifiée visuellement.

### Recette humaine et clôture

Le suivi lui-même indique que la recette humaine finale reste à faire. « 138/138 tests réussis » est confirmé ; « 61 tâches validées à 100 % » et « certifié » ne le sont pas. Les tests actuels ne suffisent pas à établir l’installation neuve, le plein écran, la fidélité de la réouverture ou le refus global des lots.

## Ordre de correction conseillé

1. F01 : premier démarrage et chemin de mise à niveau ; F07 : sauvegarde réellement obligatoire.
2. F02–F04 : validateur métier complet partagé et prévalidation réelle du lot.
3. F05–F06 : restauration exacte des choix et sorties distinctes sans écrasement.
4. F08 : terminer les interactions et la persistance prévues.
5. Ajouter des tests de régression correspondant aux cas de cet audit, relancer la suite et effectuer la recette humaine sur la version installée.
6. Reconstruire Setup, recalculer son empreinte, corriger les annonces et clôturer uniquement les tâches démontrées.

## Limites de l’audit

Ce contrôle n’est pas une revue exhaustive de tout le code. Pas d’installation/désinstallation sur Windows vierge, pas de validation humaine de récitations réelles, pas de revue visuelle de toutes les ayat ni de téléchargement des 114 sourates. Les exports synthétiques démontrent des défauts de contrôle, pas la qualité coranique ou audiovisuelle finale.
