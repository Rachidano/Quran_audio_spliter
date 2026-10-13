# Contre-audit V3 après corrections Antigravity

Date : 13 octobre 2026. Revue des corrections F01–F08, de J011/J012 et des changements d’interface/export.

## Conclusion

**Amélioration substantielle et plusieurs corrections confirmées ; clôture totale encore prématurée.** Les contrôles des services ont progressé, mais certains parcours du wizard contournent leur intention. Cinq défauts résiduels ont été reproduits, avec des témoins positifs pour distinguer une vraie validation d’un refus systématique.

Aucun code applicatif ni aucune donnée utilisateur n’a été modifié pendant ce contre-audit. Les espaces de reproduction sont isolés. L’ancien rapport reste l’historique du premier état ; ce document précise ce qui est désormais corrigé ou encore ouvert.

## Vérifications et preuves

- Suite actuelle : **139 tests réussis, 0 échec, 0 déclaré ignoré**. [TRX de cette exécution](codex-reaudit/reaudit.trx).
- Anciennes reproductions relancées : [résultats](codex-reaudit/old-scenarios/results.json). Démarrage vierge réussi ; anciens refus métier désormais observés.
- Nouvelles reproductions de parcours : [programme](codex-reaudit/harness/Program.cs), [projet](codex-reaudit/harness/Audit.csproj), [résultats](codex-reaudit/reproduction/results.json).
- Exécution depuis la racine : `dotnet run --project desktop/doc/validation/v3/codex-reaudit/harness/Audit.csproj -- desktop/doc/validation/v3/codex-reaudit/reproduction`.
- Les cas wizard utilisent les fixtures de tests existantes et la sélection de récitateur déjà chargée. Les contrôles physiques y sont désactivés pour isoler les règles de persistance ; le témoin métier valide retourne bien `true`. Le cas collection utilise un audio WAV décodable et des PNG, et active les contrôles physiques.
- Setup actuel : **242 070 803 octets**, SHA-256 **F5ABD2B9502DA681F597DF1E2B0FB1CCEA2B9EFA7EF946C4361EE56F2C050DF9**. Conforme à J012, mais le tableau de reprise mentionnait encore l’empreinte antérieure de J011.

## Corrections confirmées

| Sujet du premier audit | Résultat actuel |
|---|---|
| F01 : création de base vierge puis catalogue | Fonctionne dans la reproduction ; les tables complètes sont initialisées |
| F02 : audio/image non approuvés | Refusés, y compris dans l’appel au rendu |
| F03 : récitateur incompatible, gap non confirmé, révision audio différente, prises multiples sans choix, dépassement de 0,5 s, fichiers indécodables | Refusés par les services dans les anciennes reproductions |
| F04 : lot avec un projet incomplet | Confirmé avec un premier projet réellement valide : 0 MP4 produit, lot bloqué avant rendu |
| F05 : rechargement d’une prise sauvegardée existante | Code ajouté pour restaurer son identifiant ; reste incorrect si elle manque et en cas de modifications, voir R01/R02 |
| F06 : collision entre deux projets distincts | Identifiant du projet ajouté au nom de fichier ; l’ancienne collision directe entre projets est supprimée |
| F07 : erreur lors de la création de sauvegarde | Le code propage maintenant une exception bloquante ; le cas d’une sauvegarde préexistante invalide reste ouvert |
| F08 : annulation du wizard et renommage | Jeton/commande d’annulation et `finally` ajoutés ; actualisation du chemin du projet ajoutée au renommage |

Attention à l’interprétation de l’ancien programme de reproduction : certains de ses instantanés audio incomplets sont désormais refusés pour une différence de bornes. Le simple total « 11 scénarios passés » n’est pas une certification. Le nouveau test de collection corrige cette ambiguïté : premier projet validé avec fichiers réels, second incomplet, aucun rendu lancé.

## Erreur d’export avec jaquette MP3

La correction est présente : exclusion de la vidéo lors de l’extraction audio (`-vn`) et sélection explicite des pistes au muxage et dans le parcours discontinu. Le nouveau test `GenerateMultiAyahVideo_AudioWithCoverArt_SucceedsWithoutVideoTagError` fait partie des **139 tests exécutés avec succès** et couvre la génération à partir d’un MP3 doté d’une jaquette.

Ce défaut est donc corrigé sur le scénario testé. Cela ne dispense pas d’une écoute du résultat sur les enregistrements réels de l’utilisateur.

## Défauts résiduels reproduits

### R01 — Majeur : une prise sauvegardée manquante est remplacée sans choix

Dans `StudioViewModel.Wizard.OpenSavedProjectAsync`, le rafraîchissement sélectionne d’abord une prise par défaut. Si le `PassageId` sauvegardé n’existe plus, le code laisse ce choix en place (`if (matchingTake != null)` sans traitement contraire).

**Preuve :** création sauvegardée avec la prise 1 ; suppression de cette prise et ajout d’une prise complète 3 ; réouverture puis enregistrement : la prise 3 est retenue et le projet est valide sans choix utilisateur.

**Correction attendue :** conserver l’état « prise sauvegardée introuvable », laisser la sélection vide et bloquer jusqu’au choix explicite d’un remplacement. Ne pas confondre ce cas avec une création nouvelle.

