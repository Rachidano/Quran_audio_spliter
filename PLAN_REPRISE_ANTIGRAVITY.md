# Plan de reprise et d’implémentation — Quran Warsh

**Date : 14 septembre 2026. Destinataire : Antigravity.**  
**Statut : plan à exécuter, pas constat de réalisation.**  
**Dossier : `D:\Projets\ITComp\Quran` — solution `QuranWarsh.sln`.**

## 1. Mandat et résultat attendu

Transformer le projet existant en un site cohérent de consultation et d’écoute du Coran selon رواية ورش عن نافع, avec les enregistrements de صابر عبد الحكم, un référentiel évolutif des أوجه et un véritable circuit éditorial. Réutiliser les composants utiles, réunifier les circuits concurrents et retirer les expérimentations de la navigation normale.

Le résultat doit permettre ce parcours complet :

1. Un administrateur importe et identifie une source audio.
2. Un annotateur écoute, délimite des occurrences, indique les ayat ou portions réellement récitées et, lorsqu’il sait l’identifier, le وجه observé.
3. Ses annotations sont immédiatement enregistrées dans le système et prévisualisables sur les images MARW, sans export/import manuel obligatoire.
4. Un réviseur vérifie les limites audio, le rattachement au texte et à l’image, ainsi que l’attribution éventuelle d’un وجه.
5. Le système produit les fichiers dérivés, puis permet leur publication explicite.
6. Un visiteur choisit une sourate ou une ayah, voit l’image correspondante, choisit un وجه disponible, écoute la bonne occurrence, télécharge le passage et partage un lien stable.
7. Les responsables peuvent enrichir et corriger le référentiel, retrouver les occurrences concernées et mesurer la couverture réelle du corpus.

Il faut livrer un **système complet**, puis poursuivre le **travail humain de qualification du contenu**. Une application opérationnelle sur un corpus pilote ne signifie pas que tous les enregistrements des 114 sourates sont annotés et validés. Les deux avancements doivent être affichés séparément.

### 1.1 Documents faisant référence

| Document | Rôle dans cette reprise |
|---|---|
| [Spécifications initiales](specifications_fonctionnelles_techniques_quran_warsh_net9.md) | Besoins, parcours publics, administration, règles métier, sécurité, exploitation et critères MVP |
| [Plan initial](plan_implementation_site_coran_warsh_saber_abdelhakam_net9.md) | Architecture .NET, ingestion, pilote Fatiha/Baqara et généralisation |
| [Plan de l’éditeur](plan_antigravity_editeur_audio_ayah_wajh_net9.md) | Occurrences multiples, portions, waveform, workflow, audit et rendu |
| [Plan images](plan_antigravity_mushaf_warsh_images.md) | Images comme référence visuelle, régions multiples, portions graphiques et zoom |
| [Audit du 12 septembre](docs/AUDIT_WARSH_ET_PLAN_DE_REPRISE.md) | Historique des défauts de provenance, pagination et numérotation ; certaines anomalies ont ensuite été corrigées |
| [Journal du pilote](docs/PILOTE_WARSH.md) | Historique des essais ; ne pas considérer chaque ancienne instruction comme l’état courant |
| [Circuit intégré du 14 septembre](docs/SYSTEME_ANNOTATIONS_AWJUH.md) | Description de la dernière intégration partielle et de ses limites |
| [Recherche des awjuh](docs/research/awjuh/README.md) et [extraction](docs/research/awjuh/EXTRACTION.md) | Provenance et couverture documentaire partielle, sans certification d’exhaustivité |

Les demandes ultérieures de l’utilisateur précisent les documents initiaux : utiliser les **images MARW effectivement choisies**, proposer un **CRUD fonctionnel** des أوجه, simplifier l’annotation, avancer automatiquement au début et au numéro suivants, conserver les répétitions et exploiter directement les annotations dans le lecteur.

### 1.2 Arbitrages à appliquer

- Conserver la solution .NET 9 / EF Core 9 / Razor Pages / PostgreSQL et les six projets existants. Ne pas convertir le projet en SPA ni le réécrire. La contrainte .NET 9 est explicite dans les documents ; toute évolution de version fera l’objet d’une décision distincte, notamment lors de la préparation de l’hébergement.
- Le mushaf actif est le paquet MARW `newMushaf`, révision locale `20260913-full`. Ne pas remettre les anciens scans ou le pilote SVG en référence active.
- Une référence d’ayah comporte une identité de corpus/décompte. Le seul couple sourate/numéro n’est pas suffisant pour migrer les anciennes données.
- Réutiliser les coordonnées communautaires/importées. Le dessin manuel sert aux corrections et aux portions manquantes, pas à recréer systématiquement toutes les pages.
- Les anciens codes d’awjuh supprimés à la demande de l’utilisateur ne doivent pas être recréés par un seed.
- Un وجه du référentiel, une observation faite en écoutant et une occurrence audio sont trois objets distincts.
- L’image originale et le MP3 original sont immuables. Un export utilisateur doit rester récupérable avec ses identifiants et ses temps d’origine.
- Les exemples anciens de noms de fichiers fondés uniquement sur `surah/ayah/wajh.mp3` sont remplacés par des clés comportant l’identité et la révision de l’occurrence : plusieurs occurrences ne doivent jamais s’écraser.
- L’enregistrement d’une annotation ne vaut ni approbation ni publication. Aucun outil automatique ne certifie une lecture ou un وجه.
- Le back-office est en arabe et en RTL. Les termes métier restent cohérents entre référentiel, éditeur, validation et lecteur.

## 2. État de départ : ce qui existe et ce qui manque

Cet inventaire repose sur les documents locaux et le code examiné. Les comptes de base et les résultats de tests historiques doivent être reconfirmés au début de l’exécution. Aucun test applicatif n’a été relancé pour la seule rédaction de ce plan.

| Domaine | Acquis réutilisables | Limites à traiter |
|---|---|---|
| Structure | Six projets `Quran.Domain`, `Application`, `Infrastructure`, `Web`, `AudioProcessor`, `Import` ; migrations EF | Logique métier encore dans les contrôleurs ; services et modèles concurrents |
| Images MARW | 604 PNG et 604 JSON, manifeste et empreintes ; rapport structurel : 114 sourates, 6 214 références | Ce sont les comptes de ce paquet, pas une constante universelle ; contrôle structurel distinct de validation humaine |
| Géométrie | Conversion des coordonnées MARW et corrections des ouvertures de sourates | Revue des 114 ouvertures consignée ; ne prouve pas une revue humaine exhaustive des 6 214 ayat ; portions précises à compléter |
| Lecteur | `/mushaf/page/{pageNumber}` et alias Warsh ; sélection et surlignage ; écoute de passages | Deux lecteurs audio peuvent coexister ; lecture continue, téléchargements, portions graphiques et intégration aux pages publiques incomplètes |
| Texte | Entités `Surah`, `Ayah`, mots et import existants | Ancien référentiel non qualifié par rapport à MARW ; risque de texte/décompte d’une autre référence |
| Annotation avancée | `/Admin/AudioEditor`, `AudioSegment`, waveform et services de rendu | Liens vers les anciens `AyahId` ; ne constitue pas le même circuit que le nouvel annotateur |
| Annotation simplifiée | `/mushaf/audioreview`, sauvegarde en base, imports JSON, reprises et prévisualisation | Principalement Baqara, nombreuses valeurs spécifiques ; fin puis sauvegarde encore séparées ; interface à simplifier |
| Nouveaux passages | `MushafPassage`, références d’édition, temps, liens de reprise, versions | Deuxième modèle de segments ; source liée par chaînes ; workflow limité à Draft/Published ; pas de véritable audit de revue |
| Référentiel | 14 propositions amorcées en base ; CRUD, versions, archivage, refus de suppression si utilisées | Sources, contraintes et structure savante partiellement embarquées en JSON ; approbation rudimentaire ; couverture non exhaustive |
| Intégration | Prévisualisation sur MARW, filtre par وجه, invalidation après modification | `preview=true` n’est pas une autorisation ; publication directe d’un brouillon ; original utilisé pour la lecture publique |
| Sécurité | Middleware d’autorisation présent | Pas de circuit Identity/authentification complet dans `Program.cs` ; routes administratives et API à protéger globalement |
| Stockage | Répertoires sources/working/published ; HTTP Range via fichiers statiques | Toute la racine audio est exposée : les originaux et fichiers de travail ne sont pas isolés du public |
| Qualité | Tests .NET et scripts navigateur ; dernier document indique 18 tests unitaires ciblés réussis | Ce n’est pas une recette complète ; certains scripts touchent la base réelle et dépendent d’un autre projet pour Playwright |
| Exploitation | Compose PostgreSQL/MinIO ; binaires de tests dans `artifacts` | Déploiement Web/worker, secrets, restauration, supervision et lancement reproductible à finaliser |

