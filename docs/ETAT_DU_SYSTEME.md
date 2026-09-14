# État du Système — Quran Warsh

**Date : 14 septembre 2026**  
**Dossier : `D:\Projets\ITComp\Quran`**  
**Solution : `QuranWarsh.sln`**

Ce document consigne l’état réel du système au démarrage de la reprise ordonnée selon `PLAN_REPRISE_ANTIGRAVITY.md`.

---

## 1. Environnement et Outillage

- **Système d'exploitation :** Windows 11 (x64)
- **Runtime & SDK :** .NET 9.0.312
- **Moteur de base de données :** PostgreSQL 16.x (`D:\Program Files\PostgreSQL\16\bin`)
- **Serveur local :** Service Windows `postgresql-x64-16` en cours d'exécution
- **Base principale :** `quran_db` sur `localhost:5432`
- **Base de test isolée :** `quran_db_test` (créée et alimentée pour isoler les tests)
- **Base de validation de restauration :** `quran_db_test_restore` (restauration testée avec succès)
- **Outil de traitement audio :** FFmpeg / FFprobe disponibles
- **Gestionnaire de versions :** Git initialisé avec `.gitignore` complet excluant médias lourds, dumps, secrets et artefacts.

---

## 2. Structure des Projets (.NET 9)

La solution `QuranWarsh.sln` comprend six projets applicatifs et deux projets de tests :
1. `src/Quran.Domain` : Entités coraniques, segments audio, images mushaf, awjuh et enums.
2. `src/Quran.Application` : Interfaces de services, DTOs et cas d'usage.
3. `src/Quran.Infrastructure` : Entity Framework Core 9 (Npgsql), accès PostgreSQL, stockage de fichiers et catalogue Awjuh.
4. `src/Quran.AudioProcessor` : Découpage FFmpeg, sondage FFprobe et normalisation.
5. `src/Quran.Import` : Ingestion des référentiels textuels, audio Archive.org et paquets MARW.
6. `src/Quran.Web` : Application ASP.NET Core 9 (Razor Pages, API Controllers, Static Files).
7. `tests/Quran.UnitTests` : 49 tests unitaires validés (couvrant validation, audio, awjuh, géométrie, normalisation).
8. `tests/Quran.IntegrationTests` : Tests d'intégration configurés pour utiliser la base isolée `quran_db_test`.

---

## 3. Inventaire de la Base de Données `quran_db`

Relevé effectué le 14/09/2026 à 13:00 :

| Table | Nombre de lignes | Description / Rôle actuel |
|---|---:|---|
| `Surahs` | 114 | Référentiel complet des 114 sourates |
| `Ayahs` | 29 | Ayat amorcées (8 pour Al-Fatiha [0-7], 21 pour Al-Baqarah [0-20]) |
| `AyahWords` | 234 | Découpage mot-à-mot des versets amorcés |
| `MushafEditions` | 1 | Édition active : `MADINAH_WARSH` (Complexe Roi Fahd) |
| `MushafPages` | 5 | Pages 1 à 5 importées avec clés master et web |
| `AyahImageRegions` | 18 | Régions graphiques normalisées pour Al-Fatiha et Al-Baqarah (Page 2) |
| `AudioSegmentImageRegions` | 0 | Table prête pour les portions d'ayat |
| `AudioSegments` | 3 | Segments temporels créés dans l'ancien éditeur |
| `MushafPassages` | 5 | Passages issus des imports brouillons de l'utilisateur (2:1 à 2:5) |
| `Awjuh` | 14 | 14 propositions actives (4 Azraq/Shatibiyya + 10 Asbahani/Yasin) |
| `Reciters` | 1 | Cheikh Saber Abdel-Hakam (`Id = 1`) |
| `Riwayas` | 1 | Warsh an Nafi (`Id = 1`) |
| `Recitations` | 33 | Enregistrements rattachés aux ayat |
| `AudioAssets` | 34 | Assets MP3 découpés et référencés |
| `SourceAudios` | 1 | Sourate 2 (Al-Baqarah, 247 Mo, durée ~4h 18min) |
| `Sources` | 2 | Métadonnées de provenance Archive.org |
| `ValidationReviews` | 0 | Historique de revue (à alimenter par le workflow) |
| `AspNetRoles` | 2 | Rôles système (`Admin`, `Reviewer`) |
| `AspNetUsers` | 1 | Utilisateur administrateur local initialisé |
| `AspNetUserRoles` | 1 | Attribution rôle Admin |
| `AspNetUserClaims`, `AspNetRoleClaims`, `AspNetUserLogins`, `AspNetUserTokens` | 0 | Tables Identity prêtes |
| `__EFMigrationsHistory` | 5 | Historique des migrations EF Core appliquées |