### R02 — Majeur : réouvrir/enregistrer efface le besoin de revoir une annotation modifiée

Le service détecte correctement une révision audio différente de l’instantané. Cependant, le wizard charge la prise courante puis `SaveProjectAsync` remplace l’instantané par ses nouvelles valeurs. `ExportProjectAsync` appelle précisément cette sauvegarde avant le contrôle final.

**Preuve :** modification de révision et de début de la première prise de 0 à 0,1 s. Validation directe : refus. Réouverture et sauvegarde sans confirmation de revue : validation acceptée.

**Correction attendue :** conserver les instantanés lors de la réouverture, signaler la modification et imposer une confirmation explicite avant leur actualisation. Une sauvegarde de présentation ne doit pas valoir validation d’un changement audio. Invalider également les confirmations de jonction affectées.

### R03 — Majeur : le wizard transforme un choix automatique en choix prétendument explicite

Le service refuse plusieurs prises complètes sans `PassageId` choisi. Mais `RefreshWizardSequenceAsync` sélectionne automatiquement la prise marquée pour export ou la première disponible, puis `SaveProjectAsync` enregistre son identifiant comme si l’utilisateur l’avait choisie.

**Preuve :** deux prises complètes, aucune sélection manuelle dans le scénario ; le rafraîchissement sélectionne la prise 3 et la sauvegarde donne un projet valide.

**Correction attendue :** si plusieurs prises sont proposées pour une nouvelle sélection, demander un choix explicite. Préserver une sélection précédemment confirmée et toujours valable sans la redemander inutilement.

### R04 — Majeur : les modifications des images ne déclenchent pas de nouvelle revue

Les identifiants et révisions d’images sont désormais sauvegardés, mais `ValidateProjectAsync` ne les compare pas à l’image courante. Les comparaisons ajoutées portent sur l’audio.

**Preuve :** augmentation de `ImageRecipe.Revision` après la sauvegarde, statut courant toujours approuvé : le projet reste valide malgré son instantané périmé.

**Correction attendue :** comparer recette, révision et identité du média à l’instantané, puis demander une revue du projet si l’image a changé. La vérification de lisibilité ne prouve pas l’identité du contenu. Même vigilance pour un audio remplacé au même chemin : son empreinte n’est pas contrôlée par le validateur actuel.

### R05 — Majeur : une sauvegarde préexistante corrompue autorise la migration

`CreatePreMigrationBackup` considère un `.pre_v3.bak` existant et non vide comme suffisant. Il ne contrôle pas s’il s’agit d’une base SQLite lisible ni si elle représente les données à migrer.

**Preuve :** fichier contenant `not a SQLite backup` ; la migration réussit et le fichier invalide est conservé tel quel.

**Correction attendue :** produire un snapshot cohérent de l’état à migrer, le vérifier et conserver une version distincte si nécessaire. Un fichier non vide n’est pas une preuve de sauvegarde restaurable.

## Interface et finitions : constat par inspection

- Bibliothèque au démarrage et mode de réglage individuel actif par défaut : changements présents.
- Boutons des deux modes, contraste de « التالي », états actifs des positions et libellé « أسود » : modifications présentes dans le XAML.
- Plein écran désormais raccordé à un événement : il maximise la fenêtre principale sans bordure. Cela ne transforme pas spécifiquement le panneau vidéo en lecteur plein écran. L’état de fenêtre n’est pas restauré dans `OnUnloaded` : à vérifier lors du changement d’onglet en plein écran.
- `JunctionReviewed` est stocké et contrôlé, mais aucune commande ou liaison XAML permettant à l’utilisateur de confirmer une jonction n’a été trouvée. Le refus est désormais effectif, sans parcours complet pour le résoudre.
- Le choix d’une autre prise actualise le badge de la ligne mais pas le booléen global `IsWizardSequenceValid`. Les boutons suivant/export liés à ce booléen peuvent conserver un état périmé. Tester le passage d’une prise partielle/non approuvée à une prise complète approuvée.
- Les préférences globales de signature et la sélection d’un sous-ensemble d’une collection ne sont toujours pas démontrées dans le code examiné.
- La régénération du même projet utilise toujours le même numéro de révision et peut supprimer la sortie existante avant le déplacement final. L’identifiant du projet règle les collisions entre projets, pas toute la gestion des versions et de la publication.

Ces constats ne sont pas présentés comme une recette visuelle humaine. Aucun lancement de l’installateur ni parcours interactif complet n’a été réalisé dans ce contre-audit.

## Priorités de reprise

1. Corriger R01–R03 ensemble : choix et instantanés conservés, modifications visibles, confirmation explicite avant export.
2. Corriger R04 et R05 : identité/révisions des ressources et sauvegarde réellement restaurable.
3. Terminer les interactions de jonction et la mise à jour de l’état global du wizard.
4. Vérifier les autres finitions prévues, puis refaire une recette de la version installée et produire une nouvelle empreinte du Setup.

Ne pas annuler les corrections déjà confirmées : la reprise doit être ciblée sur ces écarts, et non repartir de zéro.