### 2.1 Points d’entrée du code à examiner en premier

| Sujet | Fichiers principaux |
|---|---|
| Routes, authentification, fichiers statiques | `src/Quran.Web/Program.cs`, `Pages/Shared/_Layout.cshtml` |
| Annotations actuelles | `Controllers/AnnotationsController.cs`, `Pages/Mushaf/AudioReview.cshtml`, JavaScript associé sous `wwwroot` |
| Ancien circuit audio | `Controllers/AudioEditorController.cs`, `Infrastructure/Services/AudioEditorService.cs`, `Pages/Admin/AudioEditor.cshtml` |
| Modèles à réunifier | `Domain/Entities/AudioSegment.cs`, `MushafPassage.cs`, `Recitation.cs`, `SourceAudio.cs`, `AudioAsset.cs` |
| Référentiel | `Controllers/AwjuhController.cs`, `Domain/Entities/Wajh.cs`, `Infrastructure/Services/AwjuhCatalog.cs`, `Infrastructure/Data/awjuh-catalog.json` |
| Corpus graphique | `Infrastructure/Services/MarwPilotCorpus.cs`, `MarwOpeningReview`, `Infrastructure/Data/marw-opening-review.json` |
| Lecteur | `Pages/Mushaf/Page.cshtml` et `.cs`, `wwwroot/js/warsh-pilot.js`, `wwwroot/js/mushaf-playback.js` |
| Anciennes données publiques | `QuranReaderService.cs`, `AyahsController.cs`, `SurahsController.cs`, `SearchController.cs`, pages `Ayah`, `Surah`, `Index` |
| Persistance | `Infrastructure/Persistence/QuranDbContext.cs` et migrations, notamment `20260914102312_IntegratedAwjuhAndMushafPassages` |
| Imports et contrôles | Projets `Quran.Import`, `Quran.AudioProcessor`, `CorpusCheckerService.cs`, scripts sous `scripts/` |

Les chemins abrégés de ce tableau sont relatifs au projet concerné sous `src/Quran.*`. Localiser le fichier avant modification ; conserver les noms existants lorsqu’ils conviennent.

### 2.2 Blocages prioritaires

**P0 — Numérotation.** Le début d’Al-Baqara importé historiquement isole `الم` comme ayah 1. Dans le paquet MARW utilisé, la référence 2:1 couvre davantage de texte, jusqu’à `المتقين`, et la référence 2:4 se termine par `المفلحون`. Il est donc incorrect de rattacher automatiquement une ancienne annotation « 2:4 » à l’image MARW « 2:4 ». La correction passe par les limites textuelles et la vérification, pas par un décalage numérique global.

**P0 — Autorisations.** `AnnotationsController` permet actuellement une prévisualisation par paramètre et une publication avec une simple confirmation déclarative. Les routes de mutation du référentiel et les autres administrations doivent être soumises à une authentification et à des politiques serveur.

**P0 — Publication et médias.** Arrêter un lecteur JavaScript à `EndMs` ne limite pas les octets accessibles : son URL donne accès à l’original complet. Séparer la prévisualisation privée sur original et l’écoute publique des dérivés validés.

**P1 — Modèles concurrents.** `AudioSegment` et `MushafPassage` ne doivent pas rester deux vérités métier. `Recitation` ne doit pas écraser des occurrences ayant la même ayah et le même وجه.

**P1 — Ergonomie.** Les pages d’essai se sont accumulées. Le prochain travail doit aboutir à un seul éditeur et à un lecteur partagé, pas à une nouvelle démonstration isolée.

**P1 — Référentiel.** Les 14 entrées sont des propositions de portée limitée. Le système doit gérer l’enrichissement et la validation ; aucune interface ne doit afficher « tous les أوجه » à partir de ce nombre.

## 3. Produit cible et nettoyage de la navigation

### 3.1 Site public

| Écran | Fonctionnalités attendues |
|---|---|
| Accueil `/` | Présentation concise, accès aux 114 sourates, recherche, accès au mushaf, indication honnête de la disponibilité audio |
| Sourate `/surah/{number}` | Métadonnées du corpus actif, liste des ayat, accès aux images, disponibilité audio, lecture suivie lorsque couverte |
| Ayah `/quran/{surah}/{ayah}` | Référence qualifiée, extrait(s) d’image, occurrences publiées, choix des أوجه disponibles, lecture, téléchargement, partage, précédent/suivant |
| Mushaf `/mushaf/page/{pageNumber}` | Page MARW fidèle, zoom, navigation, sélection, surlignage, lecteur partagé et suivi de page |
| Recherche `/search` | Nom de sourate, numéro/référence, texte arabe normalisé ; résultats associés au corpus correct |
| Référentiel public `/awjuh` | Consultation des définitions approuvées et sources présentables ; accès aux occurrences disponibles ; aucune commande d’administration |

Les routes historiques simples restent utilisables pour le corpus actif explicitement indiqué. Les liens partagés durables incluent aussi l’identité stable du corpus ou de l’édition, par exemple un paramètre `edition`, ou une route canonique qualifiée. Un changement futur d’édition ne doit pas modifier le sens d’un lien déjà partagé. Documenter une seule convention.

Une ayah sans audio publié reste consultable. Afficher « لا يوجد تسجيل معتمد متاح لهذه الآية » et ne pas lui affecter artificiellement l’audio d’une ayah voisine.

### 3.2 Back-office en arabe

| Écran cible | Responsabilité |
|---|---|
| `/admin` | Tableau de bord, reprise du travail, couverture, anomalies et travaux en cours |
| `/admin/sources` | Inventaire, provenance, ingestion, métadonnées, statut des fichiers |
| `/admin/audio-editor/{sourceAudioId}` | Éditeur unique avec mode simple par défaut et réglages avancés |
| `/admin/review` | File des annotations à vérifier, approbation, rejet motivé, rendu et publication selon rôle |
| `/admin/awjuh` | CRUD du référentiel, voies, réalisations, contextes, contraintes, sources et révisions |
| `/admin/mushaf` | Éditions, qualification, correction/revue des régions et portions |
| `/admin/quality` | Rapports de cohérence, couverture et anomalies avec liens d’action |
| `/admin/jobs` | Imports, peaks, rendus, reprises après erreur et historique |

Séparer les layouts public et administratif. Utiliser des libellés arabes explicites ; garder les identifiants techniques dans les détails avancés. Les temps, chemins et codes s’affichent en LTR à l’intérieur du RTL pour éviter les inversions observées sur les compteurs.

### 3.3 Sort des pages existantes

| Page existante | Destination après recette du remplacement |
|---|---|
| `/mushaf/page/...` et alias `/mushaf/warsh/page/...` | Conserver un seul endpoint Razor et un alias non ambigu ; aucun doublon différant seulement par la casse |
| `/mushaf/audioreview` | Rediriger vers l’éditeur unique de la source concernée, après migration des brouillons et possibilité de récupérer les données locales |
| `/Admin/AudioEditor` | Faire évoluer en éditeur canonique ; conserver un adaptateur d’URL si nécessaire |
| `/mushaf/awjuh` | Retirer les mutations de cette route ; proposer un accès public en lecture et un lien admin autorisé |
| `/mushaf/audiodemo` | Archiver le scénario comme test ; retirer la route normale une fois le lecteur réel validé |
| `/mushaf/review` | Intégrer la revue graphique au back-office ; migrer/exporter les décisions stockées localement |
| `/Admin/MushafRegionEditor` | Réutiliser pour les corrections dans le circuit d’édition qualifié |
| `/Admin/Validation`, `/Admin/Inventory` | Intégrer aux écrans canoniques, sans dupliquer leurs fonctions |
| `/Ayah`, `/Surah`, accueil existant | Adapter aux services et références canoniques, en préservant les URLs utiles |

Pendant la transition, un flag serveur réserve les anciennes pages à un administrateur en développement. Un flag JavaScript ou un lien caché ne suffit pas. Pour les anciens liens dont la correspondance d’ayah est inconnue, montrer une explication et une navigation sûre ; ne pas rediriger vers une référence supposée équivalente.

## 4. Architecture cible dans la solution existante

### 4.1 Répartition des responsabilités

- **Domain** : identités de corpus, occurrences, référentiel, états, invariants, transitions et événements métier.
- **Application** : cas d’usage d’import, annotation, revue, publication, recherche, lecture et gestion du référentiel ; DTO typés ; politiques métier et orchestration.
- **Infrastructure** : EF/PostgreSQL, stockage, requêtes optimisées, audit persistant, implémentations des services et des tâches persistantes.
- **AudioProcessor** : FFprobe/FFmpeg, peaks et génération d’assets ; aucune décision d’ayah ou de وجه.
- **Import** : adaptateurs MARW/texte/audio, manifestes, validation de paquets et rapports de rapprochement.
- **Web** : Razor, API, authentification, autorisation, validation de requête et affichage ; pas de workflow métier écrit directement dans les contrôleurs.

