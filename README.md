# القرآن الكريم — رواية ورش عن نافع (منصة التلاوة والمصحف المصور)
## Quran Warsh Web Platform (.NET 9)

Plateforme web de référence pour la récitation du Saint Coran selon la رواية ورش عن نافع (récitation du Cheikh Saber Abdelhakam), intégrant la synchronisation audio-visuelle fidèle sur le مصحف الوزارة الجزائرية (MARW, révision `20260913-full`, 604 pages, 6 214 ayat), la gestion documentaire des أوجه (Al-Azraq et Al-Asbahani), un studio de découpage audio assisté, et un circuit éditorial complet (Draft → Reviewed → Approved → Rendered → Published).

---

## 🏛️ Architecture de la Solution

La solution `QuranWarsh.sln` est développée en **.NET 9 (C# 13)** selon les principes de la *Clean Architecture* et du *Domain-Driven Design* (DDD) :

```
QuranWarsh.sln
├── src/
│   ├── Quran.Domain/            # Entités cœur, agrégats (AudioSegment, QuranCorpus, Wajh, ReferenceMapping), enums, invariants
│   ├── Quran.Application/       # Cas d'usage, interfaces, DTOs, services métiers (Annotation, Awjuh, Validation, Audit)
│   ├── Quran.Infrastructure/    # EF Core 9, PostgreSQL 16, Identity, dépôts, stockage média local/S3, vector pilot
│   ├── Quran.AudioProcessor/    # Extraction de waveform (peaks 100Hz), découpage FFmpeg/FFprobe atomique et idempotent
│   ├── Quran.Import/            # Ingestion MARW, rapprochement de corpus, inventaire Archive.org (114 sourates)
│   └── Quran.Web/               # ASP.NET Core Razor Pages, API REST, authentification sécurisée par cookie, RTL/Arabic UI
└── tests/
    ├── Quran.UnitTests/         # 78 tests unitaires rapides et autonomes (règles métier, transitions d'état, invariants)
    └── Quran.IntegrationTests/  # 32 tests d'intégration (sécurité, autorisations, endpoints publics, audit qualité)
```

---

## 🚀 Prérequis Système

1. **.NET 9 SDK** (version `9.0.100` ou supérieure).
2. **PostgreSQL 16** (ou 17) avec accès superutilisateur ou droits de création de base.
3. **FFmpeg & FFprobe** (disponibles dans le `PATH` système pour le découpage audio haute fidélité et la génération de waveforms).
4. *(Optionnel pour les scripts navigateur)* : **Node.js 20+** et **Playwright**.

---

## ⚙️ Configuration Rapide

### 1. Cloner et configurer la base de données
Copiez ou adaptez la chaîne de connexion dans `src/Quran.Web/appsettings.json` ou `appsettings.Development.json` :
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=quran_db;Username=postgres;Password=VOTRE_MOT_DE_PASSE"
  }
}
```

### 2. Appliquer les migrations de base de données
Exécutez la mise à jour EF Core depuis la racine du dépôt :
```bash
dotnet ef database update --project src/Quran.Infrastructure --startup-project src/Quran.Web
```

### 3. Créer le premier administrateur (sans identifiants codés en dur)
L'application fournit une commande d'initialisation sécurisée :
```bash
dotnet run --project src/Quran.Web -- --create-admin admin@quranwarsh.local "VotreMotDePasseSecurise!123"
```

### 4. Lancer le serveur de développement
```bash
dotnet run --project src/Quran.Web
```
L'application démarre sur :
- **Site public** : `http://localhost:5000` (ou `https://localhost:5001`)
- **Accès administration** : `http://localhost:5000/Account/Login`

---

## 🧪 Exécution des Tests Automatisés

### Tests .NET (Unitaires et d'Intégration)
Toute la suite de tests peut être exécutée en une seule commande autonome :
```bash
dotnet test
```
*Total : 110 tests passants (78 unitaires + 32 intégration).*

### Tests Navigateur (Playwright)
```bash
npm install
npm test
```

---

## 📖 Cartographie des Routes Clés

### Site Public (RTL & Arabe)
- `/` : Accueil avec liste des sourates, statistiques et recherche.
- `/surah/{number}` : Fiche détaillée de la sourate avec ayat et lecteur audio interactif.
- `/quran/{surah}/{ayah}` : Permalien canonique de l'ayah avec surlignage MARW, sélecteur d'oujouth et téléchargement de l'extrait.
- `/mushaf/page/{pageNumber}` : Lecteur pleine page du Mushaf MARW vectoriel (604 pages).
- `/mushaf/awjuh` : Référentiel public consultable des أوجه avec recherche et export JSON (`/api/awjuh/catalog`).

### Back-office Administratif (Protégé : rôles `Admin` et `Reviewer`)
- `/Admin/Index` : Tableau de bord des métriques et avancement.
- `/Admin/AudioEditor` : Studio d'édition audio unifié (capture atomique, répétitions, waveform 100Hz, surlignage direct).
- `/Admin/ReviewQueue` : File d'attente de relecture, validation, rejet motivé et publication.
- `/Admin/QualityReport` : Tableau de bord de consistance (détection des trous >500ms, chevauchements, orphelins, matrice des 114 sourates).
- `/Admin/Awjuh` : CRUD complet du référentiel des oujouth, voies et citations documentaires.
- `/Admin/CorpusMapping` : Rapprochement et documentation des correspondances de décompte (MARW vs standard).
- `/Admin/Inventory` : Gestionnaire des 114 sourates Archive.org avec téléchargement et synchronisation par lot.

---

## 📚 Références Documentaires

- `PLAN_REPRISE_ANTIGRAVITY.md` : Plan directeur séquentiel des Lots 0 à 10.
- `docs/ETAT_DU_SYSTEME.md` : État détaillé de l'architecture, modèles et composants.
- `docs/BACKLOG.md` : Journal de livraison et suivi des tâches.
