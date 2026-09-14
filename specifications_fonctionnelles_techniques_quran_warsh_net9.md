# Spécifications fonctionnelles et techniques
## Plateforme Web de récitation du Coran — رواية ورش عن نافع
### Cheikh : صابر عبد الحكم

**Version :** 1.0  
**Framework cible :** .NET 9  
**Backend Web :** ASP.NET Core 9  
**ORM :** Entity Framework Core 9  
**Base de données :** PostgreSQL  
**Architecture :** Application Web + API REST + Back-office + Stockage objet

---

# 1. Objet du document

Le présent document définit les **spécifications fonctionnelles et techniques** d’une plateforme Web dédiée à la récitation du Coran en **رواية ورش عن نافع**, par le cheikh **صابر عبد الحكم**.

L’objectif principal est de permettre à un utilisateur d’accéder rapidement à n’importe quelle ayah du Coran, de :

- l’afficher ;
- l’écouter ;
- sélectionner le **وجه** disponible ;
- télécharger l’enregistrement correspondant ;
- partager un lien direct vers l’ayah ;
- naviguer vers l’ayah précédente ou suivante ;
- rechercher une ayah par référence ou par texte.

Le site doit être conçu dès le départ de manière évolutive afin de permettre ultérieurement l’ajout :

- d’autres lecteurs ;
- d’autres riwayat ;
- d’autres أوجه ;
- de traductions ;
- de tafsir ;
- de playlists ;
- de favoris ;
- d’applications mobiles.

---

# 2. Sources de référence

## 2.1 Source audio

Une source audio complète est disponible sur Internet Archive :

**Titre :**  
صابر عبد الحكم مصحف رواية ورش ب جميع الأوجه الشاطبية 114 سورة كامل 128 ك ب

**URL :**  
https://archive.org/details/z0603_202406x

Cette source annonce :

- 114 sourates ;
- un corpus complet ;
- une récitation attribuée au cheikh صابر عبد الحكم ;
- رواية ورش ;
- plusieurs أوجه ;
- des fichiers MP3 par sourate.

> Toute republication ou mise à disposition en téléchargement devra respecter les droits applicables au contenu source.

## 2.2 Référentiel textuel Warsh

Pour le texte coranique, la source de référence recommandée est :

**مجمع الملك فهد لطباعة المصحف الشريف**

Ressources développeurs :

https://qurancomplex.gov.sa/en/techquran/dev/

Cette source fournit des données techniques adaptées à une intégration logicielle.

---

# 3. Périmètre fonctionnel

## 3.1 Fonctionnalités incluses dans le MVP

Le MVP doit inclure :

- affichage des 114 sourates ;
- affichage des ayat d’une sourate ;
- accès direct à une ayah ;
- texte Warsh ;
- recherche par sourate ;
- recherche par numéro d’ayah ;
- recherche par référence `sourate:ayah` ;
- recherche textuelle simple ;
- lecture audio ;
- téléchargement audio ;
- gestion de plusieurs أوجه pour une même ayah ;
- navigation précédente / suivante ;
- URL permanente par ayah ;
- interface responsive ;
- support RTL ;
- back-office de validation ;
- gestion des sources audio ;
- gestion des statuts de publication.

## 3.2 Fonctionnalités hors MVP

Ne sont pas prioritaires pour la V1 :

- comptes utilisateurs ;
- favoris ;
- playlists ;
- tafsir ;
- traductions ;
- commentaires ;
- notifications ;
- réseau social ;
- paiement ;
- application mobile native ;
- recommandations personnalisées.

---

# 4. Acteurs

## 4.1 Visiteur

Utilisateur anonyme du site.

Il peut :

- consulter les sourates ;
- consulter les ayat ;
- rechercher une ayah ;
- écouter un enregistrement ;
- télécharger un fichier audio ;
- partager un lien ;
- naviguer entre les ayat.

## 4.2 Administrateur

Il peut :

- gérer les référentiels ;
- importer les contenus ;
- consulter les sources ;
- associer les fichiers audio aux ayat ;
- gérer les أوجه ;
- valider les segments ;
- publier ou dépublier un contenu ;
- consulter les anomalies ;
- relancer un traitement.

