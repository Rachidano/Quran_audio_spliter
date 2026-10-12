# Audit de la livraison Antigravity

11 octobre 2026 — audit demandé par l’utilisateur. **Conclusion : implémentation partielle, livraison non validée.**

Le code fournit un socle WPF/.NET 9, des services SQLite, une interface arabe, des traitements médias et un installateur. La compilation et les 68 tests existants réussissent. Cela ne confirme pas la déclaration « projet 100 % terminé » : plusieurs parcours essentiels ne sont pas raccordés, et sept contrôles complémentaires reproduisent des défauts pertinents pour ce projet. Un huitième contrôle, synthétique et portant sur deux pages, est exclu du bilan après correction du périmètre.

Aucune correction du code applicatif n’a été réalisée pendant cet audit. Les ajouts sont les preuves, un programme de reproduction isolé et la mise à jour documentaire du suivi.

## Rectification du périmètre — 11 octobre 2026

L’utilisateur précise que, dans l’édition de notre source officielle utilisée ici, aucune ayah n’est répartie sur deux pages. La vérification des 604 fichiers JSON du corpus local confirme qu’aucune référence `(sourate, ayah)` ne figure sur plusieurs pages.

Le contrôle A02 utilisait des pages synthétiques ; il ne représentait donc pas un cas réel de cette édition. **Son résultat est retiré des défauts et des critères bloquant la livraison.** Il n’est pas demandé de développer une gestion des ayat sur plusieurs pages pour ce corpus. Une ayah peut en revanche occuper plusieurs lignes sur une même page : cette extraction reste à vérifier.

Les résultats bruts sont conservés comme historique d’exécution ; leur interprétation corrigée dans le présent rapport fait foi. Les autres constats restent inchangés.

## 1. Vérifications effectivement exécutées

- Inspection du code applicatif, des tests, des scripts d’installation et des rapports déclaratifs.
- Depuis `desktop/` : `dotnet test QuranVideo.sln --no-restore --logger "trx;LogFileName=audit.trx" --results-directory doc/validation/audit-tests`.
- Résultat : compilation des projets application/tests réussie ; **68 tests déclarés réussis, 0 échec, 0 ignoré**. [Résultat TRX](audit-tests/audit.trx).
- Huit reproductions ciblées exécutées sur des espaces isolés, dont **sept retenues et A02 exclu comme non applicable**, sans toucher à la base du site ni aux ressources originales. [Code de reproduction](audit-2026-10-11/harness/Program.cs), [résultats](audit-2026-10-11/run-02/results.json).
- Contrôle de présence et d’empreinte de `desktop/dist/Setup.exe` : **106 471 189 octets**, SHA-256 `C57C59CD01E720627EFC93D8D80B901EDF9D05050A71C658496B8B9C16703AD1`. L’empreinte correspond au rapport précédent ; sa taille en octets n’y était pas exacte.
- Vérification des formats réels du manifeste des 604 pages et des corrections d’ouverture : ils diffèrent des propriétés attendues par l’importeur.

**Non exécuté :** installation/désinstallation du Setup sur machine propre, recette graphique complète WPF, écoute religieuse/éditoriale, vérification visuelle exhaustive des pages, mesure de précision du lecteur sur quatre heures, production de toutes les vidéos. Aucune de ces validations n’est déduite des tests automatisés.

## 2. Défauts prioritaires

### F01 — Bloquant : premier démarrage sans corpus exploitable

L’importeur lit `totalPages`, alors que le manifeste livré contient `pageCount`. L’appel direct avec la ressource réelle lève `KeyNotFoundException` avant l’import (reproduction **A07**).

Les corrections d’ouverture sont également lues avec `surahOpenings/pageNumber/headerClipThresholdPercent`, alors que le fichier réel expose `Entries`, `Surah`, `Page`, `FirstTextLine`, etc. L’importeur calcule des empreintes, mais ne compare pas les fichiers aux empreintes attendues dans `files[]`.

Par ailleurs, les appels à `ImportPackAsync` ne sont pas raccordés au démarrage ni aux vues. Le démarrage ouvre le workspace puis construit les vues ; il n’initialise ni corpus ni collection audio. L’initialisation de recherche ne crée pas la base et retourne si elle manque. Le script Setup ne livre que les binaires et FFmpeg/FFprobe : aucune ressource Mushaf/texte ni pack compagnon n’est raccordé à l’accueil.

