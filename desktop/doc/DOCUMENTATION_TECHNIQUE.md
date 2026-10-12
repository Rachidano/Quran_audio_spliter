# Documentation Technique — Application Desktop QuranVideo
## Architecture, Modèles de Données, Algorithmes et Procédures de Fabrication

- **Version du document** : 1.0.0
- **Cible technique** : C#, WPF, .NET 9 (`net9.0-windows`), SQLite, Inno Setup 6, FFmpeg 8.0

---

### 1. Architecture Générale

L'application est structurée selon un patron MVVM (Modèle-Vue-Modèle de Vue) allégé, sans suringénierie (pas de bus d'événements global, pas de médiateur externe, pas d'abstraction de repository générique).

```
desktop/
├── src/QuranVideo.Desktop/
│   ├── Data/                    # Entités de persistance, AppDbContext (EF Core SQLite)
│   ├── Models/                  # Modèles de données métier et contrats d'échange
│   ├── Services/                # Services applicatifs concrets :
│   │   ├── QuranImportService.cs    # Importation du corpus et validation des empreintes
│   │   ├── AudioManagementService.cs# Gestion du cache audio et téléchargement HTTP
│   │   ├── WaveformService.cs       # Génération progressive des formes d'onde audio
│   │   ├── AyahImageService.cs      # Extraction géométrique d'images d'ayat depuis les pages
│   │   ├── CompositionService.cs    # Pipeline de composition vidéo et rendu FFmpeg
│   │   ├── BatchExportService.cs    # File d'attente d'exports en série et reprise après crash
│   │   ├── BackupService.cs         # Sauvegarde SQLite Online Backup API et packaging ZIP
│   │   ├── FfmpegVideoGenerator.cs  # Wrapper robuste d'exécution FFmpeg / FFprobe
│   │   └── SettingsService.cs       # Gestion des préférences utilisateur
│   ├── ViewModels/              # Logique de présentation réactive (INotifyPropertyChanged)
│   │   ├── MainViewModel.cs         # Navigation globale entre les modules
│   │   ├── LibraryViewModel.cs      # Bibliothèque, recherche textuelle et affichage Mushaf
│   │   ├── StudioViewModel.cs       # Studio d'annotation audio, image et rendu vidéo
│   │   ├── BatchExportViewModel.cs  # File d'attente et gestion des exports par lots
│   │   └── SettingsViewModel.cs     # Sauvegarde, restauration et chemins
│   ├── Views/                   # Vues XAML natives avec FlowDirection="RightToLeft"
│   └── packaging/               # Script Inno Setup et script PowerShell de fabrication
└── tests/QuranVideo.Tests/      # Suite xUnit complète (68 tests unitaires et d'intégration)
```

---

### 2. Modèle de Données et Schéma SQLite

La persistance repose sur SQLite avec Entity Framework Core 9. La base est configurée en mode journal WAL pour permettre des lectures concurrentes et des snapshots cohérents.

#### Tables Principales (`AppDbContext.cs` & `Entities.cs`) :
1. **`EditionEntity`** : Métadonnées du Mushaf de référence (identifiant, révision, empreinte du manifeste).
2. **`SurahEntity`** : Les 114 sourates avec numérotation, nom arabe, translittération et nombre d'ayat.
3. **`AyahEntity`** : Les 6 214 ayat uniques (sourate, numéro d'ayah, numéro de page, texte normalisé et texte d'affichage).
4. **`PageEntity`** : Les 604 pages du Mushaf avec dimensions source en pixels, chemin relatif et empreinte SHA-256.
5. **`AyahRegionEntity`** : Rectangles de découpe pour chaque ligne de l'ayah dans la page (`X, Y, Width, Height, LineNumber`).
6. **`ReciterEntity` & `RecitationSourceEntity`** : Récitateurs et enregistrements audio locaux avec durée et empreinte.
7. **`AyahAudioAnnotationEntity`** : Bornes temporelles d'une ayah (`StartTimeMs, EndTimeMs, Revision, IsApproved, RepetitionTargetAyah`).
8. **`AyahImageRecipeEntity`** : Recette de découpe d'image de l'ayah (`PaddingTop, PaddingBottom, PaddingLeft, PaddingRight, Revision, Status, ImageSha256`).
9. **`VideoCompositionEntity`** : Composition vidéo unitaire (`Orientation, Resolution, Status, OutputPath, ManifestJson`).
10. **`BatchExportJobEntity`** : Travail de la file d'attente avec capture figée (`FrozenSnapshotJson, Status, ErrorMessage, RetryCount`).

---

### 3. Algorithme Géométrique d'Extraction d'Images (`AyahImageService`)

L'application interdit formellement toute recomposition de texte par police de caractères pour les vidéos : tous les pixels proviennent exclusivement des scans haute définition du Mushaf officiel (`data/mushaf/editions/marw-newMushaf/20260913-full/`).

#### Pipeline de Découpe :
1. **Résolution de coordonnées** :
   - Les régions sont stockées dans les coordonnées natives de la page PNG (ex: 1200 x 1800).
   - Les coordonnées d'affichage XAML ou le zoom de l'interface utilisateur ne sont jamais injectés dans la base.
2. **Création du Masque Binaire** :
   - Un masque binaire 8-bit est initialisé aux dimensions de la boîte englobante de l'ayah (`BoundingBox` calculée à partir des régions des lignes avec marges de sécurité).
   - Les pixels appartenant aux boîtes de chaque ligne sont reportés dans le masque.
