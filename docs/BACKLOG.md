# Backlog d’Implémentation et Reprise — Quran Warsh

**Date : 14 septembre 2026**  
**Référence : `PLAN_REPRISE_ANTIGRAVITY.md`**  
**Solution : `D:\Projets\ITComp\Quran\QuranWarsh.sln` (.NET 9)**

Ce backlog suit l’état d’avancement réel des lots définis dans le dossier de reprise. Chaque lot doit être vérifié avant d’être marqué terminé.

---

## Vue d’ensemble des Lots

| Lot | Titre | Dépendance | Statut | Date de réalisation |
|---|---|---|---|---|
| **Lot 0** | Sécuriser la reprise et établir la référence | Aucune | ✅ **Terminé** | 14/09/2026 |
| **Lot 1** | Fermer les accès administratifs et les médias privés | Lot 0 | ✅ **Terminé** | 14/09/2026 |
| **Lot 2** | Fixer le corpus et les références | Lots 0, 1 | ✅ **Terminé** | 14/09/2026 |
| **Lot 3** | Réunifier les occurrences et migrer les brouillons | Lot 2 | ✅ **Terminé** | 14/09/2026 |
| **Lot 4** | Achever le référentiel administrable | Lots 2, 3 | ✅ **Terminé** | 14/09/2026 |
| **Lot 5** | Livrer l’éditeur unique et simple | Lots 3, 4 | ✅ **Terminé** | 14/09/2026 |
| **Lot 6** | Achever revue, rendu et publication | Lots 3, 4, 5 | ✅ **Terminé** | 14/09/2026 |
| **Lot 7** | Terminer le site public cohérent | Lots 2, 4, 6 | ✅ **Terminé** | 14/09/2026 |
| **Lot 8** | Industrialiser l’ingestion et la qualité | Lots 3, 6, 7 | ✅ **Terminé** | 14/09/2026 |
| **Lot 9** | Nettoyer le projet et rendre l’exécution reproductible | Remplacements recettés | ✅ **Terminé** | 14/09/2026 |
| **Lot 10** | Recette, exploitation et livraison | Lots 0 à 9 | ✅ **Terminé** | 14/09/2026 |

---

## Détail des Tâches par Lot

### Lot 0 — Sécuriser la reprise et établir la référence
- [x] **0.1** Relever instructions locales, SDK (.NET 9.0.312), PostgreSQL 16 local, routes et configuration.
- [x] **0.2** Initialiser le dépôt Git avec `.gitignore` complet (exclusion stricte des MP3, dumps, secrets, sorties de build).
- [x] **0.3** Sauvegarder la base de données PostgreSQL `quran_db` (`backups/20260914_pre_reprise/quran_db_snapshot.dump`).
- [x] **0.4** Tester la restauration intégrale dans une base isolée `quran_db_test_restore` (114 sourates, 5 passages restaurés sans erreur).
- [x] **0.5** Archiver et calculer les empreintes SHA-256 des exports utilisateurs (`baqarah-audio-passages-draft.json` et `baqarah-audio-passages-draft(1).json`).
- [x] **0.6** Créer la base de test dédiée `quran_db_test` et isoler les tests de la base active de travail.
- [x] **0.7** Établir la ligne de base des tests automatisés (50 tests passants).
- [x] **0.8** Rédiger `docs/ETAT_DU_SYSTEME.md` et `docs/BACKLOG.md`.

