# Projet Web — Plateforme de récitation du Coran en رواية ورش عن نافع
## Plan d’implémentation et démarche de réalisation

**Objectif :** construire from scratch un site web permettant d’accéder rapidement à n’importe quelle ayah du Coran, de l’afficher, de l’écouter et de la télécharger en récitation **ورش عن نافع** par le cheikh **صابر عبد الحكم**, avec prise en charge des différents **أوجه** lorsqu’ils existent.

---

## 1. Constat de départ

Une source audio complète existe déjà sur Internet Archive :

- **Titre :** صابر عبد الحكم مصحف رواية ورش ب جميع الأوجه الشاطبية 114 سورة كامل 128 ك ب
- **Identifiant Archive.org :** `z0603_202406x`
- **URL :** https://archive.org/details/z0603_202406x
- **Contenu annoncé :** 114 sourates
- **Format disponible :** MP3
- **Volume global indiqué :** environ 2,8 Go
- **Date d’ajout Archive.org :** 3 juin 2024

La page Archive.org expose les 114 fichiers MP3, organisés actuellement **par sourate**. Exemple :

- سورة الفاتحة
- سورة البقرة
- سورة آل عمران
- etc.

**Conséquence importante :** le projet ne part donc pas de zéro côté audio. Le travail principal consiste à :

1. récupérer et inventorier les 114 fichiers ;
2. découper chaque sourate en ayat ;
3. gérer les différents أوجه lorsqu’ils existent ;
4. rattacher chaque extrait audio à la bonne ayah ;
5. valider humainement les découpages ;
6. publier le tout via une interface web simple et rapide.

> Avant toute republication ou mise à disposition en téléchargement, il faudra vérifier les droits/licences applicables aux enregistrements et, idéalement, obtenir une autorisation explicite du détenteur des droits ou du cheikh.

---

# 2. Architecture générale recommandée

Pour un projet neuf en 2026, une architecture simple et évolutive peut être :

```text
                        INTERNET
                           │
                    HTTPS / CDN
                           │
                     Nginx / Proxy
                           │
              ┌────────────┴────────────┐
              │                         │
       ASP.NET Core 9              Stockage Objet
       Web + REST API               Audio / images
              │                     (S3 compatible)
              │
         PostgreSQL 18
              │
       ┌──────┴──────┐
       │             │
   Site public   Back-office Admin
```

## Stack proposée

> **Décision technologique du projet :** le développement sera réalisé en **.NET 9**, avec **ASP.NET Core 9** et **Entity Framework Core 9**. Le projet restera volontairement sur cette version pour toute la durée de cette implémentation.

- **Backend / Web :** ASP.NET Core 9
- **Frontend :** Razor Pages ou MVC + JavaScript léger
- **Base de données :** PostgreSQL
- **ORM :** Entity Framework Core 9
- **Reverse proxy :** Nginx
- **Stockage audio :** Object Storage compatible S3
- **Conteneurisation :** Docker / Docker Compose
- **CI/CD :** GitHub Actions, GitLab CI ou Azure DevOps
- **OS serveur :** Ubuntu Server LTS
- **HTTPS :** Let's Encrypt

### Pourquoi cette approche ?

Pour une première version, il n’est pas nécessaire de séparer immédiatement le frontend et le backend en deux applications complexes.

Un projet ASP.NET Core 9 MVC ou Razor Pages permet :

- une mise en œuvre plus rapide ;
- une excellente compatibilité SEO ;
- une interface responsive ;
- une bonne gestion RTL/arabe ;
- une API REST ajoutée progressivement pour une future application mobile.

---

# 3. Principe fondamental du projet

Le projet doit séparer clairement trois couches de contenu :

```text
1. Référentiel coranique officiel
       ↓
2. Référentiel audio du cheikh
       ↓
3. Application Web
```

Le texte coranique ne doit jamais dépendre des fichiers MP3.

L’audio ne doit jamais contenir les informations fonctionnelles directement dans le code du site.

Toutes les associations doivent passer par la base de données.

---

# 4. Référentiel du Coran Warsh

Il est fortement recommandé d’utiliser une source officielle pour le texte du Coran en **رواية ورش عن نافع**.

Une référence adaptée est le **مجمع الملك فهد لطباعة المصحف الشريف**.

Source :

https://qurancomplex.gov.sa/en/techquran/dev/

Le Complexe fournit des ressources techniques destinées aux développeurs, notamment sous plusieurs formats :

- JSON
- CSV
- SQL
- XML
- TXT
- Unicode

