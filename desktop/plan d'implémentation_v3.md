# Plan d’implémentation — V3

Date : 13 octobre 2026. Statut : périmètre validé, développement V3 à commencer.

Ce document est le contrat d’implémentation de la V3. Son suivi opérationnel est dans [tasks_v3.md](tasks_v3.md). Lire aussi [objectif.md](objectif.md), [tasks.md](tasks.md) et les documents de conception de `doc/` pour comprendre l’existant.

Les décisions V3 ci-dessous remplacent les anciennes restrictions à une seule ayah par vidéo et étendent l’application aux collections. Les autres exigences initiales restent applicables. La clôture déclarée de la V2 ne dispense pas de vérifier son fonctionnement : ce plan ne constitue pas un audit ni une certification de la V2.

## 1. Résultat attendu et périmètre

L’utilisateur prépare une vidéo contenant une ayah ou une suite complète d’ayat consécutives, choisit une présentation, vérifie le résultat et exporte localement. Il peut organiser ses créations dans des collections thématiques.

Les huit volets sont :

1. Vidéo multi-ayat avec changement d’image synchronisé et continuité strictement contrôlée.
2. Présentation : fond clair, fond sombre ou image personnelle ; horizontal 16:9 et vertical 9:16.
3. Logo PNG ou signature textuelle, emplacement et opacité, activation par création.
4. Lecteur vidéo intégré à `فيديوهاتي`.
5. Sauvegarde, réouverture, modification et régénération des créations.
6. Collections `مجموعاتي`, gestion de leur contenu et export par lot.
7. Parcours simple en arabe RTL : `اختيار الآيات ← اختيار المظهر ← معاينة ← تصدير`.
8. Mise à niveau V2 vers V3 préservant les données, installateur autonome et maintien des fonctions V2.

Ne pas ajouter de fonctionnalité hors de cette liste sans nouvelle décision utilisateur. Conserver notamment le téléchargement des 114 sourates configurable par récitateur, l’annotation manuelle, les reprises sans catalogue des awjuh, la recherche, l’export MP3, l’export individuel/par lot et la gestion des fichiers.

## 2. Contraintes permanentes

- Windows, WPF, C#, .NET 9 ; conserver le projet applicatif et le projet de tests existants.
- SQLite locale pour les informations structurées ; fichiers locaux pour les médias. Pas de nouveau framework d’architecture ni de service distant requis.
- Source audio par défaut : `الشيخ صابر الحكم`, avec les liens déjà configurés et modifiables dans l’application.
- Interface et installation en arabe, RTL ; temps, chemins et références techniques isolés en LTR si nécessaire.
- Ne pas modifier le projet web ni ses données. Les travaux applicatifs restent sous `desktop/`.
- Utiliser les pixels des images sources : aucun rendu du texte coranique par police, OCR ou génération. Ne pas modifier l’écriture, les points, les signes ni les numéros. Préserver les positions relatives des lignes et leurs blancs.
- Dans cette édition, une ayah n’est pas répartie sur deux pages. Une séquence peut traverser plusieurs pages : charger l’image de chaque ayah depuis sa page propre.
- Afficher `مصدرنا الرسمي` dans l’interface ; conserver l’attribution au ministère et son lien dans À propos.
- Fonctionnement hors connexion après acquisition des ressources ; le téléchargement exige une connexion. Ne pas annoncer que les 114 fichiers sont disponibles simplement parce que leurs liens existent.
- Ne pas intégrer au Git les bases utilisateur, audios, vidéos, caches ni preuves volumineuses.

## 3. Règles bloquantes de création et d’export

### 3.1 Sélection complète

Une création contient une sourate et l’intervalle inclusif `[première ayah, dernière ayah]`. Une seule ayah est un intervalle dont les deux bornes sont égales. Utiliser la numérotation du corpus Warsh local, pas une numérotation Hafs codée en dur.

Déduire la liste complète des ayat à partir de cet intervalle ; ne jamais construire implicitement la séquence à partir des seules annotations disponibles. Vérifier les limites de la sourate, l’ordre, l’absence de doublon et la présence de chaque ayah intermédiaire.

Toutes les prises retenues doivent appartenir au même récitateur et au même enregistrement source. Une prise doit couvrir l’ayah entière ; une répétition partielle ne peut pas remplacer une ayah complète. Plusieurs prises complètes possibles imposent un choix explicite, mémorisé, sans demander les awjuh.

### 3.2 Validation des ressources

Avant tout rendu, contrôler pour chaque ayah :

