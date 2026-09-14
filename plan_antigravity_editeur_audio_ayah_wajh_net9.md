# Plan d’implémentation complet — Éditeur audio manuel Ayah / Wajh
## Plateforme Web Quran Warsh — صابر عبد الحكم
### Document d’exécution pour Antigravity

**Version :** 1.0  
**Date :** 2026-09-12  
**Framework imposé :** .NET 9  
**Backend :** ASP.NET Core 9  
**ORM :** Entity Framework Core 9  
**Base de données :** PostgreSQL  
**Frontend :** ASP.NET Core MVC / Razor Pages + JavaScript/TypeScript léger  
**Éditeur audio :** WaveSurfer.js + Regions + Timeline + Minimap  
**Traitement audio :** FFmpeg  
**Stockage :** S3-compatible / MinIO en développement  
**Source audio initiale :** https://archive.org/details/z0603_202406x

---

# 1. Objectif

Ce document doit être utilisé comme **spécification d’implémentation pour Antigravity**.

Le système doit remplacer le découpage automatique basé sur les silences par un processus **semi-manuel piloté par l’administrateur**.

L’administrateur doit pouvoir :

1. charger le MP3 original d’une sourate ;
2. visualiser sa waveform ;
3. écouter et se déplacer librement ;
4. zoomer ;
5. sélectionner précisément une plage temporelle ;
6. associer cette plage à une ayah ;
7. associer éventuellement seulement une partie du texte de l’ayah ;
8. sélectionner le **Wajh / وجه** ;
9. enregistrer les timestamps sans créer immédiatement un MP3 ;
10. relire la sélection en boucle ;
11. ajuster début et fin ;
12. valider ;
13. générer le MP3 final uniquement après validation ;
14. publier le segment.

---

# 2. Problème métier

La détection des silences ne doit pas être utilisée comme vérité métier.

Le cheikh peut :

- faire une pause au milieu d’une ayah ;
- reprendre une partie déjà lue ;
- répéter quelques mots ;
- réciter la même portion avec un autre وجه ;
- reprendre ensuite la suite de l’ayah ;
- enchaîner plusieurs ayat sans silence significatif.

Donc :

```text
silence ≠ limite d’ayah
1 ayah ≠ forcément 1 segment audio
```

Le modèle correct est :

```text
1 Ayah
   ↓
0..N AudioSegments
```

Chaque segment possède :

```text
SourceAudio
StartMs
EndMs
Ayah
StartWordIndex
EndWordIndex
Wajh
SegmentType
Status
```

---

# 3. Règles non négociables

1. La détection de silence ne détermine jamais les limites finales.
2. L’administrateur est l’autorité finale pour Start, End, Ayah, portion de texte et Wajh.
3. La création d’une région ne génère pas immédiatement un nouveau MP3.
4. On sauvegarde d’abord les métadonnées temporelles.
5. Le fichier final est rendu uniquement après validation.
6. Les segments peuvent se suivre, être espacés ou se chevaucher.
7. Plusieurs segments peuvent pointer vers la même ayah.
8. Un segment peut pointer vers seulement une partie de l’ayah.
9. Les fichiers sources ne sont jamais modifiés.
10. Les Wajh ne sont jamais codés en dur dans le frontend.

---

# 4. Exemple métier

Ayah logique :

```text
A B C D E F G H I J
```

Récitation :

```text
A B C D E F G H I J
puis D E F avec un autre Wajh
puis G H I J
```

Stockage :

| Segment | Début | Fin | Ayah | Texte | Wajh | Type |
|---|---:|---:|---:|---|---|---|
| S1 | 10.200 | 17.850 | 25 | A → J | W1 | FullAyah |
| S2 | 18.300 | 20.750 | 25 | D → F | W2 | Repetition |
| S3 | 21.000 | 24.500 | 25 | G → J | W1 | Continuation |

---

# 5. Architecture

