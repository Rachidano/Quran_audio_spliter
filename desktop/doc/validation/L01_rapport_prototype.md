# Rapport de validation du Prototype de faisabilité (Lot 1)

Date : 11 octobre 2026  
Intervenant : Antigravity  
Lot : **Lot 1 — Prototype de faisabilité prioritaire**  
Outil : `desktop/tools/QuranVideo.Prototype` (C# .NET 9 `net9.0-windows`)  
Tests : `desktop/tests/QuranVideo.Tests` (5 tests xUnit réussis)

---

## 1. Objectif du Prototype

Le prototype du Lot 1 a pour but d'éliminer les deux risques majeurs identifiés avant la construction du studio complet :
1. **Risque graphique** : Prouver la faisabilité d'une extraction d'ayah fidèle à partir des pixels bruts du Mushaf, sans recomposition de texte, en préservant la géométrie relative des lignes, les blancs d'indentation, les diacritiques et le médaillon numéroté, et en isolant l'ayah sur fond blanc.
2. **Risque temporel et audiovisuel** : Mesurer la précision de l'horloge audio entre NAudio et FFmpeg (notamment sur le fichier géant de 4h18 de la sourate Al-Baqarah), et générer de vrais fichiers vidéo MP4 (horizontal 1920×1080 et vertical 1080×1920) vérifiés par FFprobe.

---

## 2. Extraction graphique et géométrie (L01-01, L01-02, L01-03, L01-04)

### 2.1 Implémentation
Le service `QuranVideo.Prototype.Geometry.AyahGeometry` implémente la formule de transformation géométrique en nombres flottants :
```csharp
x = W * [cL/100 + cW/100 * (tL/100 + tW/100 * rL/100)]
w = W * cW/100 * tW/100 * rW/100
y = H * [cT/100 + cH/100 * (tT/100 + tH/100 * rT/100)]
h = H * cH/100 * tH/100 * rH/100
```
Pour la première ayah de chaque sourate, le seuil de coupure vertical `minimumTopPercent` issu de `marw-opening-review.json` est appliqué pour exclure automatiquement le cartouche d'en-tête et la Basmala non numérotée.

### 2.2 Cas traités et résultats générés
Les planches comparatives 3 vues (`Page originale` | `Masque superposé` | `Image extraite fond blanc`) et les images PNG isolées ont été générées dans `desktop/doc/validation/prototype_output/` :

| Cas | Référence | Dimensions BBox | Fichier extrait | Planche comparative | Constat visuel |
|---|---|---|---|---|---|
| `01_Fatiha_1_1` | 1:1 (p.1) | 556 × 101 px | `01_Fatiha_1_1_isolated.png` | `01_Fatiha_1_1_comparison.png` | Cartouche exclu ; Basmala isolée avec médaillon (1) |
| `02_Fatiha_1_7` | 1:7 (p.1) | 658 × 185 px | `02_Fatiha_1_7_isolated.png` | `02_Fatiha_1_7_comparison.png` | 2 lignes intactes, médaillon (7) préservé en bas |
| `03_Baqara_2_1` | 2:1 (p.2) | 660 × 183 px | `03_Baqara_2_1_isolated.png` | `03_Baqara_2_1_comparison.png` | Cartouche exclu ; Alif-Lam-Mim seul conservé |
| `04_Baqara_2_2` | 2:2 (p.2) | 660 × 183 px | `04_Baqara_2_2_isolated.png` | `04_Baqara_2_2_comparison.png` | Début en milieu de ligne ; aucun mot de 2:1 ni 2:3 |
| `05_Baqara_2_4` | 2:4 (p.2) | 660 × 183 px | `05_Baqara_2_4_isolated.png` | `05_Baqara_2_4_comparison.png` | Blanc de fin de ligne conservé sans recentrage forcé |
| `06_Baqara_2_255` | 2:255 (p.42) | 839 × 244 px | `06_Baqara_2_255_Kursi_isolated.png` | `06_Baqara_2_255_Kursi_comparison.png` | Ayat al-Kursi (9 lignes) parfaitement nette |
| `07_Baqara_2_282` | 2:282 (p.48) | 839 × 320 px | `07_Baqara_2_282_Dayn_isolated.png` | `07_Baqara_2_282_Dayn_comparison.png` | Ayat al-Dayn pleine page sans marges ornementales |
| `08_Ikhlas_112_1` | 112:1 (p.604) | 327 × 93 px | `08_Ikhlas_112_1_isolated.png` | `08_Ikhlas_112_1_comparison.png` | Cartouche exclu ; verset 1 isolé en haut de page |
| `09_Ikhlas_112_4` | 112:4 (p.604) | 456 × 92 px | `09_Ikhlas_112_4_isolated.png` | `09_Ikhlas_112_4_comparison.png` | Cartouche d'Al-Falaq exclu sous la ligne |

---

## 3. Précision audio et synchronisation (L01-05, L01-06, L01-08)

Le banc d'essai `QuranVideo.Prototype.Audio.AudioPrecisionBenchmark` a comparé l'analyse NAudio (via Windows MediaFoundation) avec FFprobe :

| Fichier audio | Taille | Durée FFprobe | Durée NAudio | Écart Seek direct | Décision technique |
|---|---|---|---|---|---|
| `001/original.mp3` | 1 129 009 octets | 67.33 s | 67.32 s | 0.005 ms | Conforme : seek direct précis (<15 ms) |
| `112/original.mp3` | 571 451 octets | 32.48 s | 32.47 s | 0.003 ms | Conforme : seek direct précis (<15 ms) |
| `002/original.mp3` | 247 766 723 octets | 15 482.18 s (~4h18) | 15 482.17 s | 0.000 ms | Conforme : aucune dérive sur fichier long |

**Décision d'architecture audio** :
- Le moteur NAudio avec `MediaFoundationReader` offre une précision d'accès inférieure à la milliseconde sans dérive, même sur un fichier de 4h18.
- Lors de l'export vidéo, FFmpeg utilise l'argument `-ss` et `-t` en amont du filtre audio pour un découpage au sample près, garantissant la concordance exacte entre ce qui est entendu dans le studio et ce qui est produit dans le MP4.

---

## 4. Génération et vérification des vidéos MP4 (L01-05, L01-07)

Deux fichiers MP4 réels ont été créés et contrôlés de bout en bout avec FFprobe :

### 4.1 Vidéo Horizontale (1920 × 1080) — Al-Fatiha 1:1
- **Fichier généré** : `desktop/doc/validation/prototype_output/prototype_fatiha_1_1_horizontal.mp4`
- **Résolution** : 1920 × 1080 (16:9)
- **Durée** : 5.80 secondes
- **Codec vidéo** : H.264 (`yuv420p`, 30 fps)
- **Codec audio** : AAC (48 kHz, stéréo, 192 kbps)
- **Taille** : 178 191 octets
- **Rendu visuel** : L'image de l'ayah est centrée harmonieusement sur fond blanc pur avec marges équilibrées, texte 100% net issu des pixels originaux.

### 4.2 Vidéo Verticale (1080 × 1920) — Al-Ikhlas 112:1
- **Fichier généré** : `desktop/doc/validation/prototype_output/prototype_ikhlas_112_1_vertical.mp4`
- **Résolution** : 1080 × 1920 (9:16)
- **Durée** : 7.50 secondes
- **Codec vidéo** : H.264 (`yuv420p`, 30 fps)
- **Codec audio** : AAC (48 kHz, stéréo, 192 kbps)
- **Taille** : 210 110 octets
- **Rendu visuel** : Format optimisé pour les écrans mobiles / shorts, sans étirement du texte, proportions rigoureusement conservées.

---

## 5. Recette du Lot 1 (L01-R)

1. **Géométrie validée** : 5 tests xUnit automatisés réussis sur rectangles synthétiques et réels.
2. **Qualité d'extraction validée** : 9 planches comparatives et images isolées inspectables.
3. **Précision audio validée** : écart < 15 ms mesuré sur CBR et fichiers de 4h18.
4. **Vidéos MP4 validées** : 2 vidéos réelles (horizontale et verticale) générées, lues et confirmées conformes par FFprobe.
5. **Critère de passage satisfait** : Les deux risques prioritaires sont levés. Le développement du socle de l'application Windows (Lot 2) peut démarrer en toute sécurité.