Conserver un monolithe modulaire. Un worker peut être hébergé dans le même déploiement au départ, à condition que les travaux soient persistants et reprenables. Ne pas ajouter Redis, microservices ou moteur de recherche externe sans besoin mesuré.

### 4.2 Services métier à faire converger

Faire évoluer les interfaces existantes ou introduire leurs équivalents :

| Service | Contrat fonctionnel |
|---|---|
| Corpus/édition | Résoudre une référence qualifiée, les pages, les régions et les correspondances validées |
| Sources audio | Importer, identifier, contrôler l’intégrité, exposer les métadonnées et une prévisualisation autorisée |
| Annotation | CRUD d’occurrences, capture d’une fin, répétition, import historique, conflits et sauvegarde atomique |
| Référentiel des awjuh | CRUD/versionnement des objets, qualification, applicabilité, dépendances et retrait |
| Revue/publication | Contrôler les préconditions, enregistrer les décisions, déclencher le rendu et publier une révision |
| Lecture | Fournir uniquement les occurrences accessibles, leurs assets, plages textuelles/graphiques et ordre de lecture |
| Qualité | Calculer couverture, trous, conflits, références non résolues, médias manquants et dépendances périmées |

`AnnotationsController` et `AudioEditorController` doivent appeler **le même service d’annotation** pendant la compatibilité, puis l’adaptateur historique est retiré. Même règle pour les lecteurs publics et la page mushaf.

## 5. Modèle de données canonique

Les noms suivants sont des concepts cibles. Adapter les entités existantes avec des migrations additives, plutôt que créer des doublons pour respecter mécaniquement ces noms.

### 5.1 Corpus, texte et images

| Objet | Données / règles essentielles |
|---|---|
| `QuranCorpus` | Code stable, riwaya, système de décompte identifié, révision textuelle, provenance, statut de qualification |
| `CorpusAyah` / évolution de `Ayah` | FK corpus, sourate, ordinal dans ce corpus, texte original immuable, texte de recherche dérivé ; unicité corpus+sourate+ordinal |
| `AyahWord` | FK vers l’ayah de ce corpus, index et texte ; aucun index réutilisé implicitement entre corpus |
| `MushafEdition` | Corpus associé, éditeur/source, édition identifiée ou champ explicitement inconnu, révision du paquet, statut actif et manifeste |
| `MushafPage` | FK édition, numéro logique et éventuellement imprimé, dimensions réelles, URI versionnée, SHA-256 |
| `AyahImageRegion` | FK ayah/page, séquence, rectangle ou polygone normalisé, provenance, révision, statut de revue |
| `ReferenceMapping` | Corpus source/cible, références ou plages textuelles, cardinalité 1:1 / 1:N / N:1 / N:N, preuve, réviseur, statut |

Séparer l’identité du décompte, l’identité du texte et celle de la présentation graphique. Plusieurs éditions peuvent représenter le même corpus ; une numérisation nouvelle n’est pas automatiquement un nouveau décompte.

Une ayah peut avoir plusieurs régions et se prolonger sur plusieurs pages, même si le rapport du paquet actuel ne détecte pas de continuation. Une région doit vérifier les bornes normalisées et appartenir à l’édition de l’ayah référencée. Conserver les coordonnées brutes du fournisseur en provenance, puis la géométrie normalisée et les corrections comme couches versionnées.

Ne pas éditer manuellement le texte canonique pour le faire correspondre à une image. Importer un paquet textuel identifié, comparer les limites, puis qualifier le lien. Si le texte MARW équivalent n’est pas encore obtenu, rendre le manque visible et empêcher l’usage public d’un texte provenant d’un autre corpus comme s’il correspondait.

### 5.2 Source et occurrence audio

Faire de `AudioSegment` l’agrégat canonique en l’étendant ; migrer `MushafPassage` vers cet agrégat, puis retirer progressivement sa table active. Préserver les GUID si possible. En cas de collision, utiliser une table de correspondance d’identifiants documentée.

| Objet | Données / règles essentielles |
|---|---|
| `SourceAudio` | Récitateur, riwaya, attribution de voie éventuellement inconnue, sourate(s), provenance, clé interne, empreinte, durée, codec et statut d’intégrité |
| `AudioSegment` | ID stable, source FK, temps entiers en ms, type, ordre, lien de reprise, état éditorial, version de concurrence |
| `SegmentRevision` | Snapshot immuable des données sémantiques d’une occurrence : temps, référence, portion, attribution ; auteur/date |
| `SegmentTextSpan` | Référence qualifiée, plage de mots facultative et ordre ; permet les cas couvrant plusieurs unités après rapprochement |
| `SegmentImageRegion` | Portion graphique propre à une révision du segment et à une édition ; séquence et statut de validation |
| `SegmentWajhAssignment` | Révision du وجه attribué, contexte, statut de l’attribution, preuve/note et réviseur ; absence d’attribution autorisée |
| `AudioAsset` | Source ou dérivé identifié, segment+révision si dérivé, format, durée, taille, checksum, état de génération |
| `Publication` | Révision précise, asset précis, date/auteur, état public ou retiré et motif |
| `AuditLog` | Acteur réel, action, objet, versions, avant/après pertinent, date et corrélation |

Prévoir des brouillons à référence non résolue : source et temps peuvent être conservés avant qualification. Une telle occurrence reste impossible à approuver/publiquer. Stocker sa référence historique avec son origine ; ne pas la convertir en référence MARW par simple attribution d’un nom d’édition.

Pour les cas ordinaires, le formulaire affiche une seule ayah. Les spans multiples ne compliquent que les cas qui en ont réellement besoin. Une introduction, la basmala ou l’isti‘adha reçoit un type/contexte adapté sans décaler artificiellement les numéros d’ayat.

`Recitation` peut devenir une projection de lecture ou une collection regroupant des occurrences. Il ne doit plus imposer « une seule occurrence par ayah et وجه ». Les consommateurs historiques sont adaptés à cette nouvelle sémantique.

### 5.3 Invariants audio

1. `0 <= StartMs < EndMs <= SourceDurationMs` ; tolérance technique documentée pour l’arrondi FFprobe.
2. Source, hash, corpus et références existent et sont cohérents avant approbation.
3. Indices de mots : tous deux absents ou tous deux valides, dans le bon corpus ; début inférieur ou égal à fin.
4. Une reprise pointe vers une occurrence compatible de la même source ; pas de cycle. Les liens de reprise sont préservés en migration.
5. Un chevauchement temporel déclenche un avertissement contextualisé : il n’est pas systématiquement interdit, car plusieurs annotations peuvent décrire le même matériau.
6. Un intervalle non annoté reste un trou explicite. Il n’est jamais affecté automatiquement à une ayah.
7. Un type `FullAyah` doit être vérifié comme couvrant l’ayah entière du corpus choisi ; un ancien libellé ne constitue pas une preuve.
8. Les publications pointent vers des révisions immuables ; aucun écrasement de média entre occurrences.
9. Les changements sémantiques invalident les approbations dépendantes selon une politique explicite ; une modification cosmétique du libellé ne doit pas dépublier arbitrairement des milliers d’audios.
10. Concurrence réellement gérée avec PostgreSQL : choisir un token EF adapté, vérifier les mises à jour concurrentes sur PostgreSQL réel ; ne pas supposer qu’un `byte[] RowVersion` se comporte comme SQL Server.

### 5.4 Référentiel des أوجه : CRUD et versionnement réels

Le système doit distinguer :

- **رواية / طريق / sous-voie** : périmètre de transmission, avec hiérarchie et références documentaires.
- **Règle / réalisation** : phénomène identifié et valeur de réalisation.
- **وجه** : définition structurée, éventuellement composée de plusieurs réalisations, avec un contexte d’application.
- **Contrainte** : dépendance, exclusion, condition ou exception, avec preuve et état de revue.
- **Occurrence audio** : réalisation effectivement entendue dans une source, pouvant être non identifiée ou seulement proposée.

Tables ou collections relationnelles à gérer depuis l’administration : `TransmissionRoute`, `ReadingRule`, `ReadingRealization`, `WajhDefinition`, `WajhRevision`, `WajhComponent`, `ApplicationScope`, `CombinationConstraint`, `ReferenceSource`, `EvidenceCitation`.

Ne pas enfouir toutes les relations dans `DefinitionJson`. Un JSON versionné peut conserver le document source ou une expression structurée validée, mais les identités, liens, statuts et champs utilisés pour filtrer doivent être requêtables et contraints en base.

Fonctions obligatoires :