```text
                                  INTERNET
                                     │
                                  HTTPS
                                     │
                                  NGINX
                                     │
                  ┌──────────────────┴──────────────────┐
                  │                                     │
           ASP.NET Core 9                        Object Storage
                  │                              S3 / MinIO
        ┌─────────┼────────────┐                        │
        │         │            │                        │
 Public Site   Admin UI      REST API                   │
        └─────────┴──────┬─────┘                        │
                         │                              │
                  Application Layer                    │
                         │                              │
                    EF Core 9                          │
                         │                              │
                    PostgreSQL                         │
                         │                              │
                         └──────── FFmpeg Worker ───────┘
```

---

# 6. Stack imposée

| Couche | Technologie |
|---|---|
| Runtime | .NET 9 |
| Web | ASP.NET Core 9 |
| ORM | EF Core 9 |
| DB | PostgreSQL |
| UI Admin | MVC/Razor + TypeScript/JavaScript |
| Waveform | WaveSurfer.js |
| Régions | WaveSurfer Regions |
| Timeline | WaveSurfer Timeline |
| Overview | WaveSurfer Minimap |
| Audio | FFmpeg |
| Auth | ASP.NET Core Identity |
| Storage | S3-compatible |
| Dev storage | MinIO |
| Logs | Serilog |
| API docs | OpenAPI |
| Tests | xUnit |
| Containers | Docker Compose |

---

# 7. Structure de solution

```text
QuranWarsh/
│
├── src/
│   ├── QuranWarsh.Domain/
│   ├── QuranWarsh.Application/
│   ├── QuranWarsh.Infrastructure/
│   ├── QuranWarsh.Web/
│   ├── QuranWarsh.AudioProcessor/
│   └── QuranWarsh.Import/
│
├── tests/
│   ├── QuranWarsh.UnitTests/
│   ├── QuranWarsh.IntegrationTests/
│   └── QuranWarsh.WebTests/
│
├── data/
│   ├── reference/
│   └── import/
│
├── scripts/
├── docker/
├── docker-compose.yml
└── README.md
```

---

# 8. Modèle de données

## Surah

```csharp
public class Surah
{
    public int Id { get; set; }
    public int Number { get; set; }
    public string NameArabic { get; set; } = null!;
    public string? NameLatin { get; set; }
    public int AyahCount { get; set; }
}
```

Contrainte : `UNIQUE(Number)`.

## Ayah

```csharp
public class Ayah
{
    public long Id { get; set; }
    public int SurahId { get; set; }
    public int AyahNumber { get; set; }
    public string TextWarsh { get; set; } = null!;
    public string SearchText { get; set; } = null!;
    public int? PageNumber { get; set; }
    public int? LineStart { get; set; }
    public int? LineEnd { get; set; }
}
```

Contrainte : `UNIQUE(SurahId, AyahNumber)`.

## AyahWord

Cette table permet d’associer un segment à une partie précise d’une ayah.

```csharp
public class AyahWord
{
    public long Id { get; set; }
    public long AyahId { get; set; }
    public int WordIndex { get; set; }
    public string TextWarsh { get; set; } = null!;
    public string? SearchText { get; set; }
}
```

Contrainte : `UNIQUE(AyahId, WordIndex)`.

## Reciter

```csharp
public class Reciter
{
    public int Id { get; set; }
    public string NameArabic { get; set; } = null!;
    public string? NameLatin { get; set; }
    public bool IsActive { get; set; }
}
```

Valeur initiale : `صابر عبد الحكم`.

## Riwaya

```csharp
public class Riwaya
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public string NameArabic { get; set; } = null!;
    public string? Description { get; set; }
}
```

Valeur initiale : `WARSH / ورش عن نافع`.

## Wajh

```csharp
public class Wajh
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public string NameArabic { get; set; } = null!;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}
```

Les Wajh doivent être configurables en base.

---

# 9. SourceAudio

Un `SourceAudio` représente le MP3 original d’une sourate.

```csharp
public class SourceAudio
{
    public Guid Id { get; set; }
    public int SurahId { get; set; }
    public int ReciterId { get; set; }
    public int RiwayaId { get; set; }
    public string OriginalFileName { get; set; } = null!;
    public string StorageKey { get; set; } = null!;
    public string? OriginalUrl { get; set; }
    public long DurationMs { get; set; }
    public long SizeBytes { get; set; }
    public string? ChecksumSha256 { get; set; }
    public int? Bitrate { get; set; }
    public string? Codec { get; set; }
    public SourceAudioStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
```