### Lot 1 — Fermer les accès administratifs et les médias privés
- [x] **1.1** Installer ASP.NET Core Identity (EF Core Stores, migration `20260914120743_AddIdentitySupport`, rôles `Admin` et `Reviewer`).
- [x] **1.2** Configurer l’authentification par cookie sécurisé et ordonner `UseAuthentication()` avant `UseAuthorization()`.
- [x] **1.3** Séparer la navigation publique et le back-office (`_AdminLayout.cshtml` et `_ViewStart.cshtml` dans `/Admin`, navbar publique épurée).
- [x] **1.4** Protéger toutes les routes d’administration (`/Admin/*`, `/api/admin/*`, endpoints de mutation dans `AnnotationsController`, `AwjuhController`, `MushafController`).
- [x] **1.5** Supprimer l’accès public direct aux répertoires `sources/` et `working/` ; seuls les fichiers sous `/audio/published` restent servis statiquement ; streaming sécurisé implémenté dans `AudioStreamController` avec support HTTP 206 Partial Content (Range).
- [x] **1.6** Supprimer le paramètre `preview=true` public et réserver la prévisualisation aux utilisateurs autorisés via `/api/annotations/preview` (`Reviewer`/`Admin`).
- [x] **1.7** Tests automatisés d’autorisation négatifs et positifs (`SecurityAccessIntegrationTests.cs`, 12 tests nouveaux, 62 tests totaux au vert).
- [x] **1.8** Commande CLI d'initialisation de l'administrateur sans credentials codés en dur (`--create-admin <email> <password>`).

### Lot 2 — Fixer le corpus et les références
- [x] **2.1** Introduire le modèle canonique corpus/édition (`QuranCorpus`, `CorpusAyah`, `MushafEdition`, `ReferenceMapping`).
- [x] **2.2** Intégrer formellement le paquet MARW `newMushaf` (révision `20260913-full`, 604 pages, 6 214 ayat).
- [x] **2.3** Rapprocher les textes et décomptes ; établir le rapport d’écart (notamment Al-Baqarah 2:1 à 2:5 : 2:1 et 2:2 fusionnés dans MARW 2:1, décalages 2:3 à 2:5).
- [x] **2.4** Modéliser et outiller la table de correspondances (`ReferenceMapping`) avec résolutions explicites et interdiction d'alignement 1:1 aveugle (`ICorpusResolutionService`).
- [x] **2.5** Conserver la géométrie MARW et les revues d'ouvertures de sourates sans écrasement ; interface d'administration `/Admin/CorpusMapping`.

### Lot 3 — Réunifier les occurrences et migrer les brouillons
- [x] **3.1** Étendre `AudioSegment` comme agrégat canonique (`SegmentRevision`, `SegmentTextSpan`, `AnnotationSession`, `AuditLog`, `RepeatOfId`, `Reference`, `Edition`, `SourceHash`, `Version` de concurrence).
- [x] **3.2** Migrer les 5 `MushafPassages` existants vers `AudioSegment` en préservant leurs GUID, timestamps et notes (migration additive sans perte).
- [x] **3.3** Traiter les deux fichiers d'export utilisateur (zone 67 986 ms à 84 592 ms marquée et conservée comme trou audible explicite de 16,606 s à qualifier humainement).
- [x] **3.4** Extraire la logique métier de `AnnotationsController` et de l'éditeur dans la couche `Quran.Application` (`IAnnotationService` / `AnnotationService`).
- [x] **3.5** Persister la session d'annotation (`AnnotationSession` : curseur temporel, dernière occurrence, reprise liée sans incrément d'ayah).

### Lot 4 — Achever le référentiel administrable
- [x] **4.1** Déployer les tables relationnelles structurées du référentiel (`TransmissionRoute`, `ReadingRule`, `ReadingRealization`, `WajhComponent`, `WajhApplicationScope`, `WajhRevision`, `CombinationConstraint`, `ReferenceSource`, `EvidenceCitation`, `DocumentaryCoverageEntry`).
- [x] **4.2** Migrer les propositions existantes avec garde d'idempotence (`CATALOG_SEEDED_V1`) sans recréer les anciens codes supprimés (`istiadha`, `basmala`, `w01`, `w02`, `w03`, `taqlil`, `silat_mim`).
- [x] **4.3** Développer le service applicatif et CRUD back-office complet des oujouth (`IAwjuhService` / `AwjuhService`) avec machine à états (`Draft`, `UnderReview`, `Approved`, `Rejected`, `Archived`), révisions versionnées, duplication et suppression sécurisée (rejet si approuvé/archivé ou référencé).
- [x] **4.4** Fournir la recherche multicritères, le rapport d'impact (`GetUsageReportAsync`) et la matrice de couverture documentaire (`DocumentaryCoverageEntry`).
- [x] **4.5** Remplacer le bootstrap destructif par un seed versionné immuable avec audit trail, et créer l'interface d'administration `/Admin/Awjuh` en arabe.

