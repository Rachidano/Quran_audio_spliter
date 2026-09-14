# Plan d’implémentation complémentaire — Mushaf Warsh en images
## Extension du plan Antigravity — Affichage fidèle selon الرسم العثماني

**Version :** 1.0  
**Date :** 2026-09-12  
**Projet principal :** QuranWarsh  
**Framework :** .NET 9  
**But :** compléter le plan d’implémentation de l’éditeur audio par un système d’affichage graphique fidèle du Mushaf Warsh

---

# 1. Objectif

Le texte Unicode ne doit pas être considéré comme le rendu graphique principal du Coran.

Le projet doit adopter une architecture hybride :

```text
Données textuelles Warsh
        +
Pages officielles du Mushaf Warsh
```

Le texte Unicode sert principalement à :

- la recherche ;
- l’indexation ;
- la navigation ;
- les métadonnées ;
- les URLs ;
- les associations logiques.

Les images du Mushaf servent à :

- l’affichage utilisateur ;
- la fidélité au الرسم العثماني ;
- le repérage visuel des ayat ;
- le surlignage ;
- l’annotation graphique ;
- l’association avec les segments audio.

---

# 2. Principe d’architecture

```text
                   AYAH
                    │
        ┌───────────┴───────────┐
        │                       │
 Texte Warsh              Mushaf Warsh
 Unicode                   Image officielle
        │                       │
 Recherche                  Affichage fidèle
 Indexation                 Zoom
 Navigation                 Highlight
 Métadonnées                Crop
        │                       │
        └───────────┬───────────┘
                    │
               Site public
```

---

# 3. Source graphique

Utiliser une source fiable du Mushaf Warsh.

Références :

## مجمع الملك فهد لطباعة المصحف الشريف

https://qurancomplex.gov.sa/

Plateforme technique / ressources développeurs :

https://qurancomplex.gov.sa/en/techquran/dev/

Le Complexe fournit notamment des ressources Unicode et des outils destinés à la publication informatique du Mushaf.

Le projet doit privilégier :

```text
Mushaf Warsh
+
الرسم العثماني
+
source officielle ou validée
```

---

# 4. Ne pas reconstruire le Mushaf

Antigravity ne doit pas générer visuellement le Mushaf à partir d’un texte arabe standard.

Éviter :

```text
Unicode
↓
Font arbitraire
↓
Reconstitution du Mushaf
```

Préférer :

```text
Image officielle
↓
Affichage Web
```

---

# 5. Organisation des ressources images

Conserver les masters séparément des versions Web.

```text
mushaf/
│
├── masters/
│   └── warsh/
│       ├── page_001.png
│       ├── page_002.png
│       ├── page_003.png
│       └── ...
│
└── web/
    └── warsh/
        ├── page_001.webp
        ├── page_002.webp
        ├── page_003.webp
        └── ...
```

Le format master ne doit jamais être écrasé.

---

# 6. Formats recommandés

## Master

Conserver :

```text
PNG
TIFF
ou
format original source
```

selon la ressource obtenue.

## Web

Générer :

```text
WebP
```

Optionnellement :

```text
AVIF
```

Le projet doit toujours conserver l’original.

---

# 7. Nouvelle entité MushafEdition

Créer :

```csharp
public class MushafEdition
{
    public int Id { get; set; }

    public string Code { get; set; } = null!;

    public string NameArabic { get; set; } = null!;

    public string? NameLatin { get; set; }

    public int RiwayaId { get; set; }

    public string? Publisher { get; set; }

    public string? SourceUrl { get; set; }

    public bool IsActive { get; set; }
}
```

Valeur initiale :

```text
Code = MADINAH_WARSH
NameArabic = مصحف المدينة - رواية ورش
```

---

# 8. Nouvelle entité MushafPage

```csharp
public class MushafPage
{
    public int Id { get; set; }

    public int MushafEditionId { get; set; }

    public int PageNumber { get; set; }

    public string MasterStorageKey { get; set; } = null!;

    public string WebStorageKey { get; set; } = null!;

    public int Width { get; set; }

    public int Height { get; set; }

    public string? ChecksumSha256 { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
```

Contrainte :

```text
UNIQUE(MushafEditionId, PageNumber)
```

---

# 9. Nouvelle entité AyahImageRegion

Une ayah peut occuper :

- une partie de ligne ;
- plusieurs lignes ;
- plusieurs portions de la même page ;
- éventuellement deux pages selon la structure du Mushaf.