---

# 10. AudioSegment — entité centrale

```csharp
public class AudioSegment
{
    public Guid Id { get; set; }

    public Guid SourceAudioId { get; set; }
    public long AyahId { get; set; }
    public int? WajhId { get; set; }

    public long StartMs { get; set; }
    public long EndMs { get; set; }

    public int? StartWordIndex { get; set; }
    public int? EndWordIndex { get; set; }

    public AudioSegmentType SegmentType { get; set; }
    public int SequenceNumber { get; set; }

    public AudioSegmentStatus Status { get; set; }
    public string? Notes { get; set; }

    public string CreatedByUserId { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }

    public string? UpdatedByUserId { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public string? ValidatedByUserId { get; set; }
    public DateTimeOffset? ValidatedAt { get; set; }

    public Guid? PublishedAudioAssetId { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
```

## AudioSegmentType

```csharp
public enum AudioSegmentType
{
    FullAyah = 1,
    PartialAyah = 2,
    Repetition = 3,
    Continuation = 4,
    Other = 99
}
```

## AudioSegmentStatus

```csharp
public enum AudioSegmentStatus
{
    Draft = 1,
    Reviewed = 2,
    Approved = 3,
    Rendering = 4,
    Rendered = 5,
    Published = 6,
    Rejected = 7
}
```

Workflow :

```text
Draft → Reviewed → Approved → Rendering → Rendered → Published
```

---

# 11. AudioAsset

```csharp
public class AudioAsset
{
    public Guid Id { get; set; }
    public string StorageKey { get; set; } = null!;
    public string MimeType { get; set; } = "audio/mpeg";
    public long DurationMs { get; set; }
    public long SizeBytes { get; set; }
    public string ChecksumSha256 { get; set; } = null!;
    public int? Bitrate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
```

---

# 12. Validation métier

Toujours imposer :

```text
StartMs >= 0
EndMs > StartMs
EndMs <= SourceAudio.DurationMs
```

Pour les mots :

```text
StartWordIndex <= EndWordIndex
```

et les deux indexes doivent exister dans l’ayah.

Les chevauchements sont autorisés.

Exemple autorisé :

```text
Segment A : 10.000 → 20.000
Segment B : 17.000 → 22.000
```

L’UI affiche seulement un avertissement.

---

# 13. Interface principale

Route :

```text
/admin/audio-editor/{sourceAudioId}
```

Layout cible :

```text
┌──────────────────────────────────────────────────────────────┐
│ سورة البقرة       صابر عبد الحكم       ورش عن نافع         │
├──────────────────────────────────────────────────────────────┤
│                         WAVEFORM                             │
│ ▁▂▅▇████▆▃▂▂▅██████▅▃▂▁▂▅▇███▃▂ ...                       │
│             ├─────────────────┤                             │
│                 Région active                               │
├──────────────────────────────────────────────────────────────┤
│ TIMELINE                                                     │
│ 00:12:00        00:12:10        00:12:20        00:12:30    │
├──────────────────────────────────────────────────────────────┤
│ MINIMAP DE LA SOURATE                                        │
├──────────────────────────────────────────────────────────────┤
│ ▶ Play  ⏸ Pause  🔁 Loop |◀ -1s | +1s ▶| Zoom -  Zoom +    │
├──────────────────────────────┬───────────────────────────────┤
│ SEGMENT                      │ AYAH                          │
│ Start 00:12:13.427           │ Ayah [ 25 ▼ ]                │
│ End   00:12:21.854           │ ﴿ texte Warsh... ﴾           │
│ Durée 00:00:08.427           │ [sélection de mots]          │
│ Wajh [ ... ▼ ]               │                               │
│ Type [ ... ▼ ]               │                               │
│ Notes [...]                  │                               │
│ [▶ Preview] [✓ Enregistrer]  │                               │
└──────────────────────────────┴───────────────────────────────┘
```