Les données peuvent inclure notamment :

- numéro de sourate ;
- numéro d’ayah ;
- texte de l’ayah ;
- page du Mushaf ;
- ligne de début ;
- ligne de fin.

## Règle importante

Le texte officiel doit être conservé **tel quel** dans la base.

On peut cependant créer un second champ destiné uniquement à la recherche.

Exemple :

```text
ayah_text
    = texte officiel Warsh

search_text
    = version normalisée pour la recherche
```

Le champ `search_text` peut par exemple retirer certaines marques diacritiques afin de permettre une recherche plus souple.

---

# 5. Modèle de données recommandé

Il ne faut pas créer une simple table du type :

```text
Surah
Ayah
Mp3
```

Le modèle doit prévoir dès le départ plusieurs récitations ou plusieurs أوجه pour une même ayah.

## Tables principales

| Table | Fonction |
|---|---|
| `Surah` | Référentiel des 114 sourates |
| `Ayah` | Référentiel des ayat |
| `Reciter` | Lecteurs / cheikhs |
| `Riwaya` | رواية, ex. ورش عن نافع |
| `Wajh` | الوجه / variante disponible |
| `Recitation` | Association Ayah + lecteur + riwaya + wajh |
| `AudioAsset` | Fichier audio, durée, taille, codec, URL |
| `MushafPage` | Pages du Mushaf |
| `Source` | Provenance de l’enregistrement |
| `ImportJob` | Historique des imports |
| `Validation` | Validation humaine et publication |

---

# 6. Relation centrale

Le modèle principal peut être représenté ainsi :

```text
Ayah
 └── Recitation
      ├── Reciter = صابر عبد الحكم
      ├── Riwaya = ورش عن نافع
      ├── Wajh = ...
      └── AudioAsset
           └── URL MP3
```

Une même ayah peut donc avoir :

```text
2:255
 ├── الوجه 1 → audio-01.mp3
 ├── الوجه 2 → audio-02.mp3
 ├── الوجه 3 → audio-03.mp3
 └── ...
```

Cette structure permettra plus tard d’ajouter facilement :

- d’autres lecteurs ;
- d’autres riwayat ;
- d’autres طرق ;
- d’autres أوجه.

---

# 7. Étape 1 — Inventaire des 114 fichiers Archive.org

La première opération technique doit être de récupérer les métadonnées des 114 MP3 présents sur :

https://archive.org/details/z0603_202406x

Créer un inventaire contenant au minimum :

```text
surah_number
surah_name
source_file_name
source_url
source_size
checksum
duration
status
```

Exemple :

```text
001 | الفاتحة | ...سورة 001 الفاتحة.mp3
002 | البقرة   | ...سورة 002 البقرة.mp3
003 | آل عمران | ...سورة 003 آل عمران.mp3
```

## Objectif

À la fin de cette étape :

```text
114 sourates attendues
114 fichiers trouvés
0 fichier manquant
```

---

# 8. Étape 2 — Téléchargement et stockage des sources originales

Les fichiers originaux doivent être conservés sans modification.

Structure recommandée :

```text
/audio-source/
    /001/
        original.mp3
    /002/
        original.mp3
    /003/
        original.mp3
```

Ne jamais écraser les fichiers originaux.

Créer ensuite une zone de traitement distincte :

```text
/audio-working/
```

et une zone pour les fichiers publiés :

```text
/audio-published/
```

---

# 9. Étape 3 — Analyse audio

Pour chaque sourate :

1. lire le fichier audio ;
2. relever sa durée ;
3. identifier les limites des ayat ;
4. identifier les répétitions ;
5. identifier les différents أوجه ;
6. générer les segments candidats.

Outils possibles :

- FFmpeg
- Python
- librosa
- pydub
- outils de détection de silence
- outils de transcription / reconnaissance comme assistance

Cependant, **la reconnaissance automatique ne doit jamais être considérée comme validation finale**.

---

# 10. Étape 4 — Découpage audio

Le processus cible est :

```text
Fichier sourate complet
        │
        ▼
Analyse temporelle
        │
        ▼
Détection des limites
        │
        ▼
Découpage candidats
        │
        ▼
Association sourate / ayah
        │
        ▼
Identification des أوجه
        │
        ▼
Validation humaine
        │
        ▼
Publication
```

---

# 11. Convention de nommage

Il est préférable de ne pas utiliser directement les noms arabes comme identifiants techniques.

Exemple :