**Conséquence :** la présence de Setup.exe ne suffit pas à fournir une application utilisable avec tout le Coran sur un PC vierge.

**Sources :** [CorpusImportService.cs](../../src/QuranVideo.Desktop/Services/CorpusImportService.cs), lignes 53–56, 69–85, 115–118 et 171–183 ; [App.xaml.cs](../../src/QuranVideo.Desktop/App.xaml.cs), [MainViewModel.cs](../../src/QuranVideo.Desktop/ViewModels/MainViewModel.cs), [setup.iss](../../packaging/setup.iss).

**Correction attendue :** intégrer un assistant minimal d’import réel, comprendre les formats existants, vérifier les empreintes et fournir/détecter le pack ; tester depuis un workspace vide hors du dossier du dépôt.

### F02 — Bloquant : acquisition audio non intégrée et téléchargement défaillant

Les méthodes d’import et téléchargement de `RecitationLibraryService` ne sont pas appelées par les vues ou modèles de vue. La bibliothèque permet de choisir une sourate, mais ne fournit pas le parcours d’acquisition/changement de récitateur décrit dans le plan.

Le téléchargement déplace le fichier reçu vers sa destination finale, puis appelle l’importeur sur cette même destination ; l’importeur tente de copier le fichier sur lui-même. La reproduction **A08**, avec un vrai petit MP3 synthétique et une réponse HTTP locale simulée, échoue sur cette copie. Aucun accès réseau n’est nécessaire pour reproduire ce défaut.

**Source :** [RecitationLibraryService.cs](../../src/QuranVideo.Desktop/Services/RecitationLibraryService.cs), lignes 82–85 et 186–191.

**Correction attendue :** raccorder téléchargement/import à l’interface ; séparer réception temporaire, validation et enregistrement ; tester le parcours complet. La reprise HTTP persistante prévue reste à implémenter.

### F03 — Critique pour le contenu : source audio non liée à la sélection réelle

Le récitateur initial est Al-Husary, alors que le besoin spécifie Saber Abdelhakam avec possibilité de changement. Le studio et les exports codent `husary_murattal_warsh` et recherchent des chemins construits au lieu d’utiliser `AudioSource.RelativeFilePath`.

L’importeur stocke dans `media/audio/<reciter>/<sourate>.mp3`, tandis que le studio et les exports cherchent notamment `media/audio/sources/husary_murattal_warsh_NNN.mp3`. Ils peuvent ensuite prendre **`fatiha_ayah1.mp3` comme secours, quelle que soit la sourate demandée**, si ce fichier de prototype existe. Le défaut est établi par lecture des trois listes de candidats ; aucune vidéo coranique erronée n’a été volontairement produite pendant l’audit.

**Sources :** [StudioViewModel.cs](../../src/QuranVideo.Desktop/ViewModels/StudioViewModel.cs), lignes 230–249 ; [CompositionService.cs](../../src/QuranVideo.Desktop/Services/CompositionService.cs), lignes 130–145 ; [BatchExportService.cs](../../src/QuranVideo.Desktop/Services/BatchExportService.cs), lignes 400–413.

**Correction attendue :** sélectionner une source identifiée en base, résoudre son chemin et vérifier son empreinte ; refuser l’action si elle manque. Supprimer tout secours vers un audio de démonstration. Distinguer récitateurs et collections dans les clés des compositions et noms d’exports.

### F04 — Critique pour le contenu : extraction arbitraire ou mauvaise page

Sans régions, le service découpe un rectangle arbitraire `(50, 100, largeur - 100, 200)` et retourne un succès (**A01**). Il faut au contraire signaler une zone absente et demander une correction.

Le constat initial relatif à une ayah sur deux pages est retiré : le test A02 était synthétique et ne correspond pas à l’édition utilisée. Il ne justifie aucune correction multi-page.

Le studio affiche également `CurrentSurah.StartPage`, indépendamment de l’ayah choisie ; sélectionner une ayah plus loin dans Al-Baqara ne change pas la page selon sa référence.

**Sources :** [AyahImageService.cs](../../src/QuranVideo.Desktop/Services/AyahImageService.cs), lignes 84–121 ; [StudioViewModel.cs](../../src/QuranVideo.Desktop/ViewModels/StudioViewModel.cs), lignes 207–227.