---

# 14. WaveSurfer.js

Utiliser :

```text
WaveSurfer
Regions
Timeline
Minimap
Hover
```

Installation :

```bash
npm install wavesurfer.js
```

Exemple d’import :

```javascript
import WaveSurfer from 'wavesurfer.js'
import RegionsPlugin from 'wavesurfer.js/dist/plugins/regions.esm.js'
import TimelinePlugin from 'wavesurfer.js/dist/plugins/timeline.esm.js'
import MinimapPlugin from 'wavesurfer.js/dist/plugins/minimap.esm.js'
```

Une région doit être :

```text
draggable
resizable
```

Événements à gérer :

```text
region-created
region-clicked
region-update
region-updated
region-removed
```

---

# 15. Création et modification d’une région

Workflow :

```text
Nouvelle sélection
    ↓
drag souris sur waveform
    ↓
création region temporaire
    ↓
activeRegion
    ↓
choix Ayah
    ↓
choix mots si nécessaire
    ↓
choix Wajh
    ↓
Save
```

Ne pas écrire en base à chaque mouvement de souris.

Pour une région déjà enregistrée :

```text
region-updated → UI dirty
```

Puis :

```text
Save
```

ou auto-save futur avec debounce.

---

# 16. Précision temporelle

WaveSurfer utilise des secondes dans le navigateur.

Convertir :

```javascript
const startMs = Math.round(region.start * 1000)
const endMs   = Math.round(region.end * 1000)
```

Stockage PostgreSQL :

```text
BIGINT StartMs
BIGINT EndMs
```

Ne pas stocker les timestamps métier en float.

---

# 17. Outils d’écoute

Ajouter :

- Play/Pause ;
- seek ;
- loop region ;
- Preview ;
- Preview avec contexte ;
- zoom ;
- saisie d’un timestamp ;
- navigation rapide.

## Preview avec contexte

Paramètres :

```text
PreRoll = 500 ms
PostRoll = 500 ms
```

Lecture :

```text
StartMs - PreRoll → EndMs + PostRoll
```

---

# 18. Raccourcis clavier

| Touche | Action |
|---|---|
| Space | Play/Pause |
| L | Loop |
| ← | -500 ms |
| → | +500 ms |
| Shift+← | -2 s |
| Shift+→ | +2 s |
| + | Zoom + |
| - | Zoom - |
| Delete | Supprimer région |
| Ctrl+S | Save |
| N | Nouvelle région |

Désactiver les raccourcis si un champ texte a le focus.

---

# 19. Sélection de l’ayah

L’administrateur peut :

- choisir l’ayah ;
- taper son numéro ;
- aller à précédente/suivante ;
- filtrer ;
- voir immédiatement `TextWarsh`.

Ajouter :

```text
← Ayah précédente
Ayah suivante →
```

---

# 20. Sélection d’une partie de l’ayah

Afficher chaque mot comme élément sélectionnable :

```text
[الله] [لا] [إله] [إلا] [هو] [الحي] [القيوم]
```

Comportement :

1. clic sur premier mot ;
2. clic sur dernier mot ;
3. tous les mots intermédiaires sont sélectionnés.

Stocker :

```text
StartWordIndex
EndWordIndex
```

Bouton :

```text
Ayah complète
```

qui remet les indexes à `null`.

---

# 21. Segments existants sur la waveform

Charger tous les segments du `SourceAudio`.

Chaque région affiche :

```text
AyahNumber · WajhCode · Status
```

Exemple :

```text
25 · W1 · Draft
25 · W2 · Approved
26 · W1 · Published
```

Au clic sur une région :

- elle devient active ;
- le formulaire se remplit ;
- l’ayah est sélectionnée ;
- les mots sélectionnés sont surlignés.

---

# 22. Liste des segments

Sous l’éditeur :

| # | Ayah | Portion | Wajh | Start | End | Durée | Type | Statut |
|---|---:|---|---|---:|---:|---:|---|---|

Actions :

```text
Edit
Preview
Duplicate
Delete
Approve
Reject
Render
Publish
```

Tri par `StartMs`.

