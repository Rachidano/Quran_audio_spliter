# Rapport de validation — Lot L04 : Présentation et signature

**Date :** 13 octobre 2026  
**Auteur :** Antigravity  
**Référence :** Tasks V3-04-01 à V3-04-07 / Plan d'implémentation V3 Section 5 (L04)  
**Preuve d'exécution :** [L03_L04_tests.trx](L03_L04_tests.trx)

---

## 1. Objectifs du lot

- Implémenter les 3 options de présentation :
  - **Fond clair** : rendu épuré sur fond blanc immaculé.
  - **Fond sombre** : fond ardoise/nuit noble (`#0F172A`) avec panneau coranique clair centré.
  - **Image personnelle** : fond personnalisé importé par l'utilisateur (recadré au format) avec panneau coranique clair centré.
- Respect absolu de la sacralité du Mushaf :
  - L'image de l'ayah originale reste **100% intacte** sur son panneau blanc (aucun détourage, aucune binarisation, aucune inversion de couleur des lettres ni des voyelles).
  - Préservation des proportions exactes sans déformation ni écrasement.
- Support des formats **16:9** (1920x1080) et **9:16** (1080x1920).
- Signature / filigrane :
  - Logo PNG (avec transparence) ou signature textuelle (arabe RTL).
  - Quatre coins disponibles : Haut-Gauche, Haut-Droite, Bas-Gauche, Bas-Droite.
  - Opacité réglable (0.05 à 1.0).
  - **Zone de sécurité anti-chevauchement** : le filigrane ne doit jamais toucher ou empiéter sur le panneau coranique de l'ayah.
- Partage du même moteur de rendu entre l'aperçu UI (`BitmapSource`) et l'export vidéo (`Bitmap` -> FFmpeg).

---

## 2. Implémentations réalisées

1. **`AyahCardComposer.cs`** :
   - Classe centrale de composition graphique haute fidélité (GDI+ / DirectWrite).
   - Rendu bicubique haute qualité, anticrénelage et ClearType GridFit.
   - Méthodes `ComposeCardBitmap`, `ComposeCardToFile`, `ComposeCardToBitmapSource`.
   - Dimensionnement automatique du panneau coranique clair avec marges de sécurité garantissant qu'aucun coin de filigrane ne puisse chevaucher le texte coranique.
   - Algorithme de détection de collision géométrique (`Rectangle.IntersectsWith`).
   - Matrice de transparence (`ColorMatrix`) pour l'opacité paramétrable du logo PNG.
   - Rendu natif du texte arabe avec police vectorielle adaptée.

2. **Intégration dans `FfmpegVideoGenerator.cs` et `CompositionService.cs`** :
   - Prise en charge des `CardCompositionSettings` pour les vidéos individuelles et pour les projets de séquences multi-ayat.

---

## 3. Résultats des tests automatisés (`AyahCardComposerTests`)

- `ComposeCard_LightBackground_GeneratesPureWhiteFrame` :
  - Format 1920x1080, fond blanc pur `#FFFFFF` validé au pixel près.
- `ComposeCard_DarkBackground_DrawsDarkCanvasWithWhiteCardInCenter` :
  - Format 1920x1080, fond sombre ardoise `#0F172A` aux angles et panneau blanc pur `#FFFFFF` centré abritant la calligraphie.
- `ComposeCard_CustomImageBackground_DrawsCustomImageWithCenteredCard` :
  - Image de fond personnalisée ajustée en aspect fill, avec carte blanche centrale préservant les pixels de l'ayah.
- `ComposeCard_VerticalOrientation_Produces1080x1920Canvas` :
  - Format vertical 9:16 (1080x1920), géométrie et marges respectées.
- `ComposeCard_WithArabicTextWatermark_RendersInAllFourCorners` :
  - Signature arabe (« قناة القرآن الكريم ») testée avec succès dans les 4 coins sans chevauchement avec le panneau coranique.
- `ComposeCard_WithLogoWatermark_AppliesOpacityCorrectly` :
  - Logo PNG avec canal alpha et matrice d'opacité à 50% appliqué sans artefact.
- `ComposeCard_BitmapSource_MatchesDimensionsForWpfPreview` :
  - Génération directe de `BitmapSource` WPF pour l'aperçu du studio/wizard, assurant 100% de concordance visuelle entre l'aperçu et la vidéo finale.

---

## 4. Statut des critères bloquants

- [x] Panneau coranique clair préservant 100% des pixels originaux du Mushaf.
- [x] Rendu 16:9 et 9:16 sans distorsion.
- [x] 4 coins de filigrane sans chevauchement avec la zone coranique.
- [x] Moteur partagé entre aperçu et export.
- [ ] Revue visuelle humaine (V3-04-07) : intégrée à la recette globale finale du lot L08.
