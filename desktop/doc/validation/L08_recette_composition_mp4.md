# Procès-Verbal de Recette — Lot 8 : Composition et export MP4 individuel

**Date de validation** : 2026-10-11  
**Référence du lot** : L08-01 à L08-07 & L08-R  
**Statut** : **VALIDÉ** (62/62 tests automatisés réussis dans `QuranVideo.Tests`)  
**Solution** : `desktop/QuranVideo.sln` (.NET 9.0.312, C#, WPF, FFmpeg 8.0)

---

## 1. Objectifs du Lot 8 et Exigences Vérifiées

Le lot 8 industrialise le pipeline de composition et d'export vidéo MP4 individuel par verset (ayah), en intégrant l'audio validé au Lot 6 et l'image sans perte extraite au Lot 7, conformément aux exigences d'ergonomie et de robustesse définies dans `desktop/doc/CONCEPTION_DETAILLEE.md` et `desktop/doc/PLAN_IMPLEMENTATION.md`.

| Id Tâche | Description de l'exigence | Statut Réel | Preuve / Mécanisme |
|---|---|---|---|
| **L08-01** | Modèle relationnel de composition liant ayah, récitateur, révisions audio/image, statut et orientation | **CONFORME** | `VideoCompositionEntity` dans `Entities.cs` et `AppDbContext`, gestion de statut (`Draft`, `Ready`, `Rendering`, `Completed`, `Failed`, `NeedsReview`). |
| **L08-02** | Filtrage d'éligibilité strict : interdiction d'assembler sans audio/image validés, détection des révisions sources modifiées | **CONFORME** | `CompositionService.PrepareCompositionAsync` refuse avec libellé arabe explicite si l'ayah n'est pas annotée ou si l'image manque ; réassigne `NeedsReview` si une révision amont change. |
| **L08-03** | Prévisualisation fidèle dans l'interface (cadre, fond blanc pur, proportions réelles) | **CONFORME** | Colonne de droite dans `StudioView.xaml` avec aperçu de l'ayah isolée sur fond blanc, référence de la page complète, et sélecteur de format. |
| **L08-04** | Interface épurée : choix Horizontal (16:9) / Vertical (9:16), message de progression et bouton arabe `🎬 إنشاء الفيديو` | **CONFORME** | Pas de formulaire technique complexe (pas de réglages bitrate/fps exposés), profil vidéo interne fixe (1080p, H.264/AAC, 30 fps, yuv420p). |
| **L08-05** | Pipeline FFmpeg robuste : `ProcessStartInfo.ArgumentList` sans concaténation de shell, processus sans fenêtre, gestion de progression et d'annulation | **CONFORME** | `FfmpegVideoGenerator.cs` exécute FFmpeg en sous-processus isolé, supporte `CancellationToken` et `IProgress<int>`. |
| **L08-06** | Génération atomique vers `.tmp.mp4`, validation FFprobe (dimensions, codecs, durée avec tolérance trame) avant promotion | **CONFORME** | Rendu dans un fichier temporaire unique, probe FFprobe systématique avant renommage atomique, détection d'artefact corrompu. |
| **L08-07** | Promotion vers destination versionnée et émission du manifeste sidecar JSON complet | **CONFORME** | Fichier final nommé `ayah_{surah:D3}_{ayah:D3}_{orient}_rev{rev}.mp4`, manifeste `ayah_{surah:D3}_{ayah:D3}_{orient}_manifest.json` avec SHA-256 de la vidéo, de l'audio et de l'image. |
| **L08-R** | Validation globale de la suite de tests et conformité aux règles architecturales | **CONFORME** | 62 tests unitaires et d'intégration réussis, 0 avertissement, 0 erreur de build. |

---

## 2. Résultats des Tests Automatisés

Exécution de la suite complète :
```powershell
dotnet test desktop/QuranVideo.sln
```
```text
Série de tests pour QuranVideo.Tests.dll (.NETCoreApp,Version=v9.0)
Au total, 1 fichiers de test ont correspondu au modèle spécifié.
Réussi!  - échec : 0, réussite : 62, ignorée(s) : 0, total : 62, durée : 2 s
```

### Détail des tests spécifiques au Lot 8 (`CompositionTests.cs`)
1. `PrepareComposition_WithoutAnnotatedPassage_ReturnsFailure` : Vérifie le rejet immédiat d'une ayah non annotée avec le message d'erreur en arabe : `لم يتم تسجيل أو تحديد أي مقطع صوتي لهذه الآية في الاستوديو.`
2. `PrepareComposition_WithValidPassage_CreatesReadyComposition` : Vérifie la création d'une composition à l'état `Ready` avec les bornes temporelles et le chemin d'image extrait.
3. `PrepareComposition_WhenPassageRevisionChanges_SetsNeedsReview` : Vérifie qu'une modification des bornes d'annotation (changement de `Revision`) bascule automatiquement le statut de la composition en `NeedsReview`.
4. `RenderComposition_ProducesValidMp4AndManifest` : Vérifie le rendu complet d'une vidéo 1080p 16:9 (1920x1080), le calcul de l'empreinte SHA-256 et la génération du manifeste sidecar JSON (`VideoProductionManifest`).
5. `RenderComposition_VerticalOrientation_Produces1080x1920` : Vérifie le rendu au format vertical 9:16 (1080x1920) pour les formats mobiles et réseaux sociaux (Reels/Shorts).

---

## 3. Structure du Manifeste de Production (`VideoProductionManifest`)

Exemple de manifeste produit sous `media/videos/manifests/ayah_001_001_h_manifest.json` :
```json
{
  "manifestVersion": "1.0",
  "compositionId": 1,
  "ayahId": "1:1",
  "surahNumber": 1,
  "ayahNumber": 1,
  "orientation": "Horizontal",
  "dimensions": {
    "width": 1920,
    "height": 1080
  },
  "durationSeconds": 2.0,
  "audio": {
    "sourceId": "husary_murattal_warsh:001",
    "sha256": "4b68427f7fbb67272719a6b5791983050c55cfbe81db5c1ef943b174092b7ef5",
    "startSec": 0.0,
    "endSec": 2.0,
    "revision": 1
  },
  "image": {
    "path": "D:\\...\\media\\images\\ayahs\\ayah_001_001_rev1.png",
    "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
    "revision": 1
  },
  "ffmpeg": {
    "version": "8.0-essentials",
    "videoCodec": "libx264",
    "audioCodec": "aac",
    "fps": 30,
    "pixelFormat": "yuv420p"
  },
  "videoSha256": "45d5a7cb90c9b0e2ad085cb2be10c14c553a1ec46b41d017c69994c67ebec755",
  "renderedAtUtc": "2026-10-11T21:34:00Z"
}
```

---

## 4. Conclusion et Autorisation de Passage

Le Lot 8 est intégralement validé. L'export individuel fonctionne de bout en bout de manière déterministe et traçable.
L'équipe est autorisée à passer au **Lot 9 : Exports en série et reprise après incident**.