---

# 23. Duplicate Segment

Fonction obligatoire.

Dupliquer :

```text
Ayah
StartWordIndex
EndWordIndex
StartMs
EndMs
```

Puis permettre de changer :

```text
Wajh
Type
Notes
```

Cas d’usage : même zone récitative à classifier avec un autre Wajh ou créer une annotation proche.

---

# 24. API Admin

## Charger l’éditeur

```http
GET /api/admin/source-audios/{id}/editor
```

Réponse :

```json
{
  "sourceAudio": {},
  "surah": {},
  "ayahs": [],
  "segments": [],
  "wajhs": [],
  "waveform": {}
}
```

## Créer segment

```http
POST /api/admin/source-audios/{sourceAudioId}/segments
```

```json
{
  "ayahId": 123,
  "startMs": 813432,
  "endMs": 821876,
  "startWordIndex": 4,
  "endWordIndex": 8,
  "wajhId": 2,
  "segmentType": "Repetition",
  "notes": ""
}
```

## Modifier

```http
PUT /api/admin/audio-segments/{id}
```

Utiliser concurrence optimiste via `RowVersion`.

## Supprimer

```http
DELETE /api/admin/audio-segments/{id}
```

Interdire suppression directe si publié.

## Approve

```http
POST /api/admin/audio-segments/{id}/approve
```

## Reject

```http
POST /api/admin/audio-segments/{id}/reject
```

Motif obligatoire.

## Render

```http
POST /api/admin/audio-segments/{id}/render
```

## Publish

```http
POST /api/admin/audio-segments/{id}/publish
```

## Unpublish

```http
POST /api/admin/audio-segments/{id}/unpublish
```

---

# 25. Rendu FFmpeg

Le rendu n’est possible qu’après approbation.

Commande conceptuelle :

```bash
ffmpeg   -i source.mp3   -af "atrim=start=813.432:end=821.876,asetpts=PTS-STARTPTS"   output.mp3
```

Service :

```csharp
public interface IAudioSegmentRenderer
{
    Task<AudioAsset> RenderAsync(
        AudioSegment segment,
        CancellationToken cancellationToken);
}
```

Étapes :

```text
charger source
→ calculer timestamps
→ FFmpeg
→ vérifier code retour
→ vérifier fichier
→ SHA256
→ lire durée/métadonnées
→ uploader S3
→ créer AudioAsset
→ lier segment
→ statut Rendered
```

---

# 26. Stockage

Ne jamais écraser le fichier source.

```text
sources/
  002/
    original.mp3

published/
  002/
    255/
      warsh/
        w01/
          segment-{guid}.mp3
```

Nom logique :

```text
002_255_warsh_w02_01.mp3
```

PostgreSQL ne contient jamais le binaire MP3.

---

# 27. Publication publique

Page :

```text
/quran/{surah}/{ayah}
```

Elle récupère uniquement :

```text
AudioSegments Status=Published
```

Tri :

```text
Wajh.SortOrder
SequenceNumber
```

Si une récitation correspond à une partie d’ayah, montrer clairement la partie concernée.

---

# 28. Audit

Créer `AuditLog` :

```text
Id
UserId
Action
EntityType
EntityId
BeforeJson
AfterJson
CreatedAt
```

Actions :

```text
SEGMENT_CREATED
SEGMENT_UPDATED
SEGMENT_DELETED
SEGMENT_APPROVED
SEGMENT_REJECTED
SEGMENT_RENDERED
SEGMENT_PUBLISHED
SEGMENT_UNPUBLISHED
```

Il doit être possible de savoir qui a modifié Start/End/Ayah/Wajh et qui a validé.

---

# 29. Progression de traitement

Dashboard par sourate :

```text
Ayat total
Ayat annotées
Ayat validées
Ayat publiées
Segments Draft
Segments Rejected
```

Sidebar :

```text
1 ✓
2 ✓
3 ⚠
4 -
5 ✓
```

Légende :

```text
✓ validée
⚠ à vérifier
- non traitée
```

Ajouter :

```text
Ayah suivante non traitée
```

---

# 30. Performance des longues sourates