| Opération | Comportement |
|---|---|
| Créer | Brouillon, identifiant/code stable, voie et portée explicites ; attribution non validée visible comme telle |
| Modifier un brouillon | Champs structurés modifiables, contrôle de version, audit |
| Modifier une définition approuvée | Nouvelle révision sémantique ; ancienne révision conservée ; rapport des occurrences affectées |
| Corriger un libellé | Révision/audit éditorial sans invalidation sémantique injustifiée |
| Soumettre / approuver / rejeter | Décision motivée, auteur habilité, références et périmètre vérifiés |
| Archiver / retirer | Empêcher les nouvelles attributions ; traiter explicitement les publications concernées sans effacer l’historique |
| Supprimer | Seulement un brouillon jamais référencé ; sinon archivage/retrait |
| Dupliquer | Nouvelle identité, état brouillon, provenance de la duplication, jamais copie d’une approbation |
| Rechercher | Voie, règle, contexte, statut, source, ayah ou mot et usages audio |
| Consulter les usages | Liste des segments liés avec leurs révisions, état et action de requalification |
| Importer/exporter | Schéma versionné, validation préalable, comparaison et rapport ; aucun écrasement silencieux |

Les trois codes initiaux `azraq-shatibiyya`, `azraq-tayyiba`, `asbahani-tayyiba` sont des regroupements documentaires, pas un arbre exhaustif des chaînes. Leur migration conserve cette réserve.

Les 14 propositions existantes se répartissent en quatre combinaisons Azraq/Shatibiyya et dix branches d’un contexte Asbahani au début de Yasin. Les quatre contraintes documentaires sont des objets distincts. Ne pas transformer ces nombres en une liste exhaustive ni généraliser les branches de Yasin à tout le Coran.

Pour atteindre « tous les أوجه », créer une **matrice de couverture par voie, ouvrage, chapitre, contexte et état de dépouillement**. Une ligne peut être non étudiée, extraite, à résoudre, revue ou hors périmètre motivé. Ne pas générer toutes les combinaisons cartésiennes de réalisations : certaines sont dépendantes ou incompatibles.

L’interface d’annotation ne propose comme candidates que les définitions actives dont le contexte est compatible ou encore à vérifier. Le lecteur public ne propose que celles ayant une attribution approuvée et au moins une occurrence publiée pour le passage demandé. Une définition présente dans le catalogue ne prouve pas que le cheikh l’a enregistrée.

## 6. Éditeur unique : comportement demandé par l’utilisateur

### 6.1 Mode simple par défaut

L’écran affiche la source, le lecteur/waveform, l’image MARW, l’ayah courante, le début courant et trois actions principales :

- **« إنهاء الآية والانتقال إلى التالية »** — terminer l’ayah et passer à la suivante.
- **« إنهاء التكرار »** — terminer une reprise, lorsqu’un mode reprise a été activé pour une occurrence identifiée.
- **« تراجع عن آخر إجراء »** — annuler la dernière capture tant que cela ne contredit pas une modification ultérieure.

La lecture/pause reste séparée. Ici « arrêter » signifie arrêter la lecture/capturer un repère, pas enregistrer au microphone. Il n’y a pas de capture micro à ajouter.

Au clic sur « fin d’ayah » :

1. Capturer le temps courant du lecteur en millisecondes.
2. Construire l’occurrence `[débutCourant, tempsCapturé]` avec la référence qualifiée courante.
3. Enregistrer atomiquement l’occurrence et le curseur de session, avec version et clé d’idempotence.
4. Après succès, définir le début suivant **exactement** au temps capturé.
5. Définir la référence suivante en consultant le corpus, pas avec une liste codée en dur ni un compteur hérité.
6. Charger l’image/page correspondante et conserver le contexte d’écoute. La reprise automatique de lecture peut être une préférence explicite.
7. Afficher une confirmation brève. Ne pas demander un second clic « enregistrer ».

À la dernière ayah d’une sourate, terminer la session ou proposer la source suivante disponible ; ne pas inventer une ayah supplémentaire.

### 6.2 Répétitions et أوجه

Après une fin d’ayah, l’éditeur conserve la dernière occurrence comme cible possible de reprise. Si le cheikh répète sa fin, l’annotateur active « تكرار للآية السابقة » ; la cible est visible avant capture. Terminer cette reprise :

- enregistre un segment distinct avec `RepeatOfId` et les temps propres ;
- avance le début temporel de travail à la fin de cette reprise ;
- conserve l’ayah suivante en attente, sans incrémenter une seconde fois son numéro ;
- permet une autre reprise de la même cible ;
- n’affecte aucun وجه automatiquement.

Une reprise peut avoir le même وجه, un autre وجه, ou une attribution inconnue. Utiliser « تكرار » pour le fait audible ; réserver « وجه مختلف » à une observation et « وجه معتمد » à une attribution réellement approuvée. La simple répétition ne prouve pas un changement de وجه.

Pour une pause au milieu d’une ayah, proposer dans les options « مقطع من الآية » et « استكمال الآية ». Ces actions ne passent à l’ayah suivante que lorsque l’annotateur indique explicitement la fin complète. Cette distinction évite que chaque pause du cheikh provoque un incrément de numéro.

### 6.3 Options avancées du même écran

- WaveSurfer avec régions, zoom, timeline, minimap et lecture de contexte avant/après.
- Déplacement/redimensionnement des bornes ; ajustements fins et pas rapides.
- Temps lisibles et saisie en ms ; compteurs LTR, durée correcte des fichiers de plusieurs heures.
- Liste des occurrences avec type, état, ayah, reprise, attribution et conflit éventuel.
- Choix du وجه dans le référentiel partagé, affichage de sa portée et de ses sources.
- Sélection de mots si le texte est qualifié, ou de plusieurs régions graphiques d’une portion.
- Dupliquer, supprimer un brouillon, annuler, reprendre une session, aller au prochain intervalle non traité.
- Prévisualisation dans le lecteur réel avec bandeau de brouillon et autorisation serveur.
- Export JSON de secours et import des anciens fichiers ; ces fonctions ne sont plus le parcours normal.

Ne pas décoder les quatre heures d’Al-Baqara en entier dans le navigateur. Produire des peaks à plusieurs résolutions côté serveur, associés au hash source et couvrant toute sa durée. Charger ce qui est nécessaire à la fenêtre affichée. Les peaks actuels des six premières minutes servent de fixture, pas de solution générale.

### 6.4 Sauvegarde, conflits et confort

Sauvegarder une occurrence par commande, pas renvoyer toute la collection en recalculant le SHA du MP3 de 236 Mo à chaque clic. Calculer hash et durée à l’ingestion, conserver leur résultat et programmer les recontrôles d’intégrité.

Les métadonnées de source doivent provenir du serveur. Le navigateur transmet un `SourceAudioId`, pas un chemin disque libre à faire ouvrir par FFmpeg.

Conserver un brouillon local de secours et un état visible : enregistré / en attente / échec / conflit. En cas d’échec, la capture ne doit pas être perdue ; ne pas afficher une avance comme durablement enregistrée tant que le serveur n’a pas confirmé. Un double clic ou une relance réseau avec la même clé retourne le même résultat.

Un conflit montre les versions et permet de reprendre/rejouer l’édition ; il ne remplace pas silencieusement le travail serveur par le local. Les raccourcis sont désactivés pendant la saisie dans un champ. Tester clavier, écran étroit et usage prolongé.

## 7. Utilisation des portions sur les images et à l’écoute

### 7.1 Une seule chaîne de données

```text
Source immuable
  → occurrence annotée et révision
  → référence(s) qualifiée(s) + portion(s) + attribution éventuelle
  → revue humaine
  → asset dérivé de cette révision
  → publication
  → mêmes données dans la page ayah, la page sourate et le lecteur mushaf
```

Il ne doit plus exister un JSON de démonstration figé pour montrer l’effet des annotations. Dès qu’un brouillon est sauvegardé, sa prévisualisation privée utilise les mêmes DTO et composants que la lecture publique, avec une politique d’accès différente.

### 7.2 Surlignage

Pour une ayah complète, réutiliser ses régions validées. Pour une portion, utiliser les régions propres à cette portion ou une correspondance mot→région qualifiée. Ne pas surligner l’ayah entière en prétendant montrer le mot exact.

Si seule la localisation de l’ayah est connue, le lecteur peut montrer un surlignage de contexte clairement distinct ; l’interface de revue signale que la portion précise reste à qualifier. Le mode « portion exacte » n’est disponible que si la géométrie correspondante existe et est validée.

Gérer plusieurs lignes, plusieurs régions et les changements de page. Le surlignage actif suit l’occurrence lue, y compris lors d’une reprise. Un segment sans minutage interne permet un surlignage global de ses régions ; ne pas inventer une animation mot à mot.

### 7.3 Choix d’un وجه et lecture continue

Le choix d’un وجه filtre les occurrences **disponibles pour la référence**, en préservant les différentes prises et répétitions. Une portion ne doit jamais être présentée comme une ayah entière.

Définir deux comportements distincts dans le service de lecture :