Ne pas stocker une seule bounding box.

Créer :

```csharp
public class AyahImageRegion
{
    public long Id { get; set; }

    public long AyahId { get; set; }

    public int MushafPageId { get; set; }

    public decimal X { get; set; }

    public decimal Y { get; set; }

    public decimal Width { get; set; }

    public decimal Height { get; set; }

    public int Sequence { get; set; }
}
```

---

# 10. Coordonnées normalisées

Les coordonnées doivent être stockées entre :

```text
0.0
et
1.0
```

Exemple :

```text
X      = 0.0823
Y      = 0.4271
Width  = 0.8125
Height = 0.0634
```

Ne pas stocker uniquement des pixels.

Avantage :

```text
image 500 px
image 1000 px
image 2000 px
```

Les régions restent correctes.

---

# 11. Conversion coordonnées écran → coordonnées normalisées

En JavaScript :

```javascript
const normalizedX = region.x / displayedImageWidth
const normalizedY = region.y / displayedImageHeight

const normalizedWidth = region.width / displayedImageWidth
const normalizedHeight = region.height / displayedImageHeight
```

Affichage :

```javascript
const x = normalizedX * currentWidth
const y = normalizedY * currentHeight
```

---

# 12. Une ayah = N régions

Exemple :

```text
Ayah 2:255

Region 1
Region 2
Region 3
```

Stocker :

```text
Sequence = 1
Sequence = 2
Sequence = 3
```

L’ordre doit être conservé.

---

# 13. Extension d’Ayah

L’entité `Ayah` conserve :

```csharp
public ICollection<AyahImageRegion> ImageRegions { get; set; }
```

Le texte reste également disponible :

```text
TextWarsh
SearchText
```

mais ne sert plus de rendu visuel principal.

---

# 14. ImageRegion ne remplace pas Ayah

Toujours conserver :

```text
AyahId
```

comme clé logique.

Architecture :

```text
Ayah
 ├── TextWarsh
 ├── SearchText
 └── AyahImageRegions[]
```

---

# 15. Relation avec AudioSegment

Le système audio précédent conserve :

```text
AudioSegment
 ├── SourceAudioId
 ├── StartMs
 ├── EndMs
 ├── AyahId
 ├── WajhId
 └── Status
```

Le lien graphique se fait via :

```text
AudioSegment.AyahId
        ↓
Ayah
        ↓
AyahImageRegions
```

---

# 16. Segment partiel d’une ayah

Pour les répétitions partielles, créer une nouvelle entité :

```csharp
public class AudioSegmentImageRegion
{
    public long Id { get; set; }

    public Guid AudioSegmentId { get; set; }

    public int MushafPageId { get; set; }

    public decimal X { get; set; }

    public decimal Y { get; set; }

    public decimal Width { get; set; }

    public decimal Height { get; set; }

    public int Sequence { get; set; }
}
```

Utilité :

```text
AudioSegment
→ portion sonore
→ portion visuelle exacte du Mushaf
```

---

# 17. Relation finale

```text
SourceAudio
    │
    ▼
AudioSegment
    │
    ├── AyahId
    ├── WajhId
    ├── StartMs
    ├── EndMs
    │
    └── SegmentImageRegions[]
             │
             ▼
          MushafPage
```

---

# 18. Interface d’annotation Mushaf

Dans l’éditeur audio :

```text
┌──────────────────────────────────────────────────────────────┐
│ WAVEFORM                                                     │
│ ▂▅████▅▂▂██████████▅▃▂                                     │
│         |<------ audio selection ------>|                   │
├───────────────────────────────┬──────────────────────────────┤
│ AYAH / WAJH                   │ MUSHAF                       │
│                               │                              │
│ Sourate : البقرة              │ [ page officielle Warsh ]    │
│ Ayah : 255                    │                              │
│ Wajh : W02                    │ [zone(s) surlignée(s)]       │
│                               │                              │
│ Start : ...                   │                              │
│ End   : ...                   │                              │
├───────────────────────────────┴──────────────────────────────┤
│ [ Ajouter région ] [ Supprimer ] [ Sauvegarder ]            │
└──────────────────────────────────────────────────────────────┘
```

---

# 19. Modes d’annotation

Prévoir deux modes.

## Mode A — Ayah complète

L’administrateur choisit :

```text
Ayah = 255
```

Le système charge automatiquement les `AyahImageRegions`.

Aucune nouvelle sélection graphique n’est nécessaire.