- Annotation disponible et revue selon le mécanisme de validation de l’application.
- Début et fin finis, `0 ≤ début < fin ≤ durée de la source` et durée exploitable.
- Correspondance entre annotation, ayah, source audio et version de cette source.
- Fichier audio présent, lisible, et identité cohérente avec celle enregistrée.
- Image complète présente, lisible et revue ; aucune extraction fictive ou image de secours d’une autre ayah.
- Configuration visuelle valide et ressources personnelles présentes si activées.

Les prises suivent l’ordre temporel de l’enregistrement : pas d’inversion ni de chevauchement des passages choisis. Ne pas corriger discrètement des bornes incohérentes.

### 3.3 Continuité sonore et répétitions

- Si les passages annotés se touchent, utiliser la continuité du même audio et changer l’image aux bornes définies.
- Si un intervalle non sélectionné sépare deux prises, le signaler et faire écouter la jonction. L’utilisateur doit confirmer que les prises complètes choisies forment bien la suite attendue ; sinon bloquer l’export. Ne pas exporter silencieusement cet intervalle, qui peut contenir une reprise.
- Ne pas considérer un intervalle temporel comme une ayah manquante si toutes les ayat consécutives disposent de leurs prises complètes ; ne pas masquer une ayah manquante sous prétexte de continuité audio.
- Ne pas ajouter de fondu sonore qui mélange des récitations ou tronque les syllabes.

### 3.4 Refus explicite et partagé

Un validateur commun est appelé pour l’export individuel, la régénération et les lots. Il retourne tous les problèmes identifiables, associés aux ayat concernées. Exemple :

> تعذّر التصدير: المقطع الصوتي للآية 12 غير محدّد. يجب استكمال جميع الآيات من 10 إلى 15 قبل التصدير.

Aucun export ne doit ignorer une ayah, substituer un autre récitateur ou utiliser une image arbitraire. Pour un lot, valider tous les éléments sélectionnés avant de commencer : une seule erreur bloque le lancement du lot entier.

Recontrôler les ressources avant chaque travail. Si un fichier disparaît pendant le lot, arrêter le lot et signaler les éléments non produits. Les sorties déjà terminées restent identifiées ; ne pas annoncer la réussite de tout le lot. Une annulation ne publie jamais un fichier temporaire comme vidéo terminée.

## 4. Organisation technique proposée

Réutiliser `AppDbContext`, `AnnotationService`, `CompositionService`, `AyahImageService`, `FfmpegVideoGenerator`, `BatchExportService`, `BackupService` et les ViewModels existants. Inspecter leur état réel avant modification. Les noms de nouvelles entités ci-dessous sont indicatifs ; étendre l’existant si cela évite un doublon.

### 4.1 Données minimales

| Donnée | Informations nécessaires |
|---|---|
| Création sauvegardée | Identifiant stable, titre, sourate, intervalle, récitateur/source, format, présentation, signature, révision et dates |
| Ayah de la création | Ordre, référence de l’ayah, annotation/prise choisie, bornes et révision, référence/révision de l’image, confirmation éventuelle de jonction |
| Ressource personnelle | Chemin géré dans l’espace de travail, type et identité du fichier pour fond ou logo |
| Collection | Identifiant, nom, dates |
| Élément de collection | Collection, création référencée, ordre ; une création peut représenter une ayah ou une séquence |
| Sortie / travail | Création et révision utilisées, MP3/MP4, état, chemin final, erreur éventuelle |

Conserver une référence aux annotations et un instantané des choix ayant servi au rendu. Si l’annotation change, indiquer que la création doit être revue ; ne pas modifier silencieusement les anciennes vidéos ni réutiliser des validations périmées.

Copier les fonds et logos importés dans l’espace de travail afin qu’un déplacement du fichier original ne casse pas les créations. Les références médias sont relatives à cet espace. Aucune interface de saisie JSON n’est nécessaire.

### 4.2 Rendu

Construire une chronologie explicite : prise audio retenue, image, durée, position de début dans la vidéo. Chaque ayah conserve toute sa durée audio et son image jusqu’au changement suivant.

Utiliser un même moteur de mise en page pour l’aperçu et l’export. Adapter la stratégie FFmpeg à la fiabilité de la synchronisation ; ne pas imposer une concaténation sans réencodage. Uniformiser résolution, cadence et paramètres audio/vidéo.

Préparer les fichiers temporaires dans un emplacement contrôlé, puis ne publier la sortie finale qu’après vérification. La durée attendue est la somme des passages retenus, ou la durée du segment continu correspondant. Vérifier durée réelle, dimensions, présence des pistes et lecture du fichier. Fixer et documenter une tolérance liée à la cadence vidéo et à l’encodage audio, sans tolérance arbitraire de plusieurs secondes.