### Migrations EF Core appliquées
1. `20260912203115_InitialCreate`
2. `20260912214609_AddAudioEditorModels`
3. `20260912220235_AddMushafImagesSystem`
4. `20260914102312_IntegratedAwjuhAndMushafPassages`
5. `20260914120743_AddIdentitySupport`

---

## 4. Sauvegardes et Archives Préservées

1. **Sauvegarde PostgreSQL complète :**
   - Fichier : `backups/20260914_pre_reprise/quran_db_snapshot.dump`
   - Taille : 68 375 octets
   - Format : Custom PostgreSQL (`-Fc`)
   - Restauration vérifiée : Testée sur `quran_db_test_restore` avec 100% de concordance (114 sourates, 5 passages).

2. **Archives des exports utilisateurs originaux :**
   - Répertoire sécurisé : `backups/20260914_pre_reprise/user_exports/`
   - `baqarah-audio-passages-draft.json`
     - SHA-256 : `2C0C89678697A64739FE2E7EE58C9B4DEFB7EB0620E6BC2EEC7BED0F484E22E4`
   - `baqarah-audio-passages-draft(1).json`
     - SHA-256 : `8E5C9553C37FF25C34D79E81E6CD957777CB9E65B955EF2CF4B460FD7CE5E7E1`
   - Constat sur l'export (1) : 5 segments allant de 13 974 ms à 102 310 ms avec un trou audible non annoté de **16 606 ms** entre 67 986 ms et 84 592 ms.

3. **Paquet graphique MARW :**
   - Chemin : `data/mushaf/editions/marw-newMushaf`
   - Contenu : 604 fichiers PNG + 604 fichiers JSON + manifestes
   - Taille totale : ~144 Mo

---

## 5. Sécurité et Protection des Médias (Lot 1)

- **Authentification & Rôles :** ASP.NET Core Identity avec tables `AspNet*`, rôles `Admin` et `Reviewer`, cookies HttpOnly / SameSite Lax, redirection vers `/Account/Login`.
- **Protection des accès administratifs :**
  - Pages Razor `/Admin/*` protégées par convention `RequireReviewer`.
  - Contrôleurs d'administration (`AdminRecitationsController`, `AudioEditorController`, `ImportController`, `InventoryController`, `CorpusCheckController`, mutations `AwjuhController`, mutations `MushafController`) protégés par `[Authorize]`.
  - Layouts séparés : `_AdminLayout.cshtml` avec navigation dédiée arabe/RTL, temps/codes en LTR ; `_Layout.cshtml` public épuré.
- **Protection des médias privés :**
  - Exposition statique générale `/audio` supprimée.
  - Seul `/audio/published` est servi statiquement avec en-têtes de cache immuables.
  - `/audio/sources/*` et `/audio/working/*` sont strictement privés, accessibles via `AudioStreamController` réservé aux rôles `Reviewer` et `Admin`, avec support natif du streaming HTTP 206 Range requests (scrubbing audio / WaveSurfer).
  - Paramètre public `preview=true` supprimé ; prévisualisation réservée via `/api/annotations/preview` aux utilisateurs autorisés.

---

## 6. Corpus, Références et Décomptes (Lot 2)