1. **Écoute d’une occurrence** : joue un asset complet correspondant à cette occurrence ; arrêt ou répétition à la fin.
2. **Lecture suivie** : construit une séquence de publications validées dans l’ordre ; conserve le choix compatible du visiteur et gère le changement d’ayah/page.

En cas de manque pour le وجه choisi, afficher l’indisponibilité et proposer les alternatives ; ne pas changer silencieusement de وجه. Si une ayah n’existe qu’en fragments, ne la déclarer complète qu’avec une composition éditoriale explicitement vérifiée : ordre, couverture textuelle, compatibilité d’attribution et absence de coupure trompeuse.

Ne pas assembler automatiquement le début d’une prise et la fin d’une autre pour fabriquer une récitation supposée complète. Les montages, s’ils deviennent nécessaires, sont des objets dérivés distincts, documentés et approuvés.

Pour une lecture de la source intégrale, prévoir éventuellement une publication explicite de cette source avec ses droits et son état de validation. Elle ne découle pas automatiquement de la publication de quelques segments.

### 7.4 Téléchargement et partage

Chaque occurrence publiée dispose d’un asset téléchargeable, d’un nom lisible, d’un identifiant stable et des métadonnées de récitation. Un téléchargement doit contenir seulement le passage annoncé. Fournir une URL de partage vers l’ayah et l’occurrence sélectionnée, avec l’identité d’édition nécessaire ; ne pas partager un chemin physique ou un timestamp sans source.

Le lecteur gère le chargement, les erreurs, le seek, la vitesse, la boucle et les restrictions d’autoplay du navigateur. Un seul contrôleur de lecture empêche deux pistes simultanées.

## 8. Workflow serveur, médias et sécurité

### 8.1 États et décisions

Conserver le workflow éditorial du plan initial :

```text
Draft → Reviewed → Approved → Rendering → Rendered → Published
          ↘ Rejected / retour en Draft avec motif
Published → retrait de publication explicite
```

Le statut technique du job peut être séparé de l’état éditorial : un rendu échoué ne supprime pas la décision d’approbation. Le retry repart de la même révision approuvée. La publication exige un asset prêt et vérifié ; elle ne lance pas un rendu synchrone dans la requête HTTP.

Avant approbation : contrôler source/intégrité, limites temporelles, référence de corpus, caractère complet ou partiel, liens de reprise, géométrie nécessaire et attribution. L’approbation d’une définition de وجه et celle de son attribution à un enregistrement sont deux décisions séparées.

Les occurrences sans attribution de وجه peuvent être approuvées et publiées comme telles si leur contenu et leur référence ont été vérifiés. Elles ne figurent pas comme exemples certifiés d’un وجه précis.

Chaque décision conserve l’acteur réel et les versions contrôlées. Une case `ReferenceChecked=true` sans identité, preuve conservée et transitions serveur n’est pas suffisante.

### 8.2 Modification après publication

- Une correction de temps, source, portion ou attribution crée une nouvelle révision en Draft.
- L’ancienne publication reste un snapshot identifiable. Si la correction révèle une erreur de contenu, la retirer explicitement et immédiatement avec motif ; une simple nouvelle proposition ne doit pas modifier ses octets en place.
- Une invalidation d’une définition/revue requise retire ou bloque les publications qui en dépendent conformément à une politique testée, avec liste d’impact.
- Une correction cosmétique de nom ne rend pas automatiquement faux tous les audios liés.
- Interdire la suppression directe d’une occurrence publiée. Retirer sa publication et conserver l’audit.

### 8.3 Rôles et accès

Installer ASP.NET Core Identity, cookies sécurisés, pages de connexion et gestion du premier administrateur sans mot de passe prédéfini publié. Utiliser `UseAuthentication()` avant `UseAuthorization()` et des policies explicites sur pages **et API**.

| Action | Public | Reviewer | Admin |
|---|---:|---:|---:|
| Lire les publications | Oui | Oui | Oui |
| Accéder aux originaux / brouillons | Non | Oui | Oui |
| Créer/modifier des annotations non publiées | Non | Oui | Oui |
| Revoir/approuver/rejeter un contenu | Non | Oui, avec habilitation concernée | Oui, avec décision tracée |
| Gérer les définitions, voies et sources du référentiel | Non | Lecture/proposition selon politique | Oui |
| Importer des sources, lancer les rendus, publier/retirer | Non | Non | Oui |
| Gérer les comptes et paramètres | Non | Non | Oui |

Une habilitation applicative ne constitue pas à elle seule une compétence savante : enregistrer le responsable de la qualification des أوجه et son périmètre. Le logiciel peut être testé avec des données fictives en base de test ; ne pas approuver du contenu réel uniquement pour faire passer une démonstration.

Protéger les mutations utilisant des cookies contre le CSRF. Valider tailles, schémas, chemins, types, plages et pagination côté serveur. Échapper les textes de notes/sources à l’affichage. Les liens externes du référentiel n’autorisent pas une récupération serveur arbitraire.

### 8.4 Stockage et rendu

- Retirer l’exposition statique de toute la racine `data/audio`.
- Originaux et working privés ; accès de revue par endpoint autorisé ou URL signée courte, avec support Range.
- Dérivés accessibles publiquement uniquement par une publication active ; traiter également les anciens chemins statiques.
- Clé dérivée proposée : `published/{segmentId}/{revisionId}/{assetId}.mp3` ; les noms lisibles du téléchargement sont indépendants.
- FFmpeg reçoit une liste d’arguments contrôlés, sans commande shell construite depuis des champs utilisateur.
- Rendu précis, normalisation technique documentée, conservation des sources ; vérifier code retour, durée attendue/tolérance, décodabilité, taille et checksum.
- Job persistant identifié par segment+révision+profil de rendu ; retry idempotent, annulation et nettoyage des fichiers temporaires maîtrisés.
- Si le segment change pendant le rendu, ne jamais rattacher le résultat à la nouvelle révision.
- Écrire d’abord l’asset, vérifier, puis enregistrer la disponibilité ; gérer les orphelins et l’échec entre stockage et transaction DB.
- Images originales immuables ; dérivés WebP éventuels versionnés et vérifiés visuellement. Ne pas rendre WebP obligatoire avant d’avoir mesuré le besoin.
- Cache `immutable` uniquement pour des ressources à URL versionnée. Prévoir l’invalidation des manifestes et la politique de retrait des publications au CDN.

## 9. API et intégration à réaliser

Les routes ci-dessous décrivent les contrats cibles ; adapter les routes existantes par une compatibilité temporaire. Générer une documentation OpenAPI correspondant au code.

| Famille | Contrats nécessaires |
|---|---|
| Corpus public | Sourates, ayat qualifiées, pages, régions validées, recherche paginée |
| Lecture publique | Occurrences publiées par référence/face, séquence de lecture, métadonnées de téléchargement |
| Référentiel public | Définitions approuvées et disponibles, sans brouillons ni commandes admin |
| Sources admin | Inventaire, import, état, métadonnées, peaks, flux privé |
| Segments admin | GET/POST/PUT/DELETE de brouillons, duplication, capture-fin, capture-reprise, import historique |
| Sessions admin | Curseur source/temps/référence, reprise, journal des captures et annulation contrôlée |
| Revue admin | Soumission, approbation, rejet motivé, demande de rendu, publication/retrait |
| Référentiel admin | CRUD des définitions et objets liés, révisions, revue, rapport d’impact et archivage |
| Géométrie admin | Correction de régions/portions, revue, historique et activation de révision |
| Qualité/jobs | Rapports, anomalies, relance de job et progression |

Pour les mutations : DTO typés, token de version/`If-Match`, erreurs structurées `ProblemDetails`, codes HTTP cohérents, clés d’idempotence pour les captures/imports/jobs. Un retry identique doit être distingué d’une tentative d’écrasement avec une ancienne version.

Supprimer le contournement public `preview=true`. Les données de prévisualisation sont fournies uniquement par un endpoint soumis à la policy de revue ; le composant UI reste partagé. Les filtres publics doivent être appliqués côté serveur, y compris à l’accès direct par ID.

## 10. Migration des données : procédure conservatrice

### 10.1 Avant toute migration

1. Identifier la base réellement utilisée par l’application et celle décrite dans Compose ; ne pas supposer qu’elles sont identiques.
2. Sauvegarder PostgreSQL, les exports utilisateurs, les manifestes et les données audio/images. Tester la restauration dans une base isolée.
3. Produire un inventaire : tables, comptes par état, sources, hashes, segments, publications, dépendances, identifiants orphelins et migrations appliquées.
4. Repérer les données temporaires issues de tests par preuve explicite. Ne pas effacer un élément simplement parce que son nom paraît expérimental.
5. Réaliser la migration en copie, produire un rapport avant/après, puis basculer l’application après recette.

### 10.2 Migration additive puis bascule