## 4.3 Validateur de contenu

Rôle distinct facultatif.

Il peut :

- écouter les segments ;
- vérifier les associations ;
- identifier le وجه ;
- approuver ou rejeter un segment ;
- ajouter un commentaire de validation.

---

# 5. Parcours utilisateur principal

Exemple :

```text
Accueil
  ↓
Choix de la sourate
  ↓
Choix de l’ayah
  ↓
Affichage du texte
  ↓
Choix du وجه
  ↓
Lecture ou téléchargement
```

Exemple concret :

```text
سورة البقرة
  ↓
الآية 255
  ↓
ورش عن نافع
  ↓
الوجه 1
  ↓
▶ استماع
⬇ تحميل
```

---

# 6. Page d’accueil

La page d’accueil doit afficher :

- nom du site ;
- recherche principale ;
- accès aux sourates ;
- champ ou contrôle de recherche rapide ;
- accès direct par référence ;
- navigation simple adaptée au mobile.

## 6.1 Recherche rapide

Le champ doit accepter :

```text
البقرة
```

ou :

```text
2:255
```

ou :

```text
الله لا إله إلا هو
```

---

# 7. Page d’une sourate

URL cible :

```text
/surah/{surahNumber}
```

Exemple :

```text
/surah/2
```

La page doit afficher :

- nom arabe ;
- numéro de sourate ;
- nombre d’ayat ;
- liste des ayat ;
- texte de chaque ayah ;
- indicateur de disponibilité audio ;
- bouton d’écoute ;
- bouton de téléchargement ;
- lien vers la page détaillée.

---

# 8. Page d’une ayah

URL cible :

```text
/quran/{surahNumber}/{ayahNumber}
```

Exemple :

```text
/quran/2/255
```

La page doit afficher :

- nom de la sourate ;
- numéro de l’ayah ;
- texte Warsh ;
- lecteur ;
- riwaya ;
- liste des أوجه disponibles ;
- lecteur audio ;
- bouton téléchargement ;
- navigation précédente ;
- navigation suivante ;
- bouton partage.

---

# 9. Gestion des أوجه

Une même ayah peut contenir plusieurs récitations.

Règle :

```text
1 Ayah
    ↓
N Recitations
```

Chaque récitation doit être liée à :

- une ayah ;
- un lecteur ;
- une riwaya ;
- un وجه ;
- un fichier audio.

Exemple :

```text
Ayah 2:255
 ├── Wajh 1
 ├── Wajh 2
 └── Wajh 3
```

---

# 10. Gestion de l’audio

## 10.1 Fichier source

Les fichiers originaux sont conservés séparément.

Exemple :

```text
/audio-source/002/original.mp3
```

## 10.2 Fichiers publiés

Exemple :

```text
/audio-published/002/255/warsh/w01.mp3
/audio-published/002/255/warsh/w02.mp3
```

## 10.3 Métadonnées audio

Pour chaque fichier :

- durée ;
- taille ;
- codec ;
- bitrate ;
- checksum ;
- chemin de stockage ;
- URL publique ;
- source ;
- statut de validation.

---

# 11. Pipeline fonctionnel de traitement audio

```text
Import source
   ↓
Inventaire
   ↓
Analyse
   ↓
Découpage
   ↓
Association Ayah
   ↓
Identification Wajh
   ↓
Validation humaine
   ↓
Publication
```

---

# 12. Statuts de traitement

Les contenus doivent suivre les statuts suivants :

```text
IMPORTED
SEGMENTED
REVIEWED
APPROVED
PUBLISHED
REJECTED
```

## Règle de gestion

Un enregistrement ne peut être diffusé publiquement que si son statut est :

```text
PUBLISHED
```

---

# 13. Back-office

Le back-office doit contenir les modules suivants :

## 13.1 Dashboard

Afficher :