**Correction attendue :** résoudre la bonne page depuis l’ayah et extraire ses régions sur cette page, en conservant les positions relatives des lignes. Refuser une extraction sans régions fiables. Aucune image de remplacement arbitraire ; pas de gestion multi-page à ajouter.

### F05 — Majeur : validation contournée avant export

La préparation cherche un passage sélectionné puis, à défaut, n’importe quel passage. Elle ne contrôle ni `Kind`, ni `IsValidatedByUser`, ni le statut d’approbation de l’image. **A03** crée une composition `Ready` depuis une reprise partielle non validée et une image `Suggested`.

Le rendu passe directement en `Rendering`, sans exiger `Ready`. Les exports par lot n’effectuent pas non plus la vérification de complétude/revue attendue. Les assemblages explicites de portions et les scènes multiples prévus ne sont pas réalisés par le moteur actuel, qui prend une seule plage et une seule image.

**Sources :** [CompositionService.cs](../../src/QuranVideo.Desktop/Services/CompositionService.cs), lignes 36–87 et 115–128 ; [BatchExportService.cs](../../src/QuranVideo.Desktop/Services/BatchExportService.cs), préparation des travaux.

**Correction attendue :** vérifier source, type complet ou assemblage explicitement revu, image approuvée et révisions, au moment de préparer puis de lancer le rendu.

### F06 — Majeur : nouvelles reprises écrasant les précédentes

`SavePassageAsync` recherche un passage de même source/ayah/type et le modifie même lorsque le nouvel objet n’a pas d’identifiant. **A04** enregistre deux reprises complètes nouvelles : seule celle commençant à 20 secondes reste, celle de 10 secondes a disparu.

**Source :** [AnnotationService.cs](../../src/QuranVideo.Desktop/Services/AnnotationService.cs), lignes 94–113.

**Correction attendue :** distinguer explicitement ajout de prise et modification par identifiant ; conserver les occurrences pour permettre le choix utilisateur. L’historique actuel est une pile mémoire, non un historique durable. La session persistée ne contient pas les bornes/curseur/cible de reprise exigés par le plan.

### F07 — Majeur : sauvegarde/restauration insuffisamment fiable

Le workspace sauvegarde la session dans `config.json`, mais la sauvegarde copie `session.json` : la vraie session manque (**A05**).

La restauration vérifie seulement l’existence du manifeste et du fichier SQLite. Elle ne lit ni ne compare l’empreinte déclarée. **A06** remplace la base du ZIP par du texte corrompu sans modifier le manifeste ; le service retourne `Success=true` et déploie ce fichier.

La vue de paramètres restaure sur le workspace actuellement ouvert, avec copies écrasant les fichiers. Aucun mécanisme global ne suspend les captures/rendus pendant sauvegarde/restauration. La cohérence du snapshot SQLite seul ne garantit pas celle des médias et des sessions.

**Sources :** [BackupService.cs](../../src/QuranVideo.Desktop/Services/BackupService.cs), [WorkspaceService.cs](../../src/QuranVideo.Desktop/Services/WorkspaceService.cs), [SettingsViewModel.cs](../../src/QuranVideo.Desktop/ViewModels/SettingsViewModel.cs).

**Correction attendue :** sauvegarde cohérente des bons fichiers, contrôles avant remplacement, restauration vers un espace isolé et bascule maîtrisée ; test de corruption, crash et restauration réelle.

### F08 — Majeur : les preuves actuelles ne permettent pas de clore les lots

Des tests retournent simplement lorsque les ressources locales manquent, et restent comptabilisés comme réussis : `AudioTests.cs` lignes 59, 81, 103 ; `CompositionTests.cs` lignes 210–213, 270–272 ; `CorpusImportTests.cs` ligne 181. Plusieurs chemins dépendent du répertoire courant du test. Le test d’import simule notamment un manifeste avec `totalPages`, ce qui masque l’incompatibilité avec le vrai fichier `pageCount`.

**Conséquence :** « 68/68 verts » ne prouve pas que les 604 pages réelles ont été importées ou que les exports attendus ont été exécutés. La recette de machine propre et celle d’un utilisateur non informaticien ne sont pas démontrées dans les pièces examinées. Le journal J013 reconnaît une installation non vérifiée, alors que J014 déclare la clôture complète.