| Étape | Action | Contrôle bloquant |
|---|---|---|
| M01 | Ajouter corpus/édition et références qualifiées ; conserver le corpus historique séparément | Aucune modification en place du texte ou des numéros historiques |
| M02 | Importer le paquet MARW et sa provenance dans les structures canoniques | Fichiers/hash/dimensions/comptes conformes au manifeste ; pas d’approbation humaine automatique |
| M03 | Ajouter révisions, audit, sources FK et état de mapping aux occurrences | Sources retrouvées par clé+hash ; cas inconnus conservés non résolus |
| M04 | Migrer `MushafPassage` vers l’agrégat de segments | IDs/temps/notes/liens de reprise préservés ; nombre d’objets expliqué |
| M05 | Rapprocher les anciens `AudioSegment` et `Recitation` | Aucun rapprochement par seul numéro ; cas ambigus en file de revue |
| M06 | Migrer les 14 propositions et leurs métadonnées en référentiel structuré | États existants conservés avec provenance ; pas de promotion automatique en Approved |
| M07 | Adapter lecteur/éditeur/API au service commun | Lecture et sauvegarde sur une seule vérité ; pas de double écriture durable |
| M08 | Mettre les anciens modèles en lecture seule, comparer les projections | Aucun consommateur actif oublié ; rapports de couverture cohérents |
| M09 | Retirer les adaptateurs puis les structures obsolètes dans une migration ultérieure | Sauvegarde restaurable, compatibilité validée et délai de retour défini |

Ne pas modifier une migration déjà appliquée, notamment `20260914102312_IntegratedAwjuhAndMushafPassages`. Ajouter de nouvelles migrations. Les corrections de contenu et la migration de structure sont journalisées séparément.

Le bootstrap actuel peut réinsérer une définition supprimée si son code manque. Le remplacer par un journal d’import/version de seed et un mécanisme explicite de mise à jour. Un redémarrage ou une migration ne doit pas annuler une suppression volontaire ni ressusciter les anciens codes d’awjuh.

### 10.3 Traitement des exports de l’utilisateur

Conserver les fichiers originaux dans une archive d’import privée avec empreinte et date :

- `C:\Users\r_kaci\Downloads\baqarah-audio-passages-draft.json` ;
- `C:\Users\r_kaci\Downloads\baqarah-audio-passages-draft(1).json`.

Le dernier export décrit cinq occurrences avec les bornes suivantes, exprimées en millisecondes :

| Référence déclarée dans l’export | Début | Fin | Traitement |
|---|---:|---:|---|
| 2:1 | 13 974 | 22 883 | Conserver, référence à qualifier |
| 2:2 | 22 883 | 34 217 | Conserver, référence à qualifier |
| 2:3 | 34 217 | 48 808 | Conserver, référence à qualifier |
| 2:4 | 48 808 | 67 986 | Conserver, référence à qualifier |
| 2:5 | 84 592 | 102 310 | Conserver, référence et observation de وجه à qualifier |

**Trou réel de cet export : 67 986 à 84 592 ms, soit 16,606 s.** Le premier export contient des observations de reprises dans une zone proche, mais aussi des erreurs reconnues de référence. Ne pas fusionner automatiquement les deux fichiers et ne pas remplir ce trou par interpolation. Présenter les propositions côte à côte à la personne qui écoute.

Les cinq éléments ont été importés précédemment comme Draft dans le circuit MARW, sans certification de référence. Vérifier leur état réel au moment de la reprise. Préserver les GUID ; afficher l’étiquette historique et l’état « correspondance à vérifier ». Une répétition n’acquiert pas une attribution de وجه du seul fait de son import.

### 10.4 Retour arrière

Pendant la bascule, prévoir une courte suspension des écritures ou une procédure explicite de reprise des nouvelles écritures. Un retour à un ancien binaire ne doit pas perdre les annotations créées après migration.

Retour applicatif possible tant que le schéma est additif ; restauration complète uniquement selon la procédure testée, avec export des écritures récentes. Conserver le paquet source et le binaire précédent jusqu’à la fin de la recette. Ne supprimer ni les originaux ni les exports dans une opération de nettoyage.

## 11. Lots d’implémentation et critères de sortie

Exécuter les lots dans cet ordre. Un lot livré doit comporter code, migrations éventuelles, tests appropriés, procédure de vérification et état documenté. Ne pas s’arrêter à une page de démonstration ni demander « continuer ? » après chaque correction ordinaire.

### Lot 0 — Sécuriser la reprise et établir la référence

**Dépendance : aucune.**

- Relever les instructions locales, le SDK, les outils, les routes, le processus actif, la configuration et la base réelle sans exposer les secrets.
- Vérifier l’existence d’un dépôt Git ; l’initialiser s’il manque avec un `.gitignore` adapté, sans embarquer les MP3, dumps, secrets et sorties de build.
- Sauvegarder et restaurer en environnement isolé ; inventorier les fichiers et associations.
- Isoler les tests qui utilisent actuellement la base réelle ou le réseau.
- Écrire `docs/ETAT_DU_SYSTEME.md` et un backlog reprenant les identifiants des lots de ce plan.

**Sortie :** reprise reproductible depuis une copie, base de test séparée, inventaire daté et absence de perte de données. Le site local actuel reste testable.

### Lot 1 — Fermer les accès administratifs et les médias privés

**Dépendance : lot 0.**

- Installer Identity et les rôles/policies ; protéger toutes les routes de mutation existantes, même celles qui seront retirées.
- Séparer public/admin dans la navigation et les layouts.
- Supprimer l’accès anonyme aux brouillons, originaux et working ; remplacer les flux de prévisualisation par un accès autorisé supportant Range.
- Retirer les chemins de secrets du code/Compose ; utiliser des variables et exemples sans valeurs sensibles.
- Préserver la possibilité de tester le pilote avec un compte administrateur local.

**Sortie :** tests HTTP démontrant qu’un anonyme ne peut ni modifier, ni publier, ni lire un brouillon/original par URL directe ou paramètre `preview`. Les pages publiques continuent à fonctionner.

### Lot 2 — Fixer le corpus et les références

**Dépendance : lots 0–1.**

- Introduire le modèle corpus/édition et importer le paquet MARW existant sans retéléchargement inutile.
- Qualifier le texte associé ; produire le rapport de différences avec l’ancien texte/décompte.
- Mettre en place la table de correspondances, ses états et son écran de résolution.
- Versionner la géométrie et importer les corrections d’ouverture avec leur provenance, sans les transformer en décisions humaines fictives.
- Résoudre humainement les premières références nécessaires au pilote ; le reste peut rester explicitement non résolu.

**Sortie :** aucune référence ambiguë dans les contrats ; démonstration du cas Al-Baqara initial ; un ancien ordinal ne devient pas MARW par magie ; recherche et image utilisent le corpus indiqué.

### Lot 3 — Réunifier les occurrences et migrer les brouillons

**Dépendance : lot 2 pour le schéma ; les correspondances non résolues restent des brouillons.**

- Étendre `AudioSegment`, révisions, spans, audit et source FK.
- Migrer `MushafPassage`, adapter `Recitation` et les consommateurs historiques.
- Extraire les règles de `AnnotationsController` dans Application ; appliquer concurrence/idempotence.
- Importer/reprendre les exports avec rapport, sans fusionner leurs affirmations contradictoires.
- Ajouter la session d’annotation persistante.

**Sortie :** une seule occurrence métier par ID, liens et temps préservés, brouillons utilisables après redémarrage, imports répétables sans doublon et conflits visibles.

### Lot 4 — Achever le référentiel administrable

**Dépendance : lots 2–3.**

- Déployer les tables structurées, migrer les propositions, rendre voies/sources/composants/contextes/contraintes administrables.
- Ajouter CRUD, recherche, révisions, approbation, archivage et rapport d’impact.
- Remplacer le bootstrap réinsertant les éléments supprimés.
- Brancher éditeur et lecteur sur les requêtes communes d’applicabilité/disponibilité.
- Créer la matrice de couverture documentaire, sans inventer de définitions pour remplir les cases.

**Sortie :** créer une définition en base de test, la retrouver dans l’éditeur, lui attribuer une occurrence de test, approuver les deux, la proposer au lecteur ; modifier/retirer selon la politique et vérifier l’impact. Aucune liste parallèle de faces dans le JavaScript.

### Lot 5 — Livrer l’éditeur unique et simple

**Dépendance : lots 3–4.**

- Réunir la puissance de l’ancien éditeur et la capture simple dans le même écran arabe.
- Implémenter la commande atomique « fin d’ayah », la cible de reprise, l’avancement temporel et de référence, l’annulation et la reprise de session.
- Ajouter les options avancées et la sélection de portions graphiques.
- Générer les peaks pour toute source et toute durée ; supprimer les dépendances spécifiques à Baqara du code de production.
- Réduire les boutons concurrents et les confirmations répétitives ; distinguer enregistrement, revue et publication.

