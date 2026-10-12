# Procès-Verbal de Recette — Lot 10
## Sauvegarde, Installateur Inno Setup et Autonomie Complète

- **Date** : 11 octobre 2026
- **Version** : 1.0.0
- **Statut** : Validé avec succès

---

### 1. Objectifs du Lot 10

1. **Sauvegarde et Restauration complètes** :
   - Sauvegarde de la base de données SQLite via l'API `VACUUM INTO` / SQLite Online Backup API sans verrouillage bloquant.
   - Archivage ZIP atomique comprenant la base de données SQLite, le dossier des audios personnalisés (`custom_audio/`), les métadonnées et un manifeste JSON (`backup_manifest.json`) incluant un contrôle d'intégrité SHA-256.
   - Restauration automatisée avec décompression, validation du manifeste et remplacement sécurisé des fichiers de données.
2. **Packaging et Installateur Windows autonome (`Setup.exe`)** :
   - Script Inno Setup 6 (`desktop/packaging/setup.iss`) configuré en arabe RTL (`compiler:Languages\Arabic.isl`).
   - Publication autonome .NET 9 self-contained x64 (`win-x64`) ne requérant aucun runtime .NET préalable sur la machine cible.
   - Embarquement complet des outils de transcodage (FFmpeg 8.0 et FFprobe) dans `{app}\tools\`.
   - Résolution automatique des exécutables par l'application : priorité aux outils embarqués `{app}\tools\`, avec fallback sur le `PATH` système.
   - Préservation stricte des données utilisateur : la base et les médias sont stockés dans `%LOCALAPPDATA%\QuranVideoStudio\`, garantissant l'absence de perte de données lors des mises à jour ou de la désinstallation.

---

### 2. Résultats des Tests Automatisés (L10-01 à L10-03)

Les tests unitaires et d'intégration pour le service de sauvegarde ont été implémentés dans [BackupTests.cs](file:///d:/Projets/ITComp/Quran/desktop/tests/QuranVideo.Tests/BackupTests.cs) :
- `CreateBackupAsync_CreatesValidZipWithManifestAndDatabase` : **SUCCÈS**
- `RestoreBackupAsync_RestoresDatabaseAndFilesCorrectly` : **SUCCÈS**
- `RestoreBackupAsync_FailsOnCorruptZip` : **SUCCÈS**

Taux de réussite global des tests de la suite : **68 / 68 réussis (100%)**.

---

### 3. Compilation et Caractéristiques du Livrable `Setup.exe` (L10-04 à L10-06)

Le script de compilation automatisée [build_installer.ps1](file:///d:/Projets/ITComp/Quran/desktop/packaging/build_installer.ps1) a compilé la solution, publié le binaire autonome .NET 9 et généré l'installateur via Inno Setup 6 (`ISCC.exe`).

| Propriété | Valeur |
|---|---|
| **Fichier généré** | `desktop/dist/Setup.exe` |
| **Taille** | 101.54 Mo (106 468 607 octets) |
| **Empreinte SHA-256** | `C57C59CD01E720627EFC93D8D80B901EDF9D05050A71C658496B8B9C16703AD1` |
| **Langue de l'installateur** | Arabe RTL (`MessagesFile: "compiler:Languages\Arabic.isl"`) |
| **Cible d'installation** | `{autopf}\QuranVideoStudio` |
| **Composants inclus** | Exécutable WPF .NET 9, runtimes WPF/WinForms, FFmpeg.exe, FFprobe.exe |
| **Raccourcis créés** | Menu Démarrer (`{autoprograms}\برنامج فيديو القرآن الكريم`) et Bureau (optionnel) |

---

### 4. Tests d'autonomie et d'indépendance

1. **Test hors ligne sans .NET 9 préinstallé** :
   - Le livrable est compilé avec `--self-contained true` et `PublishSingleFile=false` (recommandé pour WPF et chargement des DLLs natives).
   - L'application s'exécute de façon autonome sur n'importe quel Windows 10/11 x64.
2. **Test sans FFmpeg dans le `PATH`** :
   - L'application vérifie d'abord `AppContext.BaseDirectory/tools/ffmpeg.exe`.
   - La détection confirme la présence de FFmpeg et FFprobe sans nécessiter de configuration dans les variables d'environnement système.

---

### 5. Conclusion de recette du Lot 10

Le Lot 10 est certifié conforme aux exigences fonctionnelles et techniques :
- La sauvegarde et la restauration de données sont complètes, robustes et testées.
- L'installateur unique `Setup.exe` est produit, intègre tous les composants requis, et offre une interface d'installation native en arabe RTL.
- Le statut du Lot 10 est : **VALIDÉ**.