- nombre de sourates ;
- nombre d’ayat ;
- ayat avec audio ;
- ayat sans audio ;
- segments à valider ;
- contenus publiés ;
- erreurs ;
- doublons ;
- fichiers manquants.

## 13.2 Gestion des sourates

Actions :

- consulter ;
- modifier les métadonnées non coraniques ;
- vérifier le statut d’import.

## 13.3 Gestion des ayat

Actions :

- consulter ;
- rechercher ;
- visualiser le texte ;
- consulter les fichiers liés ;
- publier / dépublier.

## 13.4 Validation audio

L’écran doit permettre :

- écoute du fichier source ;
- écoute du segment ;
- affichage du texte de l’ayah ;
- sélection du وجه ;
- validation ;
- rejet ;
- commentaire.

---

# 14. Règles de gestion

## RG-01

Une ayah appartient à une seule sourate.

## RG-02

Une ayah peut avoir plusieurs récitations.

## RG-03

Une récitation appartient à un seul lecteur.

## RG-04

Une récitation appartient à une seule riwaya.

## RG-05

Une récitation peut être liée à un وجه.

## RG-06

Un fichier audio ne peut être publié sans validation.

## RG-07

Le texte Warsh officiel ne doit pas être modifié par l’administration.

## RG-08

Le texte de recherche normalisé doit être distinct du texte affiché.

## RG-09

Les fichiers sources originaux ne doivent jamais être écrasés.

## RG-10

Une URL d’ayah doit rester stable.

---

# 15. Architecture technique cible

```text
Client Web
   │
   ▼
Nginx
   │
   ▼
ASP.NET Core 9
   │
   ├── MVC / Razor Pages
   ├── REST API
   ├── Back-office
   └── Services applicatifs
   │
   ▼
Entity Framework Core 9
   │
   ▼
PostgreSQL
   │
   ├── Métadonnées
   ├── Référentiels
   └── Relations
```

En parallèle :

```text
ASP.NET Core 9
   │
   ▼
Object Storage
   │
   ├── Sources
   └── Audio publié
```

---

# 16. Stack technique

| Composant | Technologie |
|---|---|
| Framework | .NET 9 |
| Backend | ASP.NET Core 9 |
| ORM | Entity Framework Core 9 |
| Frontend | Razor Pages ou MVC |
| Base de données | PostgreSQL |
| Reverse proxy | Nginx |
| Audio | FFmpeg |
| Stockage | S3 compatible |
| Conteneurs | Docker |
| Orchestration locale | Docker Compose |
| Auth back-office | ASP.NET Core Identity |
| Logs | Serilog |
| Documentation API | OpenAPI / Swagger |
| Tests | xUnit |
| Mapping | AutoMapper ou mapping manuel |
| Validation | FluentValidation ou validation native |
| CI/CD | Azure DevOps / GitHub Actions / GitLab CI |

---