**Sortie :** une session de dix captures se fait sans ressaisir les débuts/numéros ; deux reprises successives restent rattachées à la bonne ayah ; un échec réseau/double clic ne perd ni ne duplique la capture ; le navigateur supporte la source longue.

### Lot 6 — Achever revue, rendu et publication

**Dépendance : lots 3–5.**

- Imposer les transitions serveur et l’audit avec les acteurs réels.
- Créer la file de revue, les motifs de rejet, les contrôles de référence/portion/attribution.
- Rendre les tâches FFmpeg persistantes, idempotentes et liées aux révisions.
- Ajouter publication et retrait explicites, vérifications d’asset et accès média conforme.
- Tester la modification d’un segment pendant un rendu et la requalification d’une attribution.

**Sortie :** impossible de publier un Draft ou un rendu absent/obsolète ; aucun remplacement de média entre deux occurrences du même وجه ; retrait effectif selon la politique de diffusion et cache documentée.

### Lot 7 — Terminer le site public cohérent

**Dépendance : lots 2, 4 et 6.**

- Adapter accueil, sourate, ayah et recherche au corpus canonique.
- Construire le lecteur partagé, le filtre de face disponible, les reprises/portions et la lecture suivie.
- Ajouter téléchargement, partage, liens canoniques, métadonnées SEO et états vides.
- Assurer zoom, régions multilignes, changement de page, mobile, clavier et RTL.
- Retirer la dépendance de la démonstration au JSON figé.

**Sortie :** depuis une annotation validée, le visiteur retrouve l’occurrence dans tous les parcours, entend/télécharge le même passage, voit la bonne image et partage un lien qui rouvre la même sélection.

### Lot 8 — Industrialiser l’ingestion et la qualité

**Dépendance : lots 3, 6–7.**

- Inventorier les 114 sources attendues du corpus audio, avec rapprochement explicite du fichier et de la sourate ; vérifier ce qui est réellement local.
- Téléchargements reprenables, checksums, FFprobe, statut de droits/provenance, peaks et jobs sans doublons.
- Ajouter les rapports de couverture par sourate, ayah, face, source, géométrie et état de validation.
- Contrôler trous/chevauchements, médias manquants, publications obsolètes et correspondances inconnues.
- Permettre l’annotation de n’importe quelle source depuis l’interface, sans modification du code.

**Sortie :** une nouvelle sourate suit tout le circuit sans constante ajoutée ; un job échoué est relançable ; couverture calculée à partir des données et non du nombre de fichiers téléchargés.

### Lot 9 — Nettoyer le projet et rendre l’exécution reproductible

**Dépendance : remplacements des lots précédents recettés.**

- Appliquer la table de retrait des pages ; retirer contrôleurs/services/JS inutilisés après recherche de leurs consommateurs.
- Déplacer la recherche historique sous documentation d’archive ; conserver manifestes et preuves utiles hors navigation produit.
- Réduire `artifacts` aux sorties nécessaires et documenter une seule commande de lancement ; ne pas effacer les originaux comme des fichiers de build.
- Créer les dépendances Playwright propres au projet avec lockfile ; ne plus dépendre de `antigravity_test` ou d’un chemin utilisateur.
- Supprimer les fixtures synthétiques de la base de travail uniquement lorsqu’elles sont identifiées et sauvegardées.
- Réécrire un README opérationnel court renvoyant vers les documents canoniques.

**Sortie :** aucune page d’essai dans le parcours normal, pas de doublon d’éditeur ou de référentiel actif, démarrage et tests possibles depuis un autre emplacement configuré.

### Lot 10 — Recette, exploitation et livraison

**Dépendance : lots 0–9.**

- Compléter Docker/Compose avec Web et worker selon le mode retenu, proxy et volumes persistants ; fixer les versions d’images retenues après vérification.
- Séparer Development/Testing/Staging/Production ; traiter les secrets, Data Protection, TLS et accès PostgreSQL/stockage.
- Ajouter logs structurés, identifiants de corrélation, santé Web/DB/stockage/worker, alertes disque et jobs en erreur.
- Mettre en place CI build/tests/migrations sur base vide et copie représentative, sauvegarde quotidienne et restauration documentée.
- Mesurer les performances sur un environnement et un corpus décrits ; vérifier la compatibilité/support du runtime imposé avant exposition publique et documenter toute décision d’évolution nécessaire.
- Exécuter la recette humaine du pilote, puis produire le dossier de livraison et la liste précise des contenus restant à qualifier.

**Sortie :** lancement reproductible, restauration démontrée, critères fonctionnels et sécurité satisfaits, aucune affirmation trompeuse sur la couverture éditoriale.

## 12. Tests et recette obligatoires

### 12.1 Règles de la suite de tests

- Les tests automatisés utilisent une base PostgreSQL et un stockage isolés. Interdire dans le harnais de test l’utilisation accidentelle de la base de travail/production.
- Tests unitaires : invariants et transitions métier ; éviter les tests qui répètent simplement l’implémentation.
- Intégration : migrations, FK, transactions, concurrence, requêtes filtrées, Identity et médias.
- Navigateur : parcours utilisateur réels et vérification des erreurs visibles ; fixtures identifiées comme fictives.
- Tests dépendant du réseau externe séparés et explicites ; le build ordinaire n’importe pas de pages ni d’audios dans la base de travail.
- Les scripts actuels sont des points de départ. Une capture d’écran ou un statut HTTP 200 ne prouve ni la correspondance textuelle ni l’attribution d’un وجه.

### 12.2 Scénarios de non-régression

| ID | Scénario | Résultat attendu |
|---|---|---|
| T01 | Ouvrir `/mushaf/page/2` et l’alias Warsh | Une seule route sélectionnée, aucune `AmbiguousMatchException` |
| T02 | Comparer page affichée au PNG MARW importé | Même image/source ; pas de substitution par l’ancien SVG/scan |
| T03 | Importer une ancienne référence 2:1 non qualifiée | Aucun rattachement MARW automatique ; brouillon conservé et anomalie visible |
| T04 | Tester des mappings 1:N et N:1 | Spans/liens explicites ; temps originaux conservés ; approbation requise |
| T05 | Charger les cinq annotations utilisateur | GUID/temps conservés ; trou de 16,606 s intact ; aucune certification implicite |
| T06 | Cliquer une fois sur fin d’ayah | Enregistrement unique ; début suivant égal à la fin ; référence suivante correcte |
| T07 | Ajouter deux reprises après une fin | Même cible de reprise ; temps propres ; pas de double incrément du numéro |
| T08 | Terminer une portion puis une continuation | L’ayah ne change qu’à la fin complète explicitement indiquée |
| T09 | Double clic, timeout puis retry | Une seule occurrence et un seul avancement de session |
| T10 | Deux utilisateurs modifient la même version | Conflit contrôlé ; aucune perte silencieuse |
| T11 | Redémarrer et restaurer une session | Bornes et curseur repris depuis le serveur ; sauvegarde locale récupérable |
| T12 | Ajouter/modifier un وجه | Même référentiel dans CRUD, éditeur, revue et lecteur ; code stable et historique |
| T13 | Approuver la définition mais pas l’attribution | Pas de présentation de l’occurrence comme exemple certifié de ce وجه |
| T14 | Supprimer une définition référencée / un segment publié | Refus contrôlé ; archivage/retrait disponibles |
| T15 | Réexécuter bootstrap et import | Pas de résurrection d’entrées supprimées ni de doublons |
| T16 | Appeler anonymement toutes les API admin | Accès refusé, y compris GET de brouillons et mutations par ID |
| T17 | `preview=true`, URL directe d’original et chemin working | Aucun accès anonyme ; pas de contournement par fichier statique |
| T18 | Publier un Draft ou un asset périmé | Refus serveur même si l’interface est contournée |
| T19 | Rendre deux occurrences même ayah/وجه | Deux assets distincts, aucune collision ni écrasement |
| T20 | Modifier le segment pendant le rendu | Asset lié à l’ancienne révision ; aucune publication automatique de la nouvelle |
| T21 | Reprendre un job après panne | Retry idempotent ; état et fichiers cohérents |
| T22 | Jouer/télécharger une portion | Durée et contenu conformes ; pas de fuite de l’original par l’URL publique |
| T23 | Portion multilignes, zoom et changement de page | Régions persistées et correctement positionnées ; pas de faux mot-à-mot |
| T24 | Face choisie absente à l’ayah suivante | Manque affiché ; aucun remplacement silencieux |
| T25 | Recherche arabe et lien partagé | Résultat du bon corpus ; texte original intact ; sélection retrouvée |
| T26 | Source de plusieurs heures | Peaks complets, mémoire navigateur maîtrisée, compteurs lisibles et seek opérationnel |
| T27 | Retirer une publication | Absente des listes et accès média conforme à la politique de retrait/cache |
| T28 | Restaurer sauvegarde en environnement vierge | Données, médias, identités et associations récupérables |

