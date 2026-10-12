# Procès-Verbal de Recette — Lot 9 : Exports en série et reprise après incident

**Date de validation** : 2026-10-11  
**Référence du lot** : L09-01 à L09-06 & L09-R  
**Statut** : **VALIDÉ** (66/66 tests automatisés réussis dans `QuranVideo.Tests`)  
**Solution** : `desktop/QuranVideo.sln` (.NET 9.0.312, C#, WPF, EF Core SQLite, FFmpeg 8.0)

---

## 1. Objectifs du Lot 9 et Exigences Vérifiées

Le lot 9 implémente le moteur d'exportation en série (par lots / batch) et le gestionnaire de résilience après incident pour permettre l'encodage continu de sourates ou d'intervalles d'ayat sans intervention répétée de l'utilisateur, conformément aux exigences de `desktop/doc/CONCEPTION_DETAILLEE.md` et `desktop/doc/PLAN_IMPLEMENTATION.md`.

| Id Tâche | Description de l'exigence | Statut Réel | Preuve / Mécanisme |
|---|---|---|---|
| **L09-01** | Sélection de sourate et plage d'ayat ; exclusion stricte des ayat non annotées sans création de vidéos vides | **CONFORME** | `BatchExportService.EnqueueRangeAsync` vérifie la présence du passage audio et de l'image pour chaque ayah ; les ayat non annotées sont ignorées et listées dans le bilan. |
| **L09-02** | Figeage d'un snapshot immuable de chaque travail avec toutes ses dépendances (révisions audio/image, bornes, orientation) | **CONFORME** | `BatchExportJobEntity` stocke `AudioPath`, `ImagePath`, `PassageRevision`, `ImageRevision`, `StartTimeSec`, `EndTimeSec` au moment de la mise en file. |
| **L09-03** | Persistance des statuts dans SQLite (`Pending`, `Running`, `Completed`, `Failed`, `Cancelled`, `Interrupted`), détection et reclassement au redémarrage | **CONFORME** | `RecoverInterruptedJobsAsync` requalifie les travaux `Running` en `Interrupted` dès l'initialisation, avec possibilité de relance sans perte. |
| **L09-04** | Encodeur unique séquentiel (`SemaphoreSlim(1, 1)`), interface fluide, arrêt de la file, annulation individuelle et retry | **CONFORME** | Worker en arrière-plan non bloquant ; commandes `StopQueue`, `CancelJobAsync`, `RetryJobAsync` dans `BatchExportViewModel`. |
| **L09-05** | Vérification préalable des dépendances et de l'espace disque (>50 Mo) avant chaque rendu, production du manifeste | **CONFORME** | Contrôle `DriveInfo.AvailableFreeSpace` avant chaque travail, génération du manifeste JSON de production pour chaque MP4. |
| **L09-06** | Résolution déterministe des collisions par identifiants et révisions, nettoyage borné des fichiers `.tmp.mp4` orphelins | **CONFORME** | Nommage versionné `ayah_{surah:D3}_{ayah:D3}_{orient}_rev{rev}.mp4` et routine de nettoyage `CleanupOrphanTempFiles` ciblée sur `media/videos/`. |
| **L09-R** | Validation globale de la suite de tests et conformité aux règles architecturales | **CONFORME** | 66 tests unitaires et d'intégration réussis, 0 avertissement, 0 erreur de build. |

---

## 2. Résultats des Tests Automatisés

Exécution de la suite complète :
```powershell
dotnet test desktop/QuranVideo.sln
```
```text
Série de tests pour QuranVideo.Tests.dll (.NETCoreApp,Version=v9.0)
Au total, 1 fichiers de test ont correspondu au modèle spécifié.
Réussi!  - échec : 0, réussite : 66, ignorée(s) : 0, total : 66, durée : 3 s
```

### Détail des tests spécifiques au Lot 9 (`BatchExportTests.cs` et `ShellViewModelTests.cs`)
1. `RecoverInterruptedJobs_WhenJobsWereRunning_MarksThemAsInterrupted` : Vérifie que tout travail laissé à l'état `Running` (suite à une fermeture inopinée ou un crash) est reclassifié en `Interrupted` avec un message d'erreur explicite en arabe.
2. `EnqueueRange_SkipsUnannotatedAyahs_AndFreezesSnapshot` : Vérifie qu'une plage incluant des ayat non annotées ne planifie que les ayat éligibles, ignore les autres avec rapport détaillé, et fige les révisions exactes des sources.
3. `CancelJob_And_RetryJob_TransitionsStatesCorrectly` : Vérifie la transition d'un travail de `Pending` à `Cancelled`, puis sa remise en file `Pending` avec incrément du compteur de tentatives (`RetryCount = 1`).
4. `ProcessQueue_RendersBatchJobSuccessfully` : Vérifie le rendu séquentiel effectif d'un travail de la file par FFmpeg, la production de la vidéo MP4 et du manifeste associé.
5. `MainViewModel_NavigationToBatchExport_SwitchesViewModel` : Vérifie la navigation vers le nouvel onglet arabe RTL `التصدير بالدفعة` (Batch Export).

---

## 3. Interface Utilisateur du Lot 9 (`BatchExportView.xaml`)

L'interface arabe RTL propose 3 zones distinctes :
1. **Panneau de sélection et planification** :
   - Choix de la sourate parmi les 114.
   - Saisie de la plage d'ayat (de l'ayah X à l'ayah Y).
   - Choix du format (Horizontal 16:9 / Vertical 9:16).
   - Bouton `إضافة للقائمة ➕` avec rapport immédiat (ex: "تمت إضافة 5 آيات، وتخطي 2 آية غير مسجلة").
2. **Barre d'actions globales** :
   - Bouton vert `▶ بدء تصدير القائمة`.
   - Bouton rouge `⏹ إيقاف مؤقت`.
   - Bouton gris `📁 فتح المجلد`.
   - Message d'état dynamique en arabe.
3. **Tableau de suivi des travaux** :
   - Identifiant du job (`#1`, `#2`...).
   - Libellé de l'ayah (`سورة الفاتحة — الآية 1`) et format.
   - Statut en arabe (`في الانتظار ⏳`, `جارٍ التصدير ⚙`, `مكتمل ✔`, `فشل ✖`, `متوقف / مقطوع ⚠️`).
   - Barre de progression individuelle.
   - Boutons d'action unitaire : `🔄 إعادة المحاولة` et `✖ إلغاء`.

---

## 4. Conclusion et Autorisation de Passage

Le Lot 9 est intégralement validé. La file d'attente garantit la résilience, la sécurité des ressources matérielles et la stabilité du système.
L'équipe est autorisée à passer au **Lot 10 : Sauvegarde, installateur obligatoire (Inno Setup Setup.exe) et autonomie**.