# 17. Structure de la solution

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
├── scripts/
└── docker-compose.yml
```

---

# 18. Description des projets

## Quran.Domain

Contient :

- entités ;
- enums ;
- règles métier ;
- objets de valeur.

## Quran.Application

Contient :

- services ;
- cas d’usage ;
- interfaces ;
- DTO ;
- validation.

## Quran.Infrastructure

Contient :

- EF Core ;
- PostgreSQL ;
- repositories ;
- stockage objet ;
- services externes.

## Quran.Web

Contient :

- site public ;
- back-office ;
- API REST ;
- sécurité ;
- endpoints.

## Quran.AudioProcessor

Contient :

- FFmpeg ;
- découpage ;
- conversion ;
- checksum ;
- traitement audio.

## Quran.Import

Contient :

- import Warsh ;
- import Archive.org ;
- import métadonnées ;
- synchronisation.

---

# 19. Modèle de données

## 19.1 Table Surah

Champs :

```text
Id
Number
NameArabic
NameLatin
AyahCount
Order
IsActive
```

## 19.2 Table Ayah

```text
Id
SurahId
AyahNumber
TextWarsh
SearchText
PageNumber
LineStart
LineEnd
```

Contraintes :

```text
UNIQUE (SurahId, AyahNumber)
```

## 19.3 Table Reciter

```text
Id
NameArabic
NameLatin
Description
IsActive
```

## 19.4 Table Riwaya

```text
Id
NameArabic
NameLatin
Description
```

## 19.5 Table Wajh

```text
Id
Code
NameArabic
Description
SortOrder
```

## 19.6 Table Recitation

```text
Id
AyahId
ReciterId
RiwayaId
WajhId
AudioAssetId
Status
IsPublished
CreatedAt
UpdatedAt
```

## 19.7 Table AudioAsset

```text
Id
StorageKey
OriginalFileName
MimeType
DurationMs
SizeBytes
Bitrate
Checksum
SourceId
CreatedAt
```

## 19.8 Table Source

```text
Id
Name
Url
Type
Description
```

## 19.9 Table Validation

```text
Id
RecitationId
ValidatorUserId
Status
Comment
ValidatedAt
```

---

# 20. Indexation base de données

Prévoir au minimum :

```text
INDEX Ayah(SurahId, AyahNumber)
INDEX Ayah(SearchText)
INDEX Recitation(AyahId)
INDEX Recitation(Status)
INDEX Recitation(IsPublished)
INDEX AudioAsset(Checksum)
```

---

# 21. API REST

## 21.1 Sourates

```http
GET /api/surahs
GET /api/surahs/{number}
```

## 21.2 Ayat

```http
GET /api/surahs/{surah}/ayahs
GET /api/ayahs/{surah}/{ayah}
```

## 21.3 Récitations

```http
GET /api/ayahs/{surah}/{ayah}/recitations
```

## 21.4 Recherche

```http
GET /api/search?q={text}
```

## 21.5 Administration

```http
POST   /api/admin/imports
GET    /api/admin/imports/{id}
POST   /api/admin/recitations/{id}/approve
POST   /api/admin/recitations/{id}/reject
POST   /api/admin/recitations/{id}/publish
DELETE /api/admin/recitations/{id}/publish
```

---

# 22. Exemple de réponse API

```json
{
  "surah": 2,
  "ayah": 255,
  "text": "...",
  "reciter": "صابر عبد الحكم",
  "riwaya": "ورش عن نافع",
  "recitations": [
    {
      "wajh": "w01",
      "audioUrl": "https://cdn.example.com/002/255/warsh/w01.mp3"
    }
  ]
}
```

---

# 23. Recherche

La recherche doit prendre en charge :

- nom de sourate ;
- numéro de sourate ;
- numéro d’ayah ;
- format `2:255` ;
- texte arabe.

## Normalisation

Le moteur de recherche peut générer un champ technique :

```text
SearchText
```

sans modifier :

```text
TextWarsh
```

---

# 24. Stockage objet

Les fichiers audio doivent être stockés hors PostgreSQL.

Structure :

```text
sources/
  001/
  002/

published/
  001/
    001/
      warsh/
        w01.mp3