```text
001/001/warsh/w01.mp3
001/001/warsh/w02.mp3

002/255/warsh/w01.mp3
002/255/warsh/w02.mp3
```

Ou :

```text
002_255_warsh_w01.mp3
002_255_warsh_w02.mp3
```

Cette convention rend les fichiers faciles à :

- rechercher ;
- synchroniser ;
- valider ;
- sauvegarder ;
- distribuer via CDN.

---

# 12. Étape 5 — Back-office de validation

Le back-office doit être développé **avant ou en parallèle du site public**.

C’est un composant essentiel du projet.

## Écran conseillé

```text
Sourate : البقرة
Ayah : 255

Texte :
[ texte Warsh ]

Source audio :
[ player du fichier original ]

Segments détectés :

[▶] Segment 1   00:31:22 → 00:31:37
    الوجه : [ ... ]
    [Valider]

[▶] Segment 2   00:31:38 → 00:31:54
    الوجه : [ ... ]
    [Valider]

[▶] Segment 3   ...
```

## Statuts proposés

```text
IMPORTED
   ↓
SEGMENTED
   ↓
REVIEWED
   ↓
APPROVED
   ↓
PUBLISHED
```

Aucun fichier ne doit être accessible publiquement avant validation.

---

# 13. Étape 6 — Proof of Concept

Ne pas commencer immédiatement par les 114 sourates.

Créer d’abord un POC avec :

```text
سورة الفاتحة
+
20 premières ayat de سورة البقرة
```

Le POC doit valider :

- import du texte Warsh ;
- découpage audio ;
- plusieurs أوجه pour une même ayah ;
- player ;
- téléchargement ;
- recherche ;
- affichage arabe ;
- responsive mobile ;
- URLs permanentes ;
- back-office.

Quand ce POC fonctionne parfaitement, poursuivre avec toute سورة البقرة.

Après validation d’Al-Baqara, industrialiser le reste du Coran.

---

# 14. Étape 7 — Site public

## Page d’accueil

L’utilisateur doit pouvoir :

- choisir une sourate ;
- choisir une ayah ;
- rechercher par texte ;
- saisir directement une référence comme `2:255`.

Exemple :

```text
البقرة
↓
255
↓
ورش عن نافع
↓
الوجه
↓
▶ استماع
⬇ تحميل
```

---

# 15. Page d’une ayah

Exemple d’URL :

```text
/quran/2/255
```

La page affiche :

```text
سورة البقرة
الآية 255

[ texte Warsh ]

القارئ
صابر عبد الحكم

الرواية
ورش عن نافع

الأوجه المتوفرة

○ الوجه 1
○ الوجه 2
○ الوجه 3

[ ▶ تشغيل ]
[ ⬇ تحميل ]

← الآية السابقة
الآية التالية →
```

---

# 16. Page d’une sourate

Exemple :

```text
/surah/2
```

Affichage :

```text
سورة البقرة

آية 1
[texte]
▶  ⬇

آية 2
[texte]
▶  ⬇

آية 3
[texte]
▶  ⬇
```

---

# 17. Recherche

Pour le MVP, PostgreSQL suffit largement.

La recherche doit accepter :

## Par numéro

```text
2:255
```

## Par sourate

```text
البقرة
```

## Par mots de l’ayah

```text
الله لا إله إلا هو
```

## Par URL directe

```text
/quran/2/255
```

Il n’est pas nécessaire d’installer Elasticsearch dès la première version.

---

# 18. Images du Mushaf

Deux approches sont possibles.

## Option A — Texte Unicode Warsh

Afficher directement le texte via une police compatible avec le الرسم العثماني.

Avantages :

- responsive ;
- léger ;
- recherche possible ;
- sélection/copie possible.

## Option B — Image de la page du Mushaf

Conserver les pages originales du Mushaf et utiliser :

```text
page
line_start
line_end
```

pour positionner visuellement l’ayah.

Cette fonction peut être développée dans une deuxième phase.

---

# 19. Stockage des MP3

Ne pas stocker les MP3 dans PostgreSQL.

PostgreSQL contient uniquement :

```text
audio_id
storage_key
duration
size
checksum
codec
bitrate
```

Les fichiers réels sont stockés dans un Object Storage.

Solutions possibles :

- Amazon S3
- Cloudflare R2
- Backblaze B2
- Wasabi
- MinIO auto-hébergé

---

# 20. Pourquoi Object Storage + CDN ?

Architecture :

```text
Utilisateur
     │
     ├── pages → ASP.NET
     │
     └── audio → CDN/Object Storage
```