Ne pas supposer que le navigateur peut décoder confortablement chaque gros MP3 à chaque ouverture.

Prévoir des **peaks pré-calculés**.

Créer un artefact de waveform pour chaque source audio, par exemple :

```text
waveforms/{sourceAudioId}/peaks.json
```

Le système doit pouvoir charger la waveform à partir de données pré-calculées lorsque nécessaire.

Ajouter :

- Minimap ;
- lazy loading des données lourdes ;
- pagination/virtualisation de la liste des segments si nécessaire.

---

# 31. Service waveform

```csharp
public interface IWaveformService
{
    Task<WaveformData> GenerateAsync(
        SourceAudio sourceAudio,
        CancellationToken cancellationToken);
}
```

Les peaks peuvent être stockés dans l’Object Storage.

---

# 32. Silence detection

Option future uniquement.

Si implémentée :

```text
silencedetect → SuggestionMarker
```

Jamais :

```text
silencedetect → AudioSegment Approved
```

Distinguer visuellement :

```text
Regions = décisions humaines
Markers = suggestions automatiques
```

---

# 33. Import initial

Source audio initiale :

```text
https://archive.org/details/z0603_202406x
```

Créer un importeur qui inventorie :

```text
Surah
OriginalUrl
FileName
Duration
Size
Codec
Bitrate
Checksum
```

Ne pas effectuer de découpage automatique lors de l’import.

---

# 34. Référentiel Warsh et AyahWord

Importer :

```text
Surah
Ayah
TextWarsh
Page
LineStart
LineEnd
```

Puis construire `AyahWord`.

La tokenisation sert uniquement à :

```text
sélection visuelle des mots
StartWordIndex
EndWordIndex
```

Ce n’est pas une analyse linguistique.

Le texte source Warsh ne doit jamais être remplacé par le texte normalisé.

---

# 35. Authentification et rôles

ASP.NET Core Identity.

Rôles :

```text
Administrator
Reviewer
```

| Fonction | Admin | Reviewer |
|---|---:|---:|
| Créer segment | ✓ | ✓ |
| Modifier Draft | ✓ | ✓ |
| Supprimer Draft | ✓ | ✓ |
| Approve | ✓ | ✓ |
| Render | ✓ | |
| Publish | ✓ | |
| Gérer Wajh | ✓ | |
| Import source | ✓ | |

---

# 36. Sécurité

Implémenter :

- HTTPS ;
- CSRF ;
- authentification admin ;
- contrôle d’accès par rôle ;
- validation serveur de tous les timestamps ;
- protection Path Traversal ;
- aucun chemin FFmpeg fourni directement par le navigateur ;
- paramètres FFmpeg construits côté serveur ;
- logs structurés ;
- vérification MIME ;
- contrôle des IDs ;
- limites d’upload si upload futur.

Toute validation JavaScript doit exister également en C#.

---

# 37. Tests unitaires obligatoires

```text
AudioSegment_StartMustBeBeforeEnd
AudioSegment_CannotExceedSourceDuration
AudioSegment_WordRangeMustBeValid
AudioSegment_AllowsOverlap
AudioSegment_FullAyahCanHaveNullWordRange
ApprovedSegment_CannotBeInvalid
PublishedSegment_MustHaveAudioAsset
```

---

# 38. Tests d’intégration

Tester :

```text
PostgreSQL
EF Core migrations
CRUD segment
concurrency
approve
reject
render
publish
MinIO/S3
FFmpeg
```

---

# 39. Tests UI

Tester :

```text
ouvrir source
waveform
play/pause
seek
zoom
créer région
déplacer
redimensionner
loop
select Ayah
select words
select Wajh
save
reload
delete
approve
```

Après reload, les régions doivent réapparaître exactement aux mêmes positions.

---

# 40. Critères d’acceptation MVP éditeur

Le module est accepté si :