## Mode B — Portion d’ayah

L’administrateur active :

```text
Sélection graphique manuelle
```

Puis dessine une ou plusieurs régions.

Ces régions sont stockées dans :

```text
AudioSegmentImageRegion
```

---

# 20. Outil de sélection image

Le composant frontend doit permettre :

- zoom ;
- déplacement ;
- dessin rectangle ;
- redimensionnement ;
- suppression ;
- multi-régions ;
- affichage des régions existantes.

Librairies possibles :

```text
Konva.js
Fabric.js
OpenSeadragon + overlay
```

Le choix peut être laissé à Antigravity.

Critère principal :

```text
zoom fluide
+
overlay précis
+
coordonnées normalisées
```

---

# 21. Recommandation frontend

Pour des pages de Mushaf haute résolution, privilégier :

```text
OpenSeadragon
```

si la taille des images est importante et si un zoom profond est nécessaire.

Sinon :

```text
Konva.js
```

peut être utilisé pour les overlays et la sélection.

---

# 22. Zoom Mushaf

Le zoom doit permettre :

```text
page entière
↓
demi-page
↓
quelques lignes
↓
un mot
```

L’administrateur doit pouvoir dessiner précisément une portion.

---

# 23. Surlignage

Une région graphique doit être affichée avec :

```text
overlay semi-transparent
bordure
label optionnel
```

Exemple label :

```text
2:255
W02
Segment 3
```

---

# 24. Affichage public — Mode page

Route possible :

```text
/mushaf/warsh/page/{page}
```

Affichage :

```text
page officielle complète
+
ayah sélectionnée en surbrillance
```

---

# 25. Affichage public — Mode ayah

Route existante :

```text
/quran/{surah}/{ayah}
```

Afficher :

```text
extrait graphique
+
audio
+
Wajh
```

---

# 26. Affichage public — Mode hybride recommandé

```text
AYAH
────────────────────────
[ extrait الرسم العثماني ]

▶ Wajh 1
▶ Wajh 2

[ Voir dans le Mushaf ]

        ↓

Page complète
+
Ayah surlignée
```

---

# 27. Génération d’un extrait visuel

Ne pas forcément générer physiquement un fichier image par ayah.

Deux approches :

## Option 1 — Crop dynamique CSS/Canvas

Afficher uniquement les régions nécessaires.

Avantages :

- pas de duplication ;
- source unique ;
- plus flexible.

## Option 2 — Cache d’extraits

Générer à la demande :

```text
ayah-crops/
  002/
    255.webp
```

Utiliser seulement comme cache.

---

# 28. Recommandation

Utiliser :

```text
MushafPage
+
AyahImageRegions
```

comme source de vérité.

Les crops sont dérivés et peuvent être régénérés.

---

# 29. Service d’image

Créer :

```csharp
public interface IMushafImageService
{
    Task<MushafPageDto> GetPageAsync(
        int editionId,
        int pageNumber,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AyahImageRegionDto>> GetAyahRegionsAsync(
        long ayahId,
        CancellationToken cancellationToken);
}
```

---

# 30. Service de crop

Optionnel :

```csharp
public interface IAyahCropService
{
    Task<GeneratedImageAsset> GenerateAyahCropAsync(
        long ayahId,
        CancellationToken cancellationToken);
}
```

---

# 31. Import Mushaf

Créer un module :

```text
QuranWarsh.Import.Mushaf
```

Étapes :

```text
1. importer édition
2. importer pages
3. calculer taille
4. calculer checksum
5. générer WebP
6. uploader storage
7. créer MushafPage
```

---

# 32. Association Ayah → Page

Si le référentiel Warsh fournit :

```text
page
line_start
line_end
```

utiliser ces données pour pré-positionner l’administrateur.

Ne pas supposer que ces lignes suffisent pour les régions exactes.

---

# 33. Workflow d’annotation graphique

```text
Choisir MushafEdition
    ↓
Choisir Page
    ↓
Choisir Ayah
    ↓
Afficher page
    ↓
Dessiner régions
    ↓
Sauvegarder
    ↓
Preview
    ↓
Valider
```

---

# 34. Page d’administration dédiée

Route :

```text
/admin/mushaf/ayah-region-editor/{ayahId}
```

Layout :

