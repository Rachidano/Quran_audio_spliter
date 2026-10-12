# Procès-verbal de recette — Lot 2 : Socle de l'application Windows (L02-R)

**Date** : 11 octobre 2026  
**Intervenant** : Antigravity  
**Statut** : **VALIDÉ** (Critères techniques et d'intégration validés, 14/14 tests automatiques réussis)

---

## 1. Périmètre et objectifs du Lot 2

Le Lot 2 met en place l'architecture applicative Windows définitive sous `desktop/` sans impacter le site web ni les données sources existantes :
1. **Solution et projets uniques** : Solution `desktop/QuranVideo.sln` regroupant un seul projet applicatif WPF (`desktop/src/QuranVideo.Desktop/QuranVideo.Desktop.csproj`) ciblant `net9.0-windows` et un projet de tests (`desktop/tests/QuranVideo.Tests/QuranVideo.Tests.csproj`).
2. **Architecture MVVM légère sans suringénierie** : Pas de repository générique, pas d'event bus ni de système de plugins. Utilisation de `CommunityToolkit.Mvvm` (8.4.0) avec commandes asynchrones et annulation (`CancellationToken`).
3. **Expérience utilisateur arabe RTL** :
   - Interface native en `FlowDirection="RightToLeft"`.
   - Trois entrées principales imposées : `المكتبة`, `إعداد الفيديو`, `فيديوهاتي`.
   - Menu secondaire pour les réglages et le diagnostic technique.
   - Isolation stricte des valeurs numériques, temporelles et chemins de fichiers en `FlowDirection="LeftToRight"`.
   - Aucun champ ni catalogue de « wajh » (أوجه).
4. **Gestion de l'espace de travail** :
   - Emplacement par défaut dans `Mes Documents\QuranVideo`.
   - Verrouillage exclusif par fichier `.lock` (`FileShare.None`) avec message d'erreur explicite en arabe si une autre instance est ouverte.
   - Chemins relatifs pour tous les médias.
   - Reprise automatique de la dernière session (`config.json`).
5. **Journalisation et diagnostic** :
   - Journal local dans `logs/quran_video.log`.
   - Version figée `1.0.0`.
   - Rapport de diagnostic exportable en JSON (`ExportDiagnosticsAsync`) excluant tout média.

---

## 2. Structure réalisée sous `desktop/`

```
desktop/
├── QuranVideo.sln
├── global.json                  (SDK 9.0.312)
├── src/
│   └── QuranVideo.Desktop/
│       ├── QuranVideo.Desktop.csproj
│       ├── App.xaml / App.xaml.cs
│       ├── Models/
│       │   ├── AyahGeometry.cs
│       │   ├── SessionState.cs
│       │   └── SurahInfo.cs
│       ├── Services/
│       │   ├── AudioPrecisionBenchmark.cs
│       │   ├── AyahImageExtractor.cs
│       │   ├── FfmpegVideoGenerator.cs
│       │   ├── LoggingService.cs
│       │   ├── SurahCatalogService.cs   (114 sourates Warsh, Baqara = 285 ayat)
│       │   └── WorkspaceService.cs       (Verrou .lock, structure, chemins relatifs)
│       ├── ViewModels/
│       │   ├── ViewModelBase.cs
│       │   ├── MainViewModel.cs          (Navigation 3 onglets, session auto)
│       │   ├── LibraryViewModel.cs       (Recherche, filtrage, sélection sourate)
│       │   ├── StudioViewModel.cs        (Bornes audio, aperçu image, création vidéo)
│       │   ├── MyVideosViewModel.cs      (Liste MP4, lecture, ouverture dossier)
│       │   └── SettingsViewModel.cs      (Infos version, diagnostic exportable)
│       └── Views/
│           ├── MainWindow.xaml / MainWindow.xaml.cs
│           ├── LibraryView.xaml / LibraryView.xaml.cs
│           ├── StudioView.xaml / StudioView.xaml.cs
│           ├── MyVideosView.xaml / MyVideosView.xaml.cs
│           └── SettingsDialog.xaml / SettingsDialog.xaml.cs
└── tests/
    └── QuranVideo.Tests/
        ├── QuranVideo.Tests.csproj
        ├── GeometryTests.cs              (5 tests géométrie / pixels)
        ├── WorkspaceTests.cs             (5 tests espace, verrou, session, diagnostic, 114 sourates)
        └── ShellViewModelTests.cs        (4 tests navigation MVVM, recherche, pagination ayat)
```

---

## 3. Résultats des tests automatiques

Exécution de `dotnet test desktop/QuranVideo.sln` :

```text
Série de tests pour D:\Projets\ITComp\Quran\desktop\tests\QuranVideo.Tests\bin\Debug\net9.0-windows\QuranVideo.Tests.dll (.NETCoreApp,Version=v9.0)
Version de VSTest 17.14.1 (x64)

Démarrage de l'exécution de tests, patientez...
Au total, 1 fichiers de test ont correspondu au modèle spécifié.

Réussi!  - échec : 0, réussite : 14, ignorée(s) : 0, total : 14, durée : 208 ms - QuranVideo.Tests.dll (net9.0)
```

### Détail des 14 tests exécutés :
1. `GeometryTests.ConvertToPixels_WithIdentityBoxes_MapsToExactDimensions` : Conforme.
2. `GeometryTests.ConvertToPixels_WithNestedCropAndTextBox_ComputesExactFormula` : Conforme (263.092 x 357.408 px).
3. `GeometryTests.ConvertToPixels_WithMinimumTopClip_ClipsTopAndReducesHeight` : Conforme.
4. `GeometryTests.ConvertToPixels_WhenMinimumTopExceedsBottom_ReturnsEmptyRect` : Conforme.
5. `GeometryTests.ComputeEnclosingBoundingBox_IncludesAllRectanglesWithPadding` : Conforme.
6. `WorkspaceTests.InitializeWorkspace_CreatesRequiredDirectoryStructure` : Structure `data/`, `media/mushaf`, `media/audio`, `media/images`, `media/videos`, `logs/` et `.lock` créée.
7. `WorkspaceTests.InitializeWorkspace_WhenAlreadyLockedByAnotherInstance_ReturnsFailureWithArabicMessage` : Double ouverture bloquée avec message arabe explicite.
8. `WorkspaceTests.SessionState_SavesAndRestoresCorrectly` : Persistance et restauration exacte de la session.
9. `WorkspaceTests.ExportDiagnostics_GeneratesCompleteJsonWithoutMediaFiles` : Diagnostic JSON généré sans aucun fichier média personnel.
10. `WorkspaceTests.SurahCatalogService_Contains114SurahsWithWarshBaqarah285Ayahs` : 114 sourates validées, Baqara à 285 ayat.
11. `ShellViewModelTests.MainViewModel_InitializesWithLibraryAsDefaultTab` : L'onglet `المكتبة` est l'accueil par défaut.
12. `ShellViewModelTests.MainViewModel_NavigationBetweenTheThreeMainTabs_SwitchesViewModelAndStatus` : Navigation fluide entre les 3 onglets.
13. `ShellViewModelTests.LibraryViewModel_SearchFilter_FindsByArabicNameNumberAndLatin` : Recherche fonctionnelle par numéro, nom arabe ou translittération latine.
14. `ShellViewModelTests.StudioViewModel_NextAndPreviousAyah_TraversesAyahsAccurately` : Défilement séquentiel des ayat et passage automatique de sourate en fin de parcours.

---

## 4. Vérification des critères d'acceptation du Lot 2

| Critère du plan | Vérification concrète | Statut |
|---|---|---|
| Démarrage sans serveur web, PostgreSQL ni internet | L'application s'appuie sur le système de fichiers local et SQLite. | **VALIDÉ** |
| Création, fermeture et réouverture d'un espace vide | Testé via `WorkspaceTests` ; cycle de vie du verrou validé. | **VALIDÉ** |
| Erreurs lisibles pour dossier inaccessible ou déjà ouvert | Message arabe explicite retourné et testé (`مجلد العمل مستخدم حالياً...`). | **VALIDÉ** |
| Tâches longues non bloquantes | Génération vidéo avec `AsyncRelayCommand` et bouton d'annulation (`CancellationToken`). | **VALIDÉ** |
| 3 entrées compréhensibles pour un utilisateur non informaticien | `المكتبة` (Library), `إعداد الفيديو` (Studio), `فيديوهاتي` (My Videos). | **VALIDÉ** |
| Pas de manipulation de chemins techniques pour ouvrir une sourate | Sélection visuelle directe de la sourate et bouton d'ouverture. | **VALIDÉ** |

---

## 5. Conclusion et passage au Lot 3

Le **Lot 2 (Socle de l'application Windows)** est intégralement réalisé et validé.
La prochaine étape est le **Lot 3 — Base locale et import des ressources** (L03-01 à L03-07).