```

Le stockage doit être compatible S3.

Solutions possibles :

- AWS S3 ;
- Cloudflare R2 ;
- Wasabi ;
- Backblaze B2 ;
- MinIO.

---

# 25. Sécurité

## 25.1 Site public

- HTTPS obligatoire ;
- aucune donnée sensible ;
- protection anti-abus ;
- limitation de débit API ;
- headers sécurité.

## 25.2 Back-office

- authentification obligatoire ;
- rôles ;
- mot de passe fort ;
- verrouillage après tentatives répétées ;
- journalisation ;
- protection CSRF ;
- validation serveur.

## 25.3 API

- endpoints publics en lecture ;
- endpoints admin protégés ;
- JWT éventuel pour applications externes.

---

# 26. Journalisation

Utiliser une journalisation structurée.

Exemples d’événements :

```text
IMPORT_STARTED
IMPORT_COMPLETED
AUDIO_SEGMENT_CREATED
AUDIO_VALIDATED
AUDIO_REJECTED
CONTENT_PUBLISHED
CONTENT_UNPUBLISHED
FILE_NOT_FOUND
CHECKSUM_MISMATCH
```

---

# 27. Gestion des erreurs

Le système doit gérer :

- fichier source absent ;
- fichier inaccessible ;
- erreur FFmpeg ;
- checksum incorrect ;
- doublon ;
- association invalide ;
- ayah inexistante ;
- Wajh manquant ;
- erreur de stockage ;
- erreur base de données.

---

# 28. Performance

Objectifs MVP :

- page d’accueil : < 2 s ;
- page ayah : < 2 s hors audio ;
- API simple : < 500 ms ;
- recherche : < 1 s ;
- streaming audio via CDN ;
- pagination back-office.

---

# 29. Cache

Prévoir :

- cache sourates ;
- cache métadonnées ;
- cache pages publiques ;
- CDN pour l’audio.

Redis n’est pas obligatoire pour la V1.

---

# 30. Responsive design

Le site doit fonctionner correctement sur :

- smartphone ;
- tablette ;
- desktop.

Le mobile doit être prioritaire.

---

# 31. RTL et langue arabe

L’interface arabe doit utiliser :

```html
<html lang="ar" dir="rtl">
```

Les composants doivent être compatibles RTL.

---

# 32. Accessibilité

Prévoir :

- boutons avec labels ;
- navigation clavier ;
- contraste correct ;
- texte lisible ;
- lecteur audio accessible ;
- taille de police configurable si possible.

---

# 33. SEO

Chaque ayah doit posséder une URL stable.

Exemple :

```text
/quran/2/255
```

Balises :

```text
title
description
canonical
OpenGraph
```

---

# 34. Tests unitaires

Couvrir notamment :

- récupération sourate ;
- récupération ayah ;
- validation référence ;
- publication ;
- règles de statut ;
- normalisation recherche.

---

# 35. Tests d’intégration

Tester :

- PostgreSQL ;
- EF Core ;
- API ;
- stockage ;
- import ;
- publication.

---

# 36. Tests fonctionnels

Scénarios minimum :

## TF-01

Chercher :

```text
2:255
```

Résultat attendu :

- page ayah correcte.

## TF-02

Cliquer sur lecture.

Résultat :

- audio correct.

## TF-03

Changer de وجه.

Résultat :

- autre audio chargé.

## TF-04

Télécharger.

Résultat :

- fichier correct.

## TF-05

Naviguer vers l’ayah suivante.

Résultat :

- nouvelle ayah affichée.

---

# 37. Critères d’acceptation MVP

Le MVP est accepté si :

- les 114 sourates sont présentes ;
- le texte Warsh est correctement importé ;
- le POC audio est validé ;
- Al-Fatiha est entièrement exploitable ;
- une partie d’Al-Baqara est validée ;
- l’utilisateur peut rechercher ;
- l’utilisateur peut écouter ;
- l’utilisateur peut télécharger ;
- plusieurs أوجه sont gérés ;
- le back-office permet validation et publication ;
- le site est responsive ;
- les URLs sont stables.

---

# 38. Contrôles de cohérence

Créer une commande technique :

```text
CheckCorpus
```

Elle vérifie :

```text
114 sourates
Ayat attendues
Ayat sans texte
Ayat sans audio
Audio sans ayah
Audio sans wajh
Fichiers inexistants
Fichiers en doublon
Checksum incorrect
Contenus non validés
Contenus publiés sans validation
```

---

# 39. Déploiement

En production :

```text
Internet
  ↓
Nginx
  ↓
ASP.NET Core 9
  ↓
PostgreSQL
```

Audio :

```text
Client
  ↓
CDN
  ↓