```text
┌───────────────────────┬────────────────────────────────────┐
│ Ayah                  │ Mushaf Page                        │
│                       │                                    │
│ سورة البقرة            │                                    │
│ 255                   │         [ Image ]                  │
│                       │                                    │
│ Page 42               │     [Regions overlays]             │
│                       │                                    │
│ [Prev] [Next]         │                                    │
├───────────────────────┴────────────────────────────────────┤
│ Regions : 1 2 3                                            │
│ [Add] [Delete] [Save] [Validate]                           │
└────────────────────────────────────────────────────────────┘
```

---

# 35. API Mushaf

## Editions

```http
GET /api/mushaf/editions
```

## Page

```http
GET /api/mushaf/editions/{editionId}/pages/{pageNumber}
```

## Régions d’ayah

```http
GET /api/mushaf/ayahs/{ayahId}/regions
```

## Admin create region

```http
POST /api/admin/mushaf/ayahs/{ayahId}/regions
```

Body :

```json
{
  "mushafPageId": 42,
  "x": 0.0823,
  "y": 0.4271,
  "width": 0.8125,
  "height": 0.0634,
  "sequence": 1
}
```

## Update

```http
PUT /api/admin/mushaf/ayah-regions/{id}
```

## Delete

```http
DELETE /api/admin/mushaf/ayah-regions/{id}
```

---

# 36. API AudioSegmentImageRegion

## Create

```http
POST /api/admin/audio-segments/{segmentId}/image-regions
```

## List

```http
GET /api/admin/audio-segments/{segmentId}/image-regions
```

## Delete

```http
DELETE /api/admin/audio-segment-image-regions/{id}
```

---

# 37. Validation

Règles :

```text
0 <= X <= 1
0 <= Y <= 1
0 < Width <= 1
0 < Height <= 1

X + Width <= 1
Y + Height <= 1
```

---

# 38. Audit

Ajouter les événements :

```text
MUSHAF_REGION_CREATED
MUSHAF_REGION_UPDATED
MUSHAF_REGION_DELETED
MUSHAF_REGION_VALIDATED
SEGMENT_IMAGE_REGION_CREATED
SEGMENT_IMAGE_REGION_UPDATED
SEGMENT_IMAGE_REGION_DELETED
```

---

# 39. Statut de validation graphique

Créer éventuellement :

```text
Draft
Reviewed
Approved
```

sur `AyahImageRegion`.

Ne pas afficher une région publique tant qu’elle n’est pas validée.

---

# 40. Performance

Les pages Mushaf peuvent être lourdes.

Prévoir :

- CDN ;
- WebP ;
- cache navigateur ;
- lazy loading ;
- préchargement page suivante ;
- dimensions fixes pour éviter le layout shift.

---

# 41. Cache

Headers recommandés :

```text
Cache-Control: public, max-age=31536000, immutable
```

pour les images versionnées.

---

# 42. URL versionnée

Exemple :

```text
/mushaf/warsh/v1/pages/042.webp
```

Ne pas modifier un fichier existant après publication.

Créer une nouvelle version si nécessaire.

---

# 43. Tests unitaires

Créer :

```text
AyahImageRegion_XMustBeNormalized
AyahImageRegion_YMustBeNormalized
AyahImageRegion_WidthMustBeValid
AyahImageRegion_HeightMustBeValid
AyahImageRegion_MustFitInsidePage
AudioSegmentImageRegion_MustBelongToSegmentAyah
```

---

# 44. Tests UI

Tester :

```text
ouvrir page Mushaf
zoom
pan
dessiner region
resize
move
delete
multi-region
save
reload
highlight
```

---

# 45. Critères d’acceptation

## AC-M01

Une page Warsh officielle s’affiche correctement.

## AC-M02

Le zoom ne dégrade pas la précision des régions.

## AC-M03

L’admin peut dessiner plusieurs régions pour une ayah.

## AC-M04

Les coordonnées sont persistées sous forme normalisée.

## AC-M05

Après reload, les régions se replacent exactement.

## AC-M06

L’ayah peut être surlignée dans la page complète.

## AC-M07

Un segment audio peut référencer seulement une portion graphique de l’ayah.

## AC-M08

Le site public peut afficher un extrait de l’ayah.

## AC-M09

Le site public peut ouvrir la page complète avec surbrillance.

## AC-M10

Le texte Unicode n’est pas requis pour le rendu graphique principal.

---

# 46. Adaptation du plan Antigravity précédent

Dans le plan audio précédent, modifier la priorité suivante.

Avant :

```text
AudioSegment
→ AyahWord
```

Après :