Cela évite que tous les MP3 transitent par le serveur ASP.NET.

Avantages :

- meilleures performances ;
- moins de bande passante serveur ;
- plus facile à faire évoluer ;
- meilleure disponibilité ;
- compatible future application mobile.

---

# 21. Organisation du projet .NET 9

Structure proposée :

```text
QuranWarsh/
│
├── src/
│   ├── Quran.Web
│   ├── Quran.Application
│   ├── Quran.Domain
│   ├── Quran.Infrastructure
│   ├── Quran.AudioProcessor
│   └── Quran.Import
│
├── tests/
│   ├── Quran.UnitTests
│   └── Quran.IntegrationTests
│
├── data/
│   ├── imports/
│   └── reference/
│
├── docker/
│
├── scripts/
│
└── docker-compose.yml
```

---

# 22. Rôle de chaque projet

## Quran.Domain

Contient :

- Surah
- Ayah
- Reciter
- Riwaya
- Wajh
- Recitation
- AudioAsset

## Quran.Application

Contient :

- use cases ;
- services ;
- interfaces ;
- logique applicative.

## Quran.Infrastructure

Contient :

- PostgreSQL ;
- EF Core ;
- Object Storage ;
- repositories.

## Quran.Web

Contient :

- site public ;
- API REST ;
- back-office.

## Quran.AudioProcessor

Contient :

- FFmpeg ;
- découpage ;
- normalisation ;
- génération des fichiers audio.

## Quran.Import

Contient :

- import du référentiel Warsh ;
- import Archive.org ;
- synchronisation des métadonnées.

---

# 23. Endpoints API futurs

Exemples :

```http
GET /api/surahs
GET /api/surahs/2
GET /api/surahs/2/ayahs

GET /api/ayahs/2/255

GET /api/ayahs/2/255/recitations

GET /api/search?q=الله

GET /api/audio/{id}
```

Cette API permettra ensuite de créer une application :

- Android ;
- iOS ;
- Flutter ;
- React Native ;
- .NET MAUI.

---

# 24. Contrôles automatiques indispensables

Créer une commande de validation globale :

```text
CheckCorpus
```

Elle doit contrôler :

```text
✓ 114 sourates présentes
✓ Toutes les ayat présentes
✓ Texte Warsh présent
✓ Audio publié présent
✓ Aucun fichier inaccessible
✓ Aucun checksum incorrect
✓ Aucun doublon
✓ Aucun numéro d’ayah invalide
✓ Tous les segments validés
✓ Tous les أوجه correctement référencés
```

Le dashboard peut afficher :

```text
Sourates                    114 / 114
Ayat référencées            XXXX / XXXX
Ayat avec audio             XXXX
Ayat validées               XXXX
Ayat sans audio             0
Fichiers invalides          0
Segments à vérifier         XX
```

---

# 25. Stratégie d’implémentation recommandée

## Phase 0 — Vérification juridique et documentaire

- vérifier la provenance des enregistrements ;
- vérifier les droits de republication ;
- identifier l’autorité qui validera les أوجه ;
- figer la source officielle du texte Warsh.

## Phase 1 — Socle

- Git ;
- solution .NET 9 ;
- Docker Compose ;
- PostgreSQL ;
- migrations EF Core ;
- configuration CI/CD.

## Phase 2 — Référentiel coranique

- importer les sourates ;
- importer les ayat ;
- importer le texte Warsh ;
- importer la pagination.

## Phase 3 — Import Archive.org

- récupérer l’inventaire ;
- récupérer les 114 MP3 ;
- calculer les checksums ;
- enregistrer les métadonnées.

## Phase 4 — Pipeline audio

- analyser ;
- segmenter ;
- encoder ;
- nommer ;
- indexer.

## Phase 5 — Back-office

- écouter ;
- corriger ;
- rattacher ;
- renseigner le وجه ;
- valider ;
- publier.

## Phase 6 — POC

- Al-Fatiha ;
- 20 premières ayat d’Al-Baqara.

## Phase 7 — Validation d’Al-Baqara

Utiliser toute la sourate comme test de charge fonctionnel.

## Phase 8 — Industrialisation

Traiter progressivement les 114 sourates.

## Phase 9 — Site public

- home ;
- navigation ;
- recherche ;
- page ayah ;
- page sourate ;
- player ;
- téléchargement.

## Phase 10 — Production

- HTTPS ;
- sauvegarde ;
- monitoring ;
- CDN ;
- cache ;
- logs ;
- sécurité.