- **Entités canoniques introduites :**
  - `QuranCorpus` : identification rigoureuse du corpus (`warsh-pilot-historical`, `marw-newMushaf`), riwaya, système de décompte, révision, provenance et statut de qualification.
  - `CorpusAyah` : ayah qualifiée rattachée à un corpus précis avec ordinal, texte original, texte de recherche et numéro de page.
  - `ReferenceMapping` : table de correspondance entre corpus source et cible, avec cardinalité (`OneToOne`, `ManyToOne`, `OneToMany`, `Ambiguous`), description des écarts, statut de revue et notes d'épreuve/isnad.
  - `MushafEdition` : rattaché explicitement à un `QuranCorpus` avec code de révision.
- **Migration EF Core :** `20260914121809_AddCorpusAndReferenceMapping` appliquée sur `quran_db` et `quran_db_test`.
- **Cas Al-Baqarah 2:1 à 2:5 documenté et prouvé :**
  - Dans le décompte MARW (Madani), `الٓمٓ` ne forme pas un verset indépendant. Le verset 2:1 MARW englobe `الٓمٓ ذَٰلِكَ ٱلۡكِتَٰبُ لَا رَيۡبَۛ فِيهِۛ هُدٗى لِّلۡمُتَّقِينَ` (fusion ManyToOne de 2:1 et 2:2 historiques).
  - Par conséquent, 2:3 historique devient 2:2 MARW, 2:4 historique devient 2:3 MARW, et 2:5 historique devient 2:4 MARW (se terminant par `ٱلۡمُفۡلِحُونَ`).
  - Interdiction formelle dans `ICorpusResolutionService` de tout alignement 1:1 aveugle : toute référence sans mapping documenté retourne `IsMapped = false` avec statut `PendingReview`.
- **Interface d'administration :** `/Admin/CorpusMapping` pour visualiser, éditer et documenter les correspondances et écarts par sourate.

---

## 7. Réunification des Occurrences et Sessions (Lot 3)

- **Agrégat canonique `AudioSegment` étendu :**
  - Référence qualifiée (`Reference`), édition (`Edition`), hash du fichier source (`SourceHash`), jeton de version de concurrence (`Version`).
  - Lien de reprise typé (`RepeatOfId` / `RepeatOf`), clé étrangère `AyahId` rendue optionnelle pour supporter les segments à référence en cours de qualification.
  - Snapshot immuable d'historique sémantique : entité `SegmentRevision` (numéro de révision, temps, référence, wajh, auteur, date).
  - Couverture textuelle multi-unités : entité `SegmentTextSpan`.
  - Session d'annotation persistante : entité `AnnotationSession` (curseur temporel milliseconde, sourate/ayah courante, dernier segment, mode reprise).
  - Journal d'audit persistant : entité `AuditLog` (acteur, action, entité, payload avant/après, corrélation).
- **Migration EF Core :** `20260914122750_AddAudioSegmentCanonicalAndSessions` appliquée sur `quran_db` et `quran_db_test`.
- **Migration des données exécutée avec succès (`--migrate-passages`) :**
  - 5 passages de `MushafPassages` migrés vers `AudioSegments` en préservant strictement les GUIDs, timestamps et notes :
    - `64f93c4a-e13d-4966-8194-a2ce04d04372` : 2:1 (13 974 ms à 22 883 ms)
    - `1166759a-0396-4eac-b9b6-8cb08c91edb8` : 2:2 (22 883 ms à 34 217 ms)
    - `b159736e-fdbe-4441-82f0-9098a493023c` : 2:3 (34 217 ms à 48 808 ms)
    - `262c7734-595b-4ac6-a11f-12b9ccd15d3d` : 2:4 (48 808 ms à 67 986 ms)
    - `0415c909-b0f3-4e3d-9f2a-ecfebc91b7ef` : 2:5 (84 592 ms à 102 310 ms)
  - Trou réel de **16 606 ms** (entre 67 986 ms et 84 592 ms) strictement préservé sans interpolation artificielle.
  - Chaque occurrence migrées a reçu sa `SegmentRevision` initiale et son entrée dans `AuditLogs`.
- **Convergence applicative :** `IAnnotationService` / `AnnotationService` implémenté dans `Quran.Application` et `Quran.Infrastructure`, synchronisant `AnnotationsController` avec l'agrégat canonique.