### Lot 5 — Livrer l’éditeur unique et simple
- [x] **5.1** Créer l'interface unifiée d'édition audio en arabe (mode simple par défaut + panneau avancé WaveSurfer).
- [x] **5.2** Implémenter l'action « إنهاء الآية والانتقال إلى التالية » (capture atomique, fin = début suivant, avance de l'ayah via corpus).
- [x] **5.3** Implémenter l'action « إنهاء التكرار » (reprise liée, temps propres, maintien du numéro d'ayah).
- [x] **5.4** Gestion des portions d'ayat (« مقطع من الآية » et « استكمال الآية ») pour les arrêts de souffle et annulation (« تراجع عن آخر إجراء »).
- [x] **5.5** Raccordement direct à l'image MARW et surlignage SVG temps réel, service de peaks d'ondes audio précalculés multi-sourates.

### Lot 6 — Achever revue, rendu et publication
- [x] **6.1** Imposer la machine à états serveur : `Draft → Reviewed → Approved → Rendering → Rendered → Published`.
- [x] **6.2** File d'attente de revue administrative avec motifs de rejet motivés et traçabilité de l'acteur (`/Admin/ReviewQueue`).
- [x] **6.3** Découpage FFmpeg idempotent, asynchrone et persistant lié à la révision exacte (`_rev{version}`).
- [x] **6.4** Publication et dépublication explicites avec vérification des assets générés (contrôle de somme de contrôle SHA-256).

### Lot 7 — Terminer le site public cohérent
- [x] **7.1** Aligner les pages publiques (`/`, `/surah/{number}`, `/quran/{surah}/{ayah}`, `/mushaf/page/{pageNumber}`).
- [x] **7.2** Lecteur partagé sur les images MARW avec surlignage fidèle et filtrage par وجه disponible.
- [x] **7.3** Téléchargement propre du passage découpé et partage par URL canonique qualifiée.
- [x] **7.4** Prise en charge RTL, responsive mobile, navigation clavier et gestion des cas sans audio publié.

### Lot 8 — Industrialiser l’ingestion et la qualité
- [x] **8.1** Inventaire et ingestion automatisée des 114 sourates Archive.org.
- [x] **8.2** Tableaux de bord de couverture et rapports de cohérence (trous, chevauchements, dépendances).

### Lot 9 — Nettoyer le projet et rendre l’exécution reproductible
- [x] **9.1** Nettoyage des contrôleurs, vues d'essai et scripts obsolètes (harmonisation des layouts, sécurisation lecture seule catalogue Awjuh, redirection douce des démonstrateurs).
- [x] **9.2** Autonomie complète des tests Playwright sans dépendance externe (`package.json`, scripts npm standardisés et exécutables en isolation).
- [x] **9.3** Documentation README épurée et reproductibilité multi-environnements (`README.md` à la racine, commandes pas-à-pas, tests unitaires et intégration).

### Lot 10 — Recette, exploitation et livraison
- [x] **10.1** Configuration Docker Compose complète (Dockerfile multi-stage .NET 9 + FFmpeg, service web, PostgreSQL 17, MinIO, volumes persistants `quran_data` et `quran_appdata`).
- [x] **10.2** Exécution et consignation de l'ensemble des 28 scénarios de recette (T01 à T28 consignés et validés dans `docs/RECETTE.md`, 110 tests automatisés au vert).
- [x] **10.3** Dossier de livraison distinguant logiciel fonctionnel et contenu restant à qualifier (`docs/RAPPORT_DE_LIVRAISON.md`).