```text
AudioSegment
→ Ayah
→ MushafPage
→ AyahImageRegion
```

Pour les segments partiels :

```text
AudioSegment
→ AudioSegmentImageRegion
```

`AyahWord` reste disponible pour :

- recherche ;
- indexation ;
- métadonnées ;
- aide à l’administration.

Il n’est plus obligatoire pour l’affichage graphique.

---

# 47. Nouveau workflow administrateur

```text
Choisir sourate
    ↓
Ouvrir MP3
    ↓
Waveform
    ↓
Sélection audio
    ↓
Choisir Ayah
    ↓
Afficher page Mushaf Warsh
    ↓
Sélectionner portion graphique si besoin
    ↓
Choisir Wajh
    ↓
Save
    ↓
Review
    ↓
Approve
    ↓
Render FFmpeg
    ↓
Publish
```

---

# 48. Vue consolidée finale

```text
                     SOURCE AUDIO
                         │
                         ▼
                    Waveform
                         │
                  AudioSegment
                         │
          ┌──────────────┼───────────────┐
          │              │               │
        Ayah           Wajh          Time Range
          │
          ▼
       MushafPage
          │
          ▼
   AyahImageRegions
          │
          └── SegmentImageRegions si portion
                         │
                         ▼
                     Publish
                         │
            ┌────────────┴────────────┐
            │                         │
        Audio final              Image fidèle
```

---

# 49. Phases d’implémentation recommandées

## Phase M1 — MushafEdition

Créer entité + migration.

## Phase M2 — MushafPage

Créer import des pages.

## Phase M3 — Web image conversion

Créer WebP depuis masters.

## Phase M4 — Viewer admin

Afficher page + zoom + pan.

## Phase M5 — AyahImageRegion

Créer sélection graphique multi-régions.

## Phase M6 — Persistance

Sauvegarder coordonnées normalisées.

## Phase M7 — Validation

Ajouter Review / Approve.

## Phase M8 — Public Mushaf page

Créer :

```text
/mushaf/warsh/page/{page}
```

## Phase M9 — Public ayah crop

Créer affichage graphique sur :

```text
/quran/{surah}/{ayah}
```

## Phase M10 — Intégration AudioSegment

Associer `AudioSegmentImageRegion`.

## Phase M11 — Al-Fatiha pilote

Annoter toutes les ayat.

## Phase M12 — 20 premières ayat d’Al-Baqara

Valider multi-lines / partial ayah / zoom / performance.

## Phase M13 — Industrialisation

Étendre aux pages restantes.

---

# 50. Première livraison complémentaire attendue

Antigravity doit livrer :

```text
MushafEdition
MushafPage
import pages
master + WebP
viewer page
zoom
pan
AyahImageRegion
multi-regions
coordonnées normalisées
save
reload
highlight
```

Sans encore intégrer les segments audio partiels.

---

# 51. Deuxième livraison complémentaire

Ajouter :

```text
AudioSegmentImageRegion
annotation partielle
public ayah crop
public full page
highlight
audit
validation
```

---

# 52. Règles finales

1. L’image officielle du Mushaf Warsh constitue la référence visuelle.
2. Le texte Unicode reste une donnée logique et de recherche.
3. Ne pas reconstruire le Mushaf à partir d’une police arbitraire.
4. Les régions graphiques doivent être normalisées.
5. Une ayah peut avoir plusieurs régions.
6. Un segment audio peut avoir ses propres régions graphiques.
7. Les masters images sont immuables.
8. Les versions Web sont dérivées.
9. Les régions doivent être auditables.
10. Toute annotation publique doit être validée.

---

# 53. Sources

## Complexe du Roi Fahd

https://qurancomplex.gov.sa/

## Ressources techniques du Complexe

https://qurancomplex.gov.sa/en/techquran/dev/

Le Complexe met à disposition des ressources informatiques pour le texte coranique et des fontes compatibles avec le Mushaf, notamment pour Warsh.

## Outil de publication Mushaf

https://nashr.qurancomplex.gov.sa/

Ce service du Complexe référence notamment des fontes Mushaf pour Hafs et Warsh compatibles avec le Mushaf de Médine.

## Corpus audio du projet

https://archive.org/details/z0603_202406x

---

# 54. Décision d’architecture à conserver

> **Image = référence visuelle. Unicode = donnée informatique.**

Cette décision doit compléter le plan audio précédent et être appliquée à l’interface publique comme au back-office d’annotation.