3. **Rendu sur Fond Blanc Pur (`#FFFFFF`)** :
   - Pour chaque fragment, les pixels du Mushaf appartenant au masque sont copiés tels quels.
   - Les zones hors masque (versets voisins, marges externes) sont peintes en blanc pur sans perte d'information calligraphique.
4. **Cas Multi-Pages et Multi-Fragments** :
   - Pour une ayah à cheval sur deux pages (ex: fin d'une page et début de la suivante), deux fragments distincts sont extraits et empilés verticalement avec un espacement harmonieux.
5. **Micro-Ajustement (Nudge)** :
   - L'utilisateur peut appliquer des corrections (+/- 5px par direction). Tout ajustement incrémente le champ `Revision` de l'entité `AyahImageRecipeEntity` et révoque le cache image.

---

### 4. Moteur Audio et Horloge d'Échantillonnage

#### Problématique de Précision :
Les fichiers MP3 (notamment à débit variable VBR ou très longs comme la sourate Al-Baqara) souffrent d'imprécisions lors du décodage à la volée basé uniquement sur les millisecondes estimées par en-tête.

#### Solution Implémentée :
1. **Inspection Précise FFprobe** :
   - Lors de l'acquisition de la source audio, FFprobe inspecte le fichier pour déterminer la durée décodée exacte en échantillons et la fréquence d'échantillonnage (ex: 44 100 Hz).
2. **Découpe Temporelle FFmpeg** :
   - Lors de la découpe finale pour le multiplexage MP4, FFmpeg est exécuté avec l'argument d'entrée `-ss [StartSeconds] -to [EndSeconds]`, garantissant un décodage PCM intermédiaire à l'échantillon près, sans utiliser de découpe binaire destructive de trames MP3.
3. **Validation Post-Rendu** :
   - La durée du fichier temporaire `.tmp.mp4` est mesurée avec FFprobe et comparée à la durée attendue (`EndTimeMs - StartTimeMs`). Un écart supérieur à 250 ms déclenche un échec explicite de l'exportation.

---

### 5. Pipeline de Composition et Concurrence de Rendu

1. **Composition Unitaire (`CompositionService`)** :
   - Supporte les formats 16:9 (`1920x1080`) et 9:16 (`1080x1920`).
   - Génération d'une commande FFmpeg optimisée avec filtre de centrage et fond uni :
     ```text
     -loop 1 -i [image_ayah.png] -ss [start] -to [end] -i [audio.mp3]
     -c:v libx264 -tune stillimage -pix_fmt yuv420p -c:a aac -b:a 192k -shortest [output.tmp.mp4]
     ```
   - Exécution via `ProcessStartInfo` avec redirection des flux, sans ouverture de fenêtre console Windows (`CreateNoWindow = true`).
2. **File d'Attente par Lots (`BatchExportService`)** :
   - Utilise un `SemaphoreSlim(1, 1)` strict pour sérialiser les encodages vidéo. Cette mesure préserve les capacités du processeur et de la mémoire vive pour garantir une réactivité parfaite de l'interface graphique.
   - **Snapshots Immuables** : Chaque tâche de lot stocke dans `FrozenSnapshotJson` l'état figé de l'ayah, de l'audio et des marges. Une retouche ultérieure dans le studio ne corrompt pas le travail en file.
   - **Reprise après crash (`RecoverInterruptedJobsAsync`)** : Au démarrage de l'application, tout travail marqué `Running` dans la base SQLite est automatiquement requalifié en `Interrupted`, évitant les blocages infinis et permettant une relance propre.

---

### 6. Sauvegarde et Restauration (`BackupService`)

1. **Snapshot SQLite Cohérent** :
   - Exécution de l'instruction SQL standard `VACUUM INTO '[temp_path]'`.
   - Cette commande crée une copie intégrale et compactée de la base de données, intégrant de manière atomique toutes les transactions du journal WAL en cours, sans bloquer les opérations de lecture.
2. **Archivage ZIP avec Manifeste d'Intégrité** :
   - L'archive ZIP regroupe la base SQLite snapshotée, les fichiers audios personnalisés du dossier `custom_audio/`, et un manifeste JSON (`backup_manifest.json`).
   - Le manifeste consigne la version de l'application, la date, la liste des fichiers inclus et leur empreinte SHA-256 respective.
3. **Gestion des Verrous de Fichiers sous Windows** :
   - Lors de la restauration, l'appel à `SqliteConnection.ClearAllPools()` libère immédiatement les connexions du pool ADO.NET avant le remplacement atomique du fichier de données.

---

### 7. Procédure de Fabrication de l'Installateur Inno Setup

#### Script de Publication et Compilation (`desktop/packaging/build_installer.ps1`) :
1. **Compilation et Publication .NET 9** :
   ```powershell
   dotnet publish desktop/src/QuranVideo.Desktop/QuranVideo.Desktop.csproj `
       -c Release -r win-x64 --self-contained true `
       -p:PublishSingleFile=false -o desktop/publish
   ```
2. **Compilation Inno Setup (`setup.iss`)** :
   ```powershell
   & "C:\Users\r_kaci\AppData\Local\Programs\Inno Setup 6\ISCC.exe" desktop/packaging/setup.iss
   ```
3. **Résultat du Livrable** :
   - Fichier : `desktop/dist/Setup.exe`
   - Taille : ~101 Mo
   - Caractéristiques : Interface d'installation en arabe RTL, .NET 9 autonome, binaires FFmpeg 8.0 et FFprobe inclus.