## 5. Lots d’implémentation

### L00 — Vérifier et figer le point de départ V2

Lire les documents et l’état Git. Exécuter les tests existants et une recette ciblée des cinq évolutions V2. Relever les tests ignorés ou ne faisant aucune assertion faute de ressources. Sauvegarder un espace V2 représentatif et documenter son contenu pour tester la migration sans toucher aux données originales.

**Livrable :** état de départ et preuves dans `doc/validation/v3/`. Les anomalies bloquant la V3 sont traitées avant les lots dépendants.

### L01 — Stockage V3 et migration

Mettre en place les créations persistantes, leurs choix par ayah, les ressources personnelles et les collections. Prévoir une version de schéma et une migration incrémentale, transactionnelle et réexécutable sans dommage. Sauvegarder une base SQLite cohérente avant migration, en tenant compte d’un éventuel journal WAL. Aucun effacement/recréation de la base utilisateur.

Étendre sauvegarde/restauration aux nouvelles données et ressources. Tester installation vierge, mise à niveau V2, second démarrage et échec de migration. En cas d’échec, présenter un message exploitable et préserver l’état précédent.

**Acceptation :** récitateurs, 114 liens configurés, annotations, médias et réglages V2 restent accessibles et inchangés hors évolution prévue.

### L02 — Séquence et validation métier

Implémenter l’intervalle complet, le choix des prises et le validateur commun de la section 3. Réutiliser les états de revue existants ; une simple présence en base n’est pas une validation humaine. Afficher les manques et permettre de revenir à l’annotation concernée.

**Acceptation :** une plage 10–15 ne peut pas être exportée si l’ayah 12 est absente ou invalide, depuis aucun point d’entrée.

### L03 — Export multi-ayat synchronisé

Produire d’abord une vidéo de cinq ayat consécutives déjà revues avec la présentation classique. Intégrer les prises complètes sélectionnées et traiter explicitement les jonctions. Gérer une séquence traversant un changement de page du Mushaf sans supposer qu’une ayah traverse elle-même deux pages.

Conserver export individuel et MP3 V2. L’export MP3 existant ne doit pas être supprimé ou rendu dépendant du choix d’un fond vidéo. Ajouter progression, annulation et vérification finale au nouveau rendu.

**Acceptation :** image correcte sur chaque passage, aucun décalage cumulatif, aucun verset omis et aucune syllabe tronquée aux jonctions lors de la recette humaine.

### L04 — Présentation et identité visuelle

Proposer trois fonds : clair, sombre, image importée. L’ayah reste sur un panneau clair ; son image originale n’est ni recolorée ni rendue transparente par suppression de couleur. Ajuster proportionnellement son échelle, sans rogner ni déplacer les fragments entre eux. Contrôler la lisibilité des ayat longues dans les deux orientations.

Ajouter logo PNG ou signature textuelle, quatre coins et quelques opacités prédéfinies. Réserver une zone distincte du panneau coranique pour empêcher tout recouvrement. Vérifier la liaison des lettres arabes de la signature. Mémoriser les préférences générales et leurs valeurs propres à chaque création ; les changements globaux ne modifient pas rétroactivement les créations enregistrées.

**Acceptation :** même rendu dans aperçu et export, aucun signe masqué, options identiques pour export individuel, séquence et lot.

### L05 — Parcours et réouverture des créations

Intégrer les quatre étapes arabes dans la navigation existante, sans créer un second studio concurrent. Les réglages secondaires sont repliés. Un choix ambigu de prise est clairement présenté et préécoutable. Afficher les blocages près de l’action d’export et conserver les choix après fermeture/redémarrage.

Permettre de rouvrir une création, modifier le fond, le format ou les prises et régénérer. Détecter sources/images/annotations modifiées depuis la précédente exportation. Conserver le résultat précédent jusqu’au succès du nouveau ; aucune écrasement silencieux d’un fichier existant.

**Acceptation :** un utilisateur peut préparer, quitter, reprendre et exporter sans JSON ni manipulation technique de fichiers.

### L06 — Lecteur intégré et gestion des fichiers

Dans `فيديوهاتي`, ajouter lecture/pause, progression déplaçable, volume et plein écran. Vérifier le composant choisi sur un Windows cible avec l’installateur autonome, sans dépendre d’un lecteur externe installé.

Libérer le média avant suppression ou renommage ; mettre à jour les chemins référencés sans casser la création. Traiter fichier absent, illisible, passage d’une vidéo à une autre et fermeture de la vue. Préserver le comportement des fichiers MP3 gérés dans la V2.