1. l’admin ouvre une sourate et voit la source ;
2. la waveform s’affiche ;
3. il peut zoomer ;
4. il peut sélectionner une plage ;
5. la plage fournit Start/End précis ;
6. il peut la lire en boucle ;
7. il peut choisir une ayah ;
8. le texte Warsh apparaît ;
9. il peut choisir une partie de l’ayah ;
10. il peut sélectionner un Wajh ;
11. Save ne crée pas immédiatement un MP3 ;
12. reload conserve la région ;
13. il peut déplacer/redimensionner ;
14. plusieurs segments peuvent cibler la même ayah ;
15. les chevauchements sont autorisés ;
16. un segment peut être approuvé ;
17. un segment approuvé peut être rendu ;
18. le rendu peut être pré-écouté ;
19. le segment peut être publié ;
20. le site public affiche le résultat.

---

# 41. Plan d’implémentation pour Antigravity

## Phase 1 — Socle

Créer :

```text
solution .NET 9
projects
Docker Compose
PostgreSQL
EF Core migrations
Identity
Serilog
OpenAPI
```

## Phase 2 — Domaine

Implémenter :

```text
Surah
Ayah
AyahWord
Reciter
Riwaya
Wajh
SourceAudio
AudioSegment
AudioAsset
AuditLog
```

## Phase 3 — Import Warsh

Implémenter :

```text
Surah import
Ayah import
AyahWord generation
```

## Phase 4 — Import audio pilote

Commencer uniquement avec :

```text
سورة الفاتحة
```

Créer `SourceAudio`, durée, checksum, codec, bitrate.

## Phase 5 — Prototype waveform

Route :

```text
/admin/audio-editor/{id}
```

Fonctions :

```text
waveform
play
pause
seek
zoom
timeline
minimap
```

Pas encore de persistance des segments.

## Phase 6 — Regions

Ajouter :

```text
create
drag
resize
delete
loop
start/end/duration
```

## Phase 7 — Persistance

Ajouter :

```text
POST
PUT
DELETE
reload regions
concurrency
```

## Phase 8 — Association Ayah

Ajouter :

```text
Ayah picker
TextWarsh
previous
next
```

## Phase 9 — Sélection de mots

Ajouter :

```text
AyahWord
StartWordIndex
EndWordIndex
```

## Phase 10 — Wajh et métadonnées

Ajouter :

```text
Wajh
SegmentType
Notes
```

## Phase 11 — Workflow

Ajouter :

```text
Draft
Reviewed
Approved
Rejected
Audit
```

## Phase 12 — FFmpeg

Ajouter :

```text
Approved
→ Render
→ AudioAsset
→ Rendered
```

## Phase 13 — Publication

Ajouter :

```text
Publish
Unpublish
/quran/{surah}/{ayah}
```

## Phase 14 — Al-Fatiha complète

Annoter réellement toute la Fatiha.

Corriger l’ergonomie avant toute industrialisation.

## Phase 15 — Al-Baqara pilote

Traiter uniquement les 20 premières ayat.

Valider :

```text
performance
répétitions
Wajh multiples
PartialAyah
longues sessions admin
```

## Phase 16 — Optimisation

Ajouter si nécessaire :

```text
pre-generated peaks
lazy loading
minimap
virtualisation
navigation rapide
```

## Phase 17 — Industrialisation

Seulement après validation :

```text
114 sourates
```

---

# 42. Livraisons attendues

## Livraison 1 — Éditeur fonctionnel

Doit contenir :

```text
.NET 9 solution
PostgreSQL
Surah
Ayah
AyahWord
SourceAudio
AudioSegment
Wajh
Login admin
Import Fatiha
Waveform
Timeline
Minimap
Créer région
Déplacer
Redimensionner
Loop
Associer Ayah
Sélectionner mots
Associer Wajh
Save
Reload
```

**Pas encore de rendu FFmpeg.**

Objectif : valider l’éditeur et le modèle.

## Livraison 2 — Validation et publication

Ajouter :

```text
Review
Approve
Reject
FFmpeg render
AudioAsset
MinIO/S3
Publish
Public ayah page
Audit log
```

## Livraison 3 — Pilote Al-Baqara

Ajouter :

```text
20 premières ayat
optimisation longues waveforms
peaks pré-calculés
navigation rapide
progression
next unprocessed ayah
```

