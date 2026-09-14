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

## 9. État des Tests Automatisés

- Commande : `dotnet test`
- Résultat actuel : **83 tests exécutés, 83 réussis (0 échec, 1 ignoré documenté)**
  - `Quran.UnitTests` : 68/68 réussis (dont `AwjuhServiceTests` validant cycle de vie, invalidation d'occurrences, suppression sécurisée et politiques de visibilité).
  - `Quran.IntegrationTests` : 15/15 réussis sur `quran_db_test` (dont `AwjuhEndToEndIntegrationTests`).