## 8. Référentiel des Awjuh Structuré et Couverture Documentaire (Lot 4)

- **Modèle de données relationnel structuré (10 entités) :**
  - `TransmissionRoute` : tariq, sous-tariq, autorité, statut de couverture.
  - `ReadingRule` & `ReadingRealization` : règles de récitation typées (catégorie, réalisation numérique/textuelle).
  - `WajhComponent` : décomposition atomique d'un wajh en réalisations concrètes.
  - `WajhApplicationScope` : périmètre d'application (sourate/ayah début-fin, statut de qualification).
  - `WajhRevision` : historique immuable des versions d'un wajh.
  - `CombinationConstraint` : contraintes d'association (condition, effet, explication).
  - `ReferenceSource` & `EvidenceCitation` : sources documentaires et citations avec locateur précis.
  - `DocumentaryCoverageEntry` : matrice de couverture par voie, ouvrage source, sourate et statut d'analyse.
  - `Wajh` étendu : FKs structurées, version de concurrence `Version`, audit d'approbation (`ReviewerUserId`, `ReviewedAt`, `RejectionReason`).
- **Migration EF Core :** `20260914124335_AddStructuredAwjuhReferentialAndCoverage` appliquée sur `quran_db` et `quran_db_test`.
- **Garantie d'idempotence et non-résurrection des codes obsolètes :**
  - Marqueur d'audit `CATALOG_SEEDED_V1` empêchant toute réinjection en boucle.
  - Interdiction stricte de résurrection des anciens codes supprimés (`istiadha`, `basmala`, `w01`, `w02`, `w03`, `taqlil`, `silat_mim`).
- **Logique métier (`IAwjuhService` / `AwjuhService`) :**
  - Cycle de vie : `Draft` -> `UnderReview` -> `Approved` / `Rejected` -> `Archived`.
  - Modification d'un wajh approuvé : génération automatique d'une nouvelle `WajhRevision` et invalidation des segments audio rattachés en `Draft` pour revue obligatoire.
  - Règle de suppression sécurisée : rejet strict si le wajh est `Approved`, `Archived` ou référencé dans des segments audio ou passages.
  - Rapport d'impact (`GetUsageReportAsync`) calculant l'arborescence des dépendances audio et mushaf.
  - Règle de requête lecteur (`GetForReaderAsync`) : seuls les oujouth approuvés ayant au moins un segment audio publié sont retournés.
- **Interface d'administration :** `/Admin/Awjuh` en arabe avec 4 onglets (Catalogue, Matrice de couverture documentaire, Contraintes de combinaison, Sources de référence).

---

## 9. Éditeur Unique et Simple d'Édition Audio (Lot 5)

- **Unification complète de l'expérience d'édition :**
  - Fusion des fonctionnalités avancées (WaveSurfer 7, zoom, timeline, minimap, ajustement fin, découpage de portions) et du mode de capture simple en une seule interface arabe / RTL `/Admin/AudioEditor` (avec sélection de sourate 1 à 114).
  - Redirection automatique de la page d'expérimentation historique `/Mushaf/AudioReview` vers `/Admin/AudioEditor/2`.