**Correction attendue :** prérequis explicites, tests manquants signalés comme tels, cas réels reproductibles, validation complète du parcours et preuves humaines distinctes. Ne pas renommer des tests en validation pour contourner leurs critères.

## 3. Autres écarts à traiter

- La correction d’image ajuste uniquement le cadre englobant ; elle ne fournit pas l’ajout/retrait de régions et exclusions nécessaire pour corriger des mots voisins au milieu d’une ligne.
- Le moteur de rendu vérifie présence de flux mais ne compare pas la durée attendue à la durée réelle ; il supprime un export existant si le nom est identique. Les noms individuels ne distinguent pas suffisamment récitateur, prise et révision d’image.
- Les snapshots de lots conservent des chemins de médias mutables ; les empreintes sont calculées pour le manifeste après rendu, sans vérification systématique contre des empreintes figées avant rendu. Le fichier effectivement utilisé peut changer après mise en file.
- L’accès SQLite utilise `EnsureCreated` ; aucune migration de schéma versionnée n’est fournie. La mise à jour d’une installation existante reste à concevoir/tester.
- Quatre entrées principales ont été introduites, dont un écran de lots supplémentaire, contrairement aux trois entrées convenues. Il faut réintégrer les lots à `فيديوهاتي` ou obtenir une décision utilisateur explicite.
- `build_installer.ps1` contient le chemin personnel du compilateur Inno et `C:\ffmpeg\bin`. La fabrication est liée à ce poste ; rendre les chemins configurables et vérifier dépendances/notices. L’installateur existe, mais n’a pas été installé ni qualifié sur machine propre par cet audit.

## 4. Résultats reproductibles complémentaires

| Contrôle | Attendu | Observé |
|---|---|---|
| A01 | Refuser une extraction sans régions | Succès et rectangle arbitraire |
| A02 — NON APPLICABLE | Cas synthétique sur deux pages, absent de cette édition | Exclu des défauts et de la recette ; résultat brut conservé uniquement pour traçabilité |
| A03 | Refuser `Ready` pour une reprise partielle non revue | `Ready`, image `Suggested` |
| A04 | Conserver deux nouvelles reprises | Une seule, première écrasée |
| A05 | Sauvegarder la session réelle | `config.json` absent |
| A06 | Rejeter la base corrompue d’une sauvegarde | Restauration déclarée réussie |
| A07 | Importer le manifeste local réel | `KeyNotFoundException` |
| A08 | Enregistrer un MP3 téléchargé | Échec de copie du fichier sur lui-même |

Commande de reproduction, depuis `desktop/`, avec un **nouveau** dossier de sortie à chaque exécution :

```powershell
dotnet run --project doc/validation/audit-2026-10-11/harness/Audit.csproj -- doc/validation/audit-2026-10-11/run-03
```

Le programme termine normalement après collecte des constats ; **son code de sortie 0 ne signifie pas validation**. Lire `results.json` et les champs `passed`, en excluant A02 conformément à la rectification ci-dessus. Il utilise des pages colorées et un son synthétique pour les contrôles mécaniques, pas une fausse validation coranique. La seule entrée métier réelle nécessaire au contrôle A07 est le manifeste du dépôt.

## 5. Ordre de reprise recommandé

1. Corriger import réel et premier lancement, puis démontrer un workspace neuf contenant toutes les ressources et une recherche exploitable.
2. Raccorder acquisition/sélection audio et supprimer tous les secours vers des fichiers de démonstration ; résoudre la source sélectionnée par son identifiant.
3. Corriger sélection de la page, masques et références ; revoir les cas réels sur une ou plusieurs lignes de la même page avant export.
4. Fiabiliser prises, validations, assemblages/scènes et choix explicite de ce qui est exporté.
5. Fiabiliser file, provenance, sauvegarde/restauration, migrations et conservation des exports.
6. Refaire les tests d’intégration et la recette utilisateur/machine propre ; reconstruire ensuite Setup.exe et actualiser honnêtement les rapports.

La base de travail peut être conservée. Il faut reprendre l’intégration et la validation des lots, pas ajouter des options ou une nouvelle architecture.