### 12.3 Recette humaine du contenu

Préparer une fiche par occurrence : source/hash, intervalle, référence du corpus, extrait visuel, texte de la portion, type complet/partiel/reprise, attribution éventuelle, décision, nom du réviseur et date.

La personne écoute un contexte avant/après, compare la fin et le début de l’occurrence, vérifie la référence sur l’image, puis la portion et l’attribution. Elle peut approuver la délimitation tout en laissant l’attribution de وجه non identifiée. Conserver les motifs des cas à reprendre.

Jalons éditoriaux :

1. Al-Fatiha complète et les 20 premières ayat **du corpus qualifié retenu** d’Al-Baqara ; vérifier explicitement la portée de ce jalon par rapport aux anciens exports numérotés autrement.
2. Les reprises signalées au début d’Al-Baqara, dont la zone non annotée ; ne pas les certifier à partir des seuls fichiers JSON.
3. Al-Baqara complète, avec couverture et cas non résolus visibles.
4. Extension progressive aux 114 sourates ; mise en publication par lots approuvés.

Antigravity doit livrer l’outillage et préparer les files de revue. Il ne doit pas déclarer achevée une validation humaine qui n’a pas eu lieu.

## 13. Matrice de couverture des besoins initiaux

| Besoin | Référence initiale | Lots | Preuve de livraison |
|---|---|---|---|
| 114 sourates et texte Warsh | Spécifications §§ 6–8, 19, 37 | 2, 7, 8 | Import qualifié, comptes par corpus et navigation |
| Recherche par texte/nom/référence | §§ 6.1, 23 | 2, 7 | Tests de recherche et liens qualifiés |
| Écoute, téléchargement, partage | §§ 5, 7–8, 10 | 6–7 | Occurrence publiée écoutable/téléchargeable, lien stable |
| Plusieurs أوجه et extensibilité | §§ 9, 19 ; demandes utilisateur | 4, 7 | CRUD, versions, usages, filtre de disponibilité |
| Sources immuables et traçables | §§ 10–12 | 0, 3, 6, 8 | Hashes, provenance, originaux conservés, dérivés distincts |
| Annotation complète/partielle/reprises | Plan éditeur | 3, 5–6 | Une occurrence par événement, liens et révisions |
| Avancement automatique simple | Demandes utilisateur | 5 | T06–T11 ; pas de deuxième sauvegarde obligatoire |
| Effet direct des annotations | Demandes utilisateur | 3, 5–7 | Prévisualisation privée et publication depuis les mêmes données |
| Images fidèles et régions | Plan images §§ 24–45 | 2, 5, 7 | MARW, géométrie versionnée, portions, zoom et revue |
| Validation avant publication | Spécifications §§ 12–14 ; plan éditeur | 1, 6 | Transitions et policies testées ; audit |
| Dashboard et contrôles corpus | §§ 13, 38 | 8 | Comptes par état, anomalies actionnables, couverture |
| RTL, responsive et accessibilité | §§ 30–32 | 5, 7, 10 | Recette mobile/clavier/lecteur et terminologie arabe |
| SEO et URLs stables | § 33 | 7 | Canonical, métadonnées, liens qualifiés |
| Sécurité, logs, erreurs | §§ 25–27 | 1, 6, 10 | Tests négatifs, logs corrélés, erreurs maîtrisées |
| Performance et cache | §§ 28–29 | 5, 7, 10 | Mesures documentées, peaks, pagination et assets versionnés |
| Déploiement et sauvegarde | §§ 39–44 | 0, 10 | Procédure exécutable et restauration prouvée |

Objectifs de performance initiaux à mesurer dans des conditions indiquées : pages d’accueil/ayah sous deux secondes hors téléchargement audio, API simples sous 500 ms et recherche sous une seconde. Définir le réseau, le volume, l’état du cache et les percentiles mesurés ; ne pas annoncer ces chiffres à partir d’un seul chargement local.

Restent hors MVP selon les documents : comptes visiteurs, favoris, playlists personnelles, commentaires, paiements, applications mobiles natives, traduction et tafsir complets. Préserver l’extensibilité vers d’autres récitateurs/riwayat sans développer maintenant tous ces modules. Le référentiel administrable et les répétitions, eux, font partie du besoin courant.

## 14. Documentation et exploitation à livrer

À la fin de la reprise, conserver une documentation organisée :

| Fichier cible | Contenu |
|---|---|
| `README.md` | Installation, configuration, lancement, comptes/initialisation, liens utiles et tests |
| `docs/ETAT_DU_SYSTEME.md` | Ce qui fonctionne, couverture réelle, limites ouvertes et version livrée |
| `docs/ARCHITECTURE.md` | Modèle canonique, services, flux, identité des références et décisions |
| `docs/MIGRATION_REPRISE.md` | Procédure exécutée, mappings, comptes avant/après et retour arrière |
| `docs/GUIDE_ANNOTATION_AR.md` | Guide arabe illustré : fin d’ayah, reprise, portion, correction et sauvegarde |
| `docs/GUIDE_REFERENTIEL_AR.md` | CRUD, portée, sources, révisions, qualification et usages |
| `docs/RECETTE.md` | Résultats T01–T28, environnement, preuves et cas humains restant ouverts |
| `docs/EXPLOITATION.md` | Déploiement, secrets, jobs, surveillance, sauvegarde/restauration et incidents |
| `docs/BACKLOG.md` | Lots/tickets terminés ou restants, dépendances, critères et blocages précis |

Les anciens comptes rendus restent des archives datées, pas plusieurs instructions de lancement concurrentes. Les documents de besoins initiaux sont conservés. Une documentation nouvelle ne suffit jamais à clore un ticket dont le comportement n’est pas implémenté.

Le mode de lancement final doit utiliser le code et la configuration documentés, pas un binaire ad hoc perdu parmi des dossiers `artifacts`. Prévoir un script Windows adapté au poste actuel et un lancement conteneurisé pour l’environnement cible. Les processus de fond locaux doivent être identifiés et arrêtables sans arrêter tous les processus .NET de la machine.

## 15. Définition de « terminé » et conduite d’exécution

### 15.1 Système fonctionnel terminé

- Un seul référentiel en base et un seul circuit d’occurrences pour tous les écrans.
- Une édition et un corpus explicitement identifiés ; aucune association supposée par numéro.
- Un éditeur arabe simple qui persiste les captures et les exploite immédiatement en prévisualisation.
- Un workflow serveur complet, audit et rôles ; aucun brouillon ni original privé accessible anonymement.
- Un lecteur partagé qui distingue ayah complète, portion, reprise et attribution ; téléchargement et partage opérationnels.
- Toutes les sourates sont prises en charge par le logiciel sans branche spécifique ajoutée pour chaque sourate.
- Migration vérifiée, tests isolés, restauration démontrée et lancement reproductible.
- Pages expérimentales retirées du parcours normal et anciens circuits désactivés après reprise des données.

### 15.2 Corpus terminé

La couverture complète n’est atteinte que lorsque les données attendues sont présentes, correctement rattachées, revues et publiées selon le périmètre défini. Rapporter séparément :

- pages importées / pages structurellement conformes / géométries revues ;
- ayat avec texte qualifié / ayat avec correspondance texte-image qualifiée ;
- sources audio inventoriées / acquises / contrôlées ;
- occurrences annotées / revues / approuvées / publiées ;
- ayat entièrement couvertes / partiellement couvertes / sans audio ;
- attributions de وجه proposées / approuvées / non identifiées ;
- couverture documentaire par voie et source, avec les contextes restant à étudier.

**604 pages importées ne signifie pas 114 sourates audio validées. Un catalogue de 14 propositions ne signifie pas tous les أوجه.**

### 15.3 Consignes à Antigravity

Commencer par le lot 0, puis exécuter les lots en conservant un journal de progression. Pour chaque lot : constater l’état réel, implémenter dans les composants canoniques, migrer avec protection des données, tester et consigner les preuves.

Réutiliser le travail déjà présent. Ne pas recréer des écrans pilotes isolés, des JSON de production concurrents ou des associations codées en dur pour donner l’impression que le parcours fonctionne.

Avancer sur les tâches techniques sans multiplier les demandes de confirmation. Lorsqu’une décision exige une écoute ou une qualification humaine, préparer un cas concret dans la file de revue avec source, image, intervalle et question précise ; poursuivre les autres tâches indépendantes. Ne jamais convertir l’absence de réponse en approbation de contenu.

Le rapport final doit indiquer les comportements livrés, les migrations exécutées, les tests réellement passés, les URLs pour tester et les contenus encore non qualifiés. Un élément bloqué doit être nommé avec sa dépendance, pas masqué derrière une formule « site complet ».