---

# 43. Consignes de code

Antigravity doit respecter :

1. `.NET 9` uniquement.
2. Ne pas migrer vers .NET 10.
3. `async/await` pour I/O.
4. `CancellationToken` pour opérations I/O longues.
5. Pas de logique métier dans les controllers.
6. Services applicatifs dédiés.
7. Migrations EF Core.
8. PostgreSQL cible.
9. `Guid` pour SourceAudio/AudioSegment/AudioAsset.
10. timestamps en millisecondes entières.
11. aucun MP3 en base.
12. source audio immuable.
13. paramètres FFmpeg contrôlés côté serveur.
14. validation serveur obligatoire.
15. Wajh configurables.
16. chevauchements autorisés.
17. ne jamais supposer `1 ayah = 1 segment`.
18. pas de rendu MP3 à la création d’une région.
19. rendu seulement après approbation.
20. audit complet.

---

# 44. Definition of Done

Une fonctionnalité n’est terminée que si :

```text
code compile
tests verts
migration disponible si nécessaire
validation serveur présente
erreurs gérées
logs présents
UI utilisable
aucune donnée métier importante codée en dur
documentation mise à jour
```

---

# 45. Résultat final attendu

Workflow administrateur :

```text
Choisir Sourate
      ↓
Ouvrir MP3 source
      ↓
Écouter
      ↓
Zoomer
      ↓
Sélectionner région
      ↓
Choisir Ayah
      ↓
Choisir partie du texte si nécessaire
      ↓
Choisir Wajh
      ↓
Save
      ↓
Loop / vérifier
      ↓
Corriger
      ↓
Approve
      ↓
Render FFmpeg
      ↓
Preview
      ↓
Publish
```

Architecture métier :

```text
                    SOURCE MP3
                        │
                        ▼
                   Waveform UI
                        │
                        ▼
              Sélection humaine
                        │
           ┌────────────┴────────────┐
           │                         │
         Audio                    Texte
         Region                    Ayah
           │                         │
     StartMs / EndMs         WordStart / WordEnd
           │                         │
           └────────────┬────────────┘
                        │
                       Wajh
                        │
                        ▼
                   AudioSegment
                        │
                     Review
                        │
                     Approve
                        │
                        ▼
                      FFmpeg
                        │
                        ▼
                    AudioAsset
                        │
                        ▼
                    Publish
                        │
                        ▼
                    Site public
```

---

# 46. Priorité absolue

Antigravity doit d’abord produire un **éditeur audio fiable et confortable sur سورة الفاتحة**.

Ne pas industrialiser les 114 sourates avant que ce workflow soit validé :

```text
sélectionner
→ écouter
→ ajuster
→ associer
→ sauvegarder
→ relire
→ valider
```

La décision humaine est la donnée de référence pour la segmentation Ayah/Wajh.

---

# 47. Sources techniques

## .NET 9 — Microsoft

https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-9/overview

## ASP.NET Core 9 — Microsoft

https://learn.microsoft.com/aspnet/core/?view=aspnetcore-9.0

## Entity Framework Core — Microsoft

https://learn.microsoft.com/en-us/ef/core/

## WaveSurfer.js

https://wavesurfer.xyz/docs

## WaveSurfer Regions

https://wavesurfer.xyz/docs/types/plugins_regions.RegionParams

Les régions WaveSurfer exposent notamment des paramètres `start`, `end`, `drag`, `resize`, `resizeStart`, `resizeEnd`, ainsi que des événements de création, clic, mise à jour et suppression.

## FFmpeg

https://ffmpeg.org/

Documentation filtres :

https://ffmpeg.org/ffmpeg-filters.html

Le filtre `atrim` permet de sélectionner une section continue d’un flux audio et convient au rendu des segments validés.

## Corpus audio initial

https://archive.org/details/z0603_202406x

---

# 48. Règle finale à transmettre à Antigravity

> **Ne jamais considérer une coupure automatique comme une vérité fonctionnelle. La segmentation finale est créée, ajustée et validée par un humain. L’automatisation ne sert qu’à accélérer ce travail.**