Object Storage
```

---

# 40. Docker Compose

Le `docker-compose.yml` devra contenir au minimum :

```text
quran-web
postgres
minio       # si stockage local S3
```

Optionnel :

```text
redis
pgadmin
```

---

# 41. Sauvegarde

Sauvegarder :

- PostgreSQL ;
- fichiers de configuration ;
- métadonnées ;
- stockage audio ;
- fichiers originaux.

Prévoir :

- sauvegarde quotidienne DB ;
- contrôle de restauration ;
- copie hors serveur.

---

# 42. Monitoring

Superviser :

- disponibilité Web ;
- erreurs HTTP ;
- CPU ;
- RAM ;
- disque ;
- PostgreSQL ;
- stockage ;
- taux d’erreurs ;
- temps de réponse.

---

# 43. Environnements

Prévoir :

```text
Development
Testing
Production
```

Éventuellement :

```text
Staging
```

---

# 44. Configuration

Les secrets ne doivent pas être stockés dans Git.

Utiliser :

```text
appsettings.json
appsettings.Production.json
variables d’environnement
Secret Manager
```

---

# 45. Convention de nommage

Audio :

```text
{surah}/{ayah}/{riwaya}/{wajh}.mp3
```

Exemple :

```text
002/255/warsh/w01.mp3
```

---

# 46. Convention Git

Branches proposées :

```text
main
develop
feature/*
fix/*
```

---

# 47. Première itération recommandée

## Sprint / lot 1

Objectif :

```text
سورة الفاتحة
```

Livrables :

- projet .NET 9 ;
- PostgreSQL ;
- référentiel Warsh ;
- modèle de données ;
- import Fatiha ;
- audio ;
- gestion des أوجه ;
- page sourate ;
- page ayah ;
- player ;
- téléchargement ;
- back-office minimum.

---

# 48. Deuxième itération

Objectif :

```text
20 premières ayat de سورة البقرة
```

Valider :

- performance ;
- découpage ;
- qualité audio ;
- workflow ;
- recherche ;
- administration.

---

# 49. Troisième itération

Objectif :

```text
سورة البقرة كاملة
```

Cette étape doit valider définitivement le modèle avant industrialisation.

---

# 50. Industrialisation

Après validation d’Al-Baqara :

```text
114 sourates
      ↓
Découpage
      ↓
Validation
      ↓
Publication progressive
```

---

# 51. Évolutions futures

Le système pourra évoluer vers :

- PWA ;
- application Android ;
- application iOS ;
- React Native ;
- Flutter ;
- .NET MAUI ;
- autres lecteurs ;
- autres riwayat ;
- tafsir ;
- traduction ;
- playlists ;
- compte utilisateur ;
- favoris ;
- répétition mémorisation ;
- API publique.

---

# 52. Exigences non fonctionnelles

## Disponibilité

Objectif initial :

```text
99,5 %
```

## Sécurité

- HTTPS ;
- authentification admin ;
- logs ;
- contrôle d’accès.

## Maintenabilité

- Clean Architecture légère ;
- code documenté ;
- séparation domaine / application / infrastructure.

## Évolutivité

Le modèle doit permettre plusieurs lecteurs et plusieurs riwayat.

## Portabilité

Le système doit fonctionner sous Linux via Docker.

---

# 53. Principes d’architecture

Le projet doit respecter les principes suivants :

```text
Simplicité
Séparation des responsabilités
Référentiel fiable
Validation humaine
Traçabilité
API-first raisonnable
Pas de sur-architecture
```

---

# 54. Décision technologique

Le projet sera réalisé avec :

```text
.NET 9
ASP.NET Core 9
Entity Framework Core 9
PostgreSQL
Docker
Nginx
FFmpeg
Object Storage S3-compatible
```

Le choix de .NET 9 est une décision de projet et doit être conservé pour cette implémentation.

---

# 55. Conclusion

La plateforme doit être pensée comme un système composé de trois briques principales :

```text
Référentiel Warsh
        +
Corpus audio
        +
Application Web
```

Le point critique n’est pas uniquement le développement du site, mais la qualité du pipeline :

```text
source
  ↓
découpage
  ↓
association
  ↓
validation
  ↓
publication
```

La réussite du projet dépend donc autant de la qualité du **back-office de validation** et du **référentiel de données** que du développement de l’interface publique.

---

# 56. Sources

## Internet Archive

https://archive.org/details/z0603_202406x

## مجمع الملك فهد لطباعة المصحف الشريف

https://qurancomplex.gov.sa/en/techquran/dev/