---

# 26. Ordre de réalisation conseillé

```text
SOURCE WARSH OFFICIELLE
          +
ARCHIVE AUDIO DU CHEIKH
          │
          ▼
   INVENTAIRE COMPLET
          │
          ▼
    BASE DE DONNÉES
          │
          ▼
   PIPELINE DE DÉCOUPAGE
          │
          ▼
    BACK-OFFICE QA
          │
          ▼
         POC
          │
          ▼
  VALIDATION AL-BAQARA
          │
          ▼
 INDUSTRIALISATION 114
          │
          ▼
       SITE WEB
          │
          ▼
 OBJECT STORAGE + CDN
          │
          ▼
     MISE EN PRODUCTION
```

---

# 27. Ce qu’il ne faut pas faire

Éviter de :

- développer immédiatement les 114 sourates ;
- coder les أوجه directement dans le frontend ;
- stocker les MP3 dans PostgreSQL ;
- modifier manuellement le texte coranique ;
- faire confiance à une découpe automatique sans validation ;
- nommer les fichiers uniquement avec du texte arabe ;
- mélanger fichiers sources et fichiers publiés ;
- créer des milliers d’images d’ayat manuellement ;
- lancer directement React + microservices + Elasticsearch pour le MVP.

---

# 28. MVP recommandé

Le MVP peut se limiter à :

```text
✓ Warsh uniquement
✓ Cheikh صابر عبد الحكم
✓ Sourates + ayat
✓ plusieurs أوجه
✓ player audio
✓ téléchargement
✓ recherche
✓ page ayah
✓ page sourate
✓ back-office
✓ responsive mobile
```

À exclure du premier MVP :

```text
✗ comptes utilisateurs
✗ favoris
✗ playlists
✗ tafsir
✗ traductions
✗ application mobile
✗ notifications
```

Ces éléments pourront être ajoutés après stabilisation du corpus.

---

# 29. Évolutions futures

Une fois le socle stabilisé :

- ajout d’autres lecteurs ;
- ajout d’autres riwayat ;
- تفسير ;
- traductions ;
- favoris ;
- playlists ;
- répétition d’ayah ;
- mode حفظ ;
- lecture continue ;
- PWA ;
- applications mobiles ;
- API publique ;
- statistiques ;
- synchronisation multi-device.

---

# 30. Premier objectif concret

Le meilleur premier jalon serait :

> **Importer سورة الفاتحة depuis le référentiel Warsh officiel, extraire les enregistrements correspondants depuis l’audio du cheikh, gérer les différents أوجه, valider les segments, puis afficher les 7 ayat dans le site avec lecture et téléchargement.**

Critères de réussite :

```text
7 ayat affichées
7 ayat Warsh correctes
Audio correct
أوجه correctement différenciés
Navigation précédente/suivante
Lecture Web
Téléchargement
Validation back-office
Responsive
```

Une fois ce jalon validé, passer à :

> **سورة البقرة complète**

C’est seulement après validation d’Al-Baqara que le pipeline doit être appliqué à l’ensemble des 114 sourates.

---

# 31. Sources principales

## Internet Archive — corpus audio du cheikh

**صابر عبد الحكم مصحف رواية ورش ب جميع الأوجه الشاطبية 114 سورة كامل 128 ك ب**

https://archive.org/details/z0603_202406x

La page indique notamment :

- 114 fichiers MP3 disponibles ;
- un fichier par sourate ;
- environ 2,8 Go pour l’item ;
- récitation attribuée à صابر عبد الحكم ;
- رواية ورش ;
- جميع الأوجه الشاطبية.

## مجمع الملك فهد لطباعة المصحف الشريف

Ressources numériques et développeurs :

https://qurancomplex.gov.sa/en/techquran/dev/

Guide technique du Complexe :

https://qurancomplex.gov.sa/wp-content/uploads/isdarat/booklets/complextechguide.pdf

---

# 32. Conclusion

La disponibilité des 114 sourates sur Internet Archive simplifie fortement le démarrage du projet.

Le vrai chantier n’est donc pas l’acquisition du Coran audio mais la transformation de ce corpus :

```text
114 fichiers par sourate
             ↓
      découpage contrôlé
             ↓
  ayah + وجه + métadonnées
             ↓
       validation humaine
             ↓
      stockage structuré
             ↓
        publication Web
```

Le projet doit être construit autour de ce **pipeline de contenu**, car c’est lui qui garantira la qualité, la fiabilité et l’évolutivité du futur site.