- **Actions atomiques implémentées côté client et serveur :**
  - **« إنهاء الآية والانتقال إلى التالية » (`F8` / `Enter`)** : capture instantanée `[currentStartMs, currentTimeMs]`, enregistrement atomique en base avec session persistante, définition du début suivant exactement au temps capturé, avance de l'ayah via le corpus, et rafraîchissement immédiat de la prévisualisation MARW.
  - **« إنهاء التكرار » (`F9`)** : enregistrement d'un segment de reprise lié (`RepeatOfId`), temps propres, maintien du numéro de verset en attente d'enregistrement.
  - **« تراجع عن آخر إجراء » (`Ctrl+Z`)** : annulation sécurisée de la dernière occurrence (vérification d'absence de reprises dépendantes), suppression du brouillon, restauration du curseur de session au temps initial et restauration du numéro d'ayah.
  - **Gestion des pauses de souffle** : boutons dédiés « مقطع من الآية » (`PartialAyah`) et « استكمال الآية » (`Continuation`) pour éviter d'incrémenter artificiellement le verset lors d'un arrêt respiratoire.
- **Service d'ondes sonores (Peaks) multi-sourates et longue durée :**
  - Interface `IAudioPeaksService` et service `AudioPeaksService` générant via FFmpeg des signaux de crêtes audio échantillonnés et normalisés à 100 Hz, avec mise en cache disque (`data/cache/peaks/{sourceHash}.json`).
  - Évite le décodage lourd en mémoire de fichiers de plusieurs heures (ex. Al-Baqarah de 4h) dans le navigateur.
  - Point d'accès `/api/admin/audio-editor/peaks/{surahNumber}` avec fenêtrage et sous-échantillonnage dynamique.
- **Synchronisation géographique MARW en temps réel :**
  - Point d'accès `/api/admin/audio-editor/marw-ayah/{surahNumber}/{ayahNumber}` délivrant la page MARW et le polygone SVG exact de l'ayah.
  - Affichage direct de la page de مصحف ورش et surlignage SVG superposé à l'ayah active dans l'éditeur.

---

---

## 10. Circuit de Revue, Rendu FFmpeg par Révision et Publication/Dépublication (Lot 6)

- **Machine à états stricte côté serveur :**
  - Cycle de vie : `Draft` → `Reviewed` → `Approved` → `Rendering` → `Rendered` → `Published`.
  - Branche de rejet motivé : `Draft / Reviewed / Approved` → `Rejected` (motif non vide obligatoire `RejectionReason`, traçabilité de l'acteur `ReviewedByUserId` et date `ReviewedAt`).
  - Dépublication explicite : `Published` → `Rendered` (retrait immédiat de la visibilité publique, désactivation de la récitation liée `IsPublished = false`, conservation des fichiers générés et de l'audit trail).
- **Champs d'audit ajoutés sur `AudioSegment` :**
  - `RejectionReason` (texte motivant le rejet).
  - `ReviewedByUserId`, `ReviewedAt` (traçabilité du relecteur/réviseur).
  - `ApprovedByUserId`, `ApprovedAt` (traçabilité de l'approbateur).
  - `Version` (incrémenté à chaque modification de bornes, texte ou wajh, forçant le retour en `Draft` pour nouveau rendu et approbation).
  - Migration EF Core : `20260914130526_AddAudioSegmentReviewWorkflowFields` appliquée sur `quran_db` et `quran_db_test`.
- **Découpage FFmpeg persistant et versionné :**
  - Clé de travail : `working/segments/{surahNum:D3}/{ayahNum:D3}_{segmentId:N}_rev{rev}{wajhSuffix}.mp3`.
  - Clé de publication : `published/{surahNum:D3}/{ayahNum:D3}/{segmentId:N}_rev{rev}{wajhSuffix}.mp3`.
  - Aucune collision ni écrasement entre prises successives d'un même verset ou répétitions.
  - Vérification de l'empreinte SHA-256 (`ComputeChecksum`) avant toute copie en publication : garantit que le fichier sur disque correspond rigoureusement à l'asset validé.
- **File de revue et d'approbation (`/Admin/ReviewQueue`) :**
  - Interface dédiée en arabe avec filtres par sourate et par statut (`Draft`, `Reviewed`, `Approved`, `Rejected`, `Rendered`, `Published`).
  - Affichage des KPIs d'avancement par statut.
  - Modale interactive pour le rejet motivé avec contrôle de champ obligatoire.
  - Actions en un clic : soumission pour revue, approbation, rejet motivé, تصيير FFmpeg, نشر et سحب النشر.
- **Points d'accès API sécurisés (`AudioEditorController`) :**
  - `POST /api/admin/audio-editor/segments/{id}/submit-review` (`Reviewer`, `Admin`)
  - `POST /api/admin/audio-editor/segments/{id}/approve` (`Reviewer`, `Admin`)
  - `POST /api/admin/audio-editor/segments/{id}/reject` (`Reviewer`, `Admin` avec `RejectSegmentRequest`)
  - `POST /api/admin/audio-editor/segments/{id}/render` (`Admin`)
  - `POST /api/admin/audio-editor/segments/{id}/publish` (`Admin`)
  - `POST /api/admin/audio-editor/segments/{id}/unpublish` (`Admin`)
  - `GET /api/admin/audio-editor/review-queue` (`Reviewer`, `Admin`)

---

---

## 11. Site Public Cohérent, Lecteur MARW Unifié et Téléchargements Propres (Lot 7)

- **Harmonisation des routes canoniques :**
  - Accueil : `/` (avec recherche plein texte/référence, liens canoniques vers `/surah/{number}` et `/mushaf/page/1`).
  - Sourate : `/surah/{number}` (navigation séquentielle, permalinks vers `/quran/{surah}/{ayah}`, téléchargement direct avec nom de fichier propre).
  - Ayah canonique : `/quran/{surah}/{ayah}` (fil d'ariane, découpage vectoriel fidèle du Mushaf Warsh MARW officiel, sélecteur interactif d'أوجه avec synchronisation de l'URL `?wajh=...`, partage de permalien canonique, téléchargement MP3 identifié `سورة_{nom}_{ayah}_{wajh}.mp3`, navigation au clavier flèches gauche/droite et espace).
  - Page Mushaf MARW : `/mushaf/page/{pageNumber}` (planche officielle 604 pages, surlignage vectoriel SVG des ayat, lecteur partagé avec sélecteur de وجه, gestion propre des segments découpés publiés vs prévisualisations autorisées, raccourcis clavier).
- **Protection stricte des médias privés :**
  - Fichiers sources bruts `/audio/sources/*` masqués aux visiteurs anonymes et réservés aux relecteurs/administrateurs authentifiés.
  - Prévisualisation `/api/annotations/preview` réservée aux rôles `Reviewer`/`Admin`.
  - Point d'accès public unifié `/api/annotations/playback` servant les segments canoniques `AudioSegment` publiés avec métadonnées de durée et wajh.
- **Rendu typographique et internationalisation :**
  - Enregistrement du service `HtmlEncoder.Create(UnicodeRanges.All)` garantissant l'émission directe du texte arabe sans échappement HTML numérique `&#x...;`.
  - Support complet RTL, styles responsives et navigation clavier (flèches pour défilement des versets, barre d'espace pour bascule lecture/pause).
- **Tests d'intégration de bout en bout (`PublicSiteIntegrationTests`) :**
  - `HomePage_LoadsSuccessfully_WithCanonicalLinks` : validation de l'index et des liens canoniques.
  - `SurahPage_CanonicalRoute_LoadsSuccessfully` : affichage des sourates et permaliens d'ayat.
  - `AyahPage_CanonicalRoute_LoadsSuccessfully_WithPermaAndWajhSelector` : découpage MARW, titre arabe, lecteur et partage.
  - `AyahPage_WithWajhQueryParameter_BindsWajhCorrectly` : liaison du paramètre d'URL wajh.
  - `MushafPage_CanonicalRoute_LoadsSuccessfully_WithRegionsAndViewer` : planche MARW, SVG et lecteur.
  - `PlaybackApi_ForValidReference_ReturnsOk` : endpoint public de lecture.
  - `PreviewApi_Anonymous_IsRejected` : rejet sécurisé des prévisualisations anonymes.

---

## 12. État des Tests Automatisés

- Commande : `dotnet test`
- Résultat actuel : **104 tests exécutés, 104 réussis (0 échec, 1 ignoré documenté)**
  - `Quran.UnitTests` : 74/74 réussis.
  - `Quran.IntegrationTests` : 30/30 réussis sur `quran_db_test` (dont `PublicSiteIntegrationTests`, `ReviewWorkflowIntegrationTests`, `AudioEditorEndToEndIntegrationTests`, `AwjuhEndToEndIntegrationTests`, `SecurityAccessIntegrationTests`).