**Acceptation :** lire puis renommer/supprimer une vidéo ne provoque ni verrou persistant ni référence invalide non signalée.

### L07 — Collections et export par lot

Ajouter `مجموعاتي`, de préférence comme vue ou onglet dans l’espace de gestion existant. Créer, renommer et supprimer une collection ; ajouter/retirer des créations d’une ayah ou d’une séquence ; présenter leur ordre et leur état de préparation.

Une collection peut contenir des éléments non consécutifs, éventuellement de sourates différentes. Elle n’est jamais convertie en une séquence vidéo implicite : chaque élément sélectionné produit son fichier distinct. Une séquence interne respecte toutes les règles de continuité.

Lancer la validation complète de la sélection avant tout export par lot. Empêcher les collisions de noms. Afficher progression, erreurs et annulation. Un filtre de sourate ne doit pas ajouter des éléments non sélectionnés ni masquer silencieusement une partie de la sélection d’une collection.

Supprimer la collection ne supprime que son regroupement et ses liens ; conserver créations, annotations et fichiers. Retirer un élément a le même principe non destructif.

**Acceptation :** deux ayat non consécutives d’une collection donnent deux vidéos ; si un élément du lot est invalide, aucun rendu ne commence.

### L08 — Recette complète, documentation et Setup

Exécuter les tests pertinents, puis une recette humaine du parcours installé. Produire `Setup.exe` autonome Windows sous .NET 9 avec les outils requis et installation arabe. Tester installation vierge et mise à niveau sur une copie de données V2, sans SDK ni FFmpeg à installer manuellement.

Actualiser le guide arabe, À propos/version, notes de version et consignes de sauvegarde. Documenter les preuves, limitations réelles et validations humaines en attente. Ne pas déclarer « terminé » tant que les critères bloquants ne sont pas vérifiés.

**Acceptation :** tous les huit volets fonctionnent dans l’application installée ; données V2 préservées, ressources locales utilisables sans réseau et téléchargement explicite toujours disponible avec réseau.

## 6. Matrice minimale de validation

| Cas | Résultat attendu |
|---|---|
| Une ayah complète | Export individuel toujours fonctionnel |
| Cinq ayat consécutives revues | Une vidéo, images synchronisées et ordre respecté |
| Ayah intermédiaire absente | Export entièrement bloqué et numéro indiqué |
| Annotation non revue, image absente/invalide, audio absent | Refus précis avant rendu |
| Mélange de sourates, récitateurs ou sources dans une création | Refus, y compris données incohérentes persistées |
| Prise partielle utilisée comme ayah entière | Refus |
| Plusieurs prises complètes | Choix explicite conservé |
| Bornes inversées, hors durée, non finies, chevauchement ou ordre audio inversé | Refus |
| Intervalle entre prises | Jonction signalée, revue nécessaire avant export |
| Séquence changeant de page | Bonne image pour chaque ayah |
| Fond clair/sombre/personnel × horizontal/vertical | Proportions conservées, aucun signe coupé |
| Signature arabe et logo aux quatre coins | Lisibles, jamais sur l’ayah |
| Réouverture après redémarrage | Choix retrouvés ; ressources modifiées détectées |
| Collection avec ayat non consécutives | Fichiers distincts, jamais fusion implicite |
| Lot contenant un élément invalide | Aucun travail lancé |
| Annulation ou disparition d’une ressource en cours | Pas de sortie partielle déclarée réussie |
| Lecture puis renommage/suppression | Pas de verrou de fichier ni lien cassé ignoré |
| Suppression d’une collection | Créations, annotations et médias conservés |
| Migration et restauration sur copie V2 | Données et réglages préservés |
| Installation autonome et session hors ligne | Utilisation des ressources locales sans dépendance au site |

Utiliser des tests automatisés pour les règles, la persistance, la migration et les sorties FFmpeg. La fidélité des signes, la lisibilité, la qualité des jonctions et l’ergonomie nécessitent aussi une vérification humaine. Une ressource de test manquante doit produire un résultat explicite, jamais une réussite silencieuse.

## 7. Méthode de livraison

Avancer dans l’ordre L00 à L08 ; mettre à jour [tasks_v3.md](tasks_v3.md) à chaque étape. Chaque lot se clôture avec fichiers modifiés, tests réellement exécutés, résultat et preuve. Ne pas cocher une recette humaine avant qu’elle ait eu lieu.

La première démonstration attendue est une séquence de cinq ayat synchronisées sur fond classique. Les présentations, la gestion des créations et les collections enrichissent ensuite ce résultat, sans remplacer ni affaiblir les validations métier.
