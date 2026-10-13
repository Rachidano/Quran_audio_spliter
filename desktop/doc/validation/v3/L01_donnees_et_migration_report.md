# Rapport de Recette L01 — Données et Migration V3

- **Date** : 13 octobre 2026
- **Auteur** : Antigravity
- **Référence** : [tasks_v3.md](../../tasks_v3.md), [plan d’implémentation_v3.md](../../plan%20d%27impl%C3%A9mentation_v3.md)

---

## 1. Objectifs du Lot L01

1. **V3-01-01** : Définir le schéma minimal en réutilisant les entités existantes (projets, prises par ayah, présentation et ressources).
2. **V3-01-02** : Ajouter collections, liens vers créations et ordre des éléments sans dupliquer les annotations.
3. **V3-01-03** : Implémenter version de schéma, sauvegarde SQLite cohérente et migration transactionnelle non destructive.
4. **V3-01-04** : Importer fonds/logos dans l’espace géré ; conserver chemins relatifs et identité des ressources.
5. **V3-01-05** : Étendre sauvegarde/restauration aux données et ressources V3.
6. **V3-01-06** : Tester base vierge, mise à niveau V2, réexécution et échec de migration ; comparer les données préservées.

---

## 2. Réalisations concrètes

### Modèle de données V3 (`Data/Entities.cs` & `Data/AppDbContext.cs`)
- `BackgroundType` (`Light`, `Dark`, `CustomImage`) : gestion des 3 fonds prévus.
- `WatermarkType` (`None`, `Text`, `Logo`), `WatermarkPosition` (4 coins), opacité.
- `UserAssetEntity` (table `user_assets`) : ressources personnelles gérées avec SHA-256 et chemins relatifs.
- `VideoProjectEntity` (table `video_projects`) : créations sauvegardées multi-ayat ou mono-ayah avec révision, orientation, format, fond et signature.
- `ProjectAyahEntity` (table `project_ayahs`) : séquence ordonnée d'ayat rattachées à la création avec références vers les prises audio, révisions, bornes, recettes d'images et confirmation de jonction.
- `CollectionEntity` (table `collections`) : regroupement thématique nommé.
- `CollectionItemEntity` (table `collection_items`) : liaison ordonnée des créations dans une collection.

### Service de Migration Non Destructif (`Services/DatabaseMigrationService.cs`)
- Contrôle de version via `PRAGMA user_version = 3`.
- Création automatique d'une sauvegarde de sécurité physique `.pre_v3.bak` via l'API SQLite Online Backup avant toute altération de base existante.
- DDL transactionnel (`CREATE TABLE IF NOT EXISTS`, index d'unicité et de performance).
- Idempotence absolue (re-passage sans erreur, conservation des données).

### Service de Gestion des Ressources Personnelles (`Services/AssetManagementService.cs`)
- Import atomique avec calcul de SHA-256.
- Stockage organisé dans `media/assets/backgrounds/` et `media/assets/logos/`.
- Traçabilité et déduplication en base de données SQLite.

### Extension de la Sauvegarde / Restauration (`Services/BackupService.cs`)
- Intégration de `data/audio_settings.json` dans le snapshot.
- Inclusion automatique du répertoire `media/assets/` et de l'ensemble des données V3.

---

## 3. Résultats des vérifications automatisées

- **Tests unitaires de migration** : 4 nouveaux tests dédiés (`MigrationTests.cs`) :
  - `MigrateDatabase_FreshDatabase_CreatesSchemaV3AndAllTables` : **PASS**.
  - `MigrateDatabase_FromV2Baseline_PreservesAllExistingDataAndCreatesPreV3Backup` : **PASS** (100% des 604 pages, 114 sourates, 6 214 versets, 6 214 textes, 228 liens audios, 47 recettes d'images et 2 passages préservés ; tables V3 opérationnelles en écriture).
  - `MigrateDatabase_IsIdempotent` : **PASS**.
  - `AssetManagementService_ImportAndRetrieveAssets_WorksAccurately` : **PASS**.
- **Suite complète** : **101/101 tests réussis** (0 échec, 0 ignoré).
- **Preuve TRX** : [L01_migration_tests.trx](L01_migration_tests.trx).

---

## 4. Conclusion

Le Lot L01 est entièrement validé et clôturé. Le schéma et les services de persistance V3 sont prêts pour le **Lot L02 (Séquences et validation)**.
