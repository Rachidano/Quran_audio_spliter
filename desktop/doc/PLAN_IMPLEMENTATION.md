# Plan d’implémentation — application desktop Windows

Version 1.1 — 11 octobre 2026. **Plan à exécuter ; aucun des lots ci-dessous n’est déclaré implémenté.**

Document associé : [conception détaillée](CONCEPTION_DETAILLEE.md). Le périmètre est une application locale autonome produisant des vidéos MP4 d’ayat à partir des images originales et d’annotations audio humaines, sans gestion des أوجه.

## 1. Règles d’exécution

- Développer sous `desktop/`, dans une solution distincte. Ne pas migrer le site ni modifier sa base pour faire fonctionner l’application.
- Livrer un **installateur Windows Setup.exe**, une interface entièrement arabe RTL et un parcours accessible à une personne non informaticienne. Ces exigences font partie de la recette, pas d’une amélioration facultative.
- Garder un seul projet WPF et un projet de tests ; organiser le code en dossiers. Pas de couches, bibliothèques ou abstractions créées sans besoin concret.
- Réutiliser les sources existantes par import contrôlé. Ne pas déplacer ni réécrire leurs originaux.
- Consigner les versions réellement retenues des dépendances. Le choix confirmé est .NET 9 (`net9.0-windows`), avec WPF, SQLite et les outils audio décrits dans la conception ; la précision du lecteur reste à qualifier au lot 1.
- Livrer un chemin complet utilisable à chaque jalon : données réelles, résultat inspectable, erreurs explicites. Les maquettes et données simulées ne constituent pas une fonctionnalité terminée.
- Effectuer des tests proportionnés aux changements. Pour les calculs, l’intégrité des données et les frontières audio, conserver des tests automatisés ; pour la conformité des pixels et de la récitation, conserver une recette humaine.
- Ne jamais traduire automatiquement « contrôles techniques réussis » en « texte coranique, image ou passage validé ».
- Un jalon de revue sert à vérifier le résultat et à ajuster la conception ; ce n’est pas une demande de permission à chaque opération de développement.

## 2. Ordre des travaux et jalons

| Lot | Objet | Dépendances | Résultat inspectable |
|---|---|---|---|
| 0 | Inventaire et données de référence | Aucun | Manifeste d’import et liste des cas de recette |
| 1 | Prototype image + audio → MP4 | 0 | Premières vidéos réelles et rapport de précision |
| 2 | Socle Windows et organisation du projet | 1 | Application ouvrant un espace de travail local |
| 3 | SQLite, import du corpus, intégrité | 2 | 114 sourates, 6 214 références et 604 pages indexées |
| 4 | Navigation, recherche et affichage | 3 | Navigation autonome dans tout le corpus |
| 5 | Récitateurs, téléchargement, lecteur | 1, 3 | Sources audio locales gérées et lecture précise |
| 6 | Studio d’annotation | 4, 5 | Annotations persistantes et reprise de session |
| 7 | Extraction et correction des images | 1, 4 | Images d’ayat inspectables et versionnées |
| 8 | Composition et export individuel | 6, 7 | MP4 d’une ayah validée, avec manifeste |
| 9 | File d’exports et récupération | 8 | Production de plusieurs vidéos sans intervention continue |
| 10 | Sauvegarde et installateur Windows | 3, 9 | Setup.exe autonome .NET 9, assistant arabe RTL et espace restaurable |
| 11 | Recette finale et documentation | Tous | Livraison V1 avec limites connues documentées |

Les lots 4 et 5 peuvent avancer indépendamment après le lot 3 ; le lot 7 peut avancer avant la fin du studio. L’ordre conseillé reste de résoudre le risque graphique et temporel au lot 1, avant d’investir dans les écrans complets.

**Jalon A :** lot 1, démonstration de faisabilité sur quelques ayat. **Jalon B :** lots 2 à 7, outil d’annotation et de préparation. **Jalon C :** lots 8 et 9, production MP4. **Jalon D :** lots 10 et 11, version Windows livrable. Le jalon A n’est pas une application couvrant tout le besoin.

## 3. Lot 0 — Figer les entrées et les cas de contrôle

### Travaux

1. Relever l’état Git initial et lire les besoins et conventions du projet existant ; établir un inventaire sans modification du site.
2. Contrôler les 604 paires PNG/JSON et le manifeste de `data/mushaf/editions/marw-newMushaf/20260913-full/` : présence, taille, SHA-256, dimensions et références d’ayat. La vérification de présence déjà faite pour cette conception ne remplace pas le recalcul des empreintes.
3. Importer comme métadonnées la provenance et la révision de l’édition. Vérifier les 114 sourates, les 6 214 références uniques et les 285 ayat de Baqara pour **cette édition**.
4. Examiner le corpus candidat `data/reference/warsh-qpc-v10/`, notamment ses différences de pages signalées. Conserver son statut candidat ; ne pas utiliser sa pagination pour remplacer la cartographie des images.
5. Lire les corrections des ouvertures de sourates et leur liaison aux empreintes des pages. Décrire quelles corrections sont réutilisables et lesquelles demandent une vérification.
6. Relever les fichiers MP3 effectivement présents et leurs caractéristiques avec FFprobe. Importer les URL disponibles dans `data/reference/archive_org_114_inventory.json`, sans assimiler inventaire et fichiers téléchargés.
7. Relever le SDK .NET 9 installé, retenir des dépendances compatibles et fixer le SDK sous `desktop/` sans changer celui du site. Consigner un PC Windows x64 de recette et les conditions hors ligne. Retenir un outil existant pour produire un Setup.exe avec assistant arabe RTL ; vérifier tôt un emballage minimal, sans développer un installateur maison.
8. Préparer un manifeste de cas réels : sourate/ayah, page(s), type de difficulté, source image, source audio si disponible, résultat attendu et état de revue.

### Livrables et acceptation

- Inventaire reproductible avec erreurs bloquantes distinctes des avertissements de qualification.
- Liste d’au moins douze cas représentatifs : début/fin de ligne, milieu de ligne, plusieurs lignes, marqueur d’ayah proche du bord, ouverture avec basmala, sourate 9, Fatiha, transition de page, ayah longue et répétition audio.
- Choisir les cas traversant des pages à partir des JSON réels ; ne pas inventer une référence de test.
- Aucune donnée du site supprimée ou modifiée. Les annotations historiques restent des entrées éventuelles à revoir, jamais des vérités implicites.

## 4. Lot 1 — Prototype de faisabilité prioritaire

### Travaux

1. Lire une image et ses zones ; implémenter la conversion `cropBox → textBox → rects → pixels` détaillée dans la conception, indépendamment du zoom de l’interface.
2. Produire un masque constitué des régions de l’ayah. Conserver la position relative des lignes, les signes et le numéro ; rendre le reste blanc. Ne pas juxtaposer les mots ni déplacer une ligne pour combler son indentation.
3. Comparer trois vues côte à côte : page originale, masque superposé, image isolée. Identifier les mots voisins inclus et les signes coupés. Démontrer une correction manuelle du masque.
4. Traiter un cas sur deux pages par deux fragments issus des originaux ; expliciter la nouvelle disposition entre fragments. Vérifier la lisibilité d’un cas long en cadre vidéo.
5. Décoder un MP3, choisir des bornes réelles à l’écoute, produire un extrait de référence et un MP4 avec image fixe. Utiliser la même définition des bornes temporelles pour le lecteur et l’export.
6. Mesurer les écarts entre curseur, audio entendu et échantillons exportés, notamment pour un MP3 VBR et le long fichier de Baqara. Examiner les effets du délai d’encodage et des seek approximatifs.
7. Générer un exemple horizontal et un exemple vertical, avec ajustement proportionnel et sans étirement du texte.
8. Évaluer le décodage NAudio/Windows face au rendu FFmpeg. Si les frontières ne concordent pas, retenir un décodage commun ou un cache PCM limité ; documenter la décision avant le lot 5.

### Tests et critère de passage

- Tests de géométrie sur rectangles synthétiques, plus comparaison visuelle sur les cas réels du lot 0.
- Test audio synthétique avec événements à des positions connues : pas de confiance exclusive dans une mesure à l’oreille.
- FFprobe confirme codecs, dimensions et durée ; le MP4 se lit dans un lecteur Windows courant.
- Rapport donnant erreurs observées, correction retenue, précision obtenue, coût mémoire et exemples produits.
- **Le passage au studio complet suppose une extraction corrigeable et un modèle de temps cohérent.** Si les zones actuelles ne suffisent pas, renforcer l’éditeur de masques ; ne pas promettre une extraction entièrement automatique.

## 5. Lot 2 — Socle de l’application Windows

### Travaux

1. Créer une solution contenant un seul projet WPF `net9.0-windows` et un projet de tests. Regrouper vues, modèles de vue, modèles, données et services dans des dossiers. Fixer le SDK .NET 9 sous `desktop/` ; ne pas modifier celui du site.
2. Utiliser MVVM léger, commandes asynchrones avec annulation et composition simple des dépendances. Ajouter des interfaces seulement lorsqu’elles servent un test ou un remplacement concret. Pas de repository générique, médiateur, bus d’événements ou moteur de plugins.
3. Créer trois entrées : `المكتبة`, `إعداد الفيديو`, `فيديوهاتي`. Intégrer image et export comme étapes du même parcours ; placer paramètres et sauvegarde dans un menu secondaire. Ne pas multiplier les écrans administratifs.
4. Préparer tous les libellés, dialogues, erreurs et aide en arabe RTL ; isoler nombres/temps/chemins en LTR. Chaque action a un bouton explicite ; les raccourcis restent facultatifs. Aucun champ « wajh » ni reprise de modèle des أوجه.
5. Proposer un dossier dans Documents, un bouton de démarrage et la reprise automatique du dernier travail. Le changement de dossier reste facultatif. Conserver chemins relatifs et verrou d’écriture sans les exposer dans le parcours courant.
6. Ajouter journal local, identification des versions et diagnostic exportable sans transmettre les médias.

### Acceptation

Application démarrant sans serveur web, base PostgreSQL ni connexion Internet ; création, fermeture et réouverture d’un espace vide ; erreurs lisibles pour dossier inaccessible ou déjà ouvert. Les tâches longues ne bloquent pas la navigation. Un utilisateur non informaticien comprend les trois entrées et ouvre une sourate sans manipuler de chemins techniques ni lire la documentation développeur.

## 6. Lot 3 — Base locale et import des ressources

### Travaux

1. Implémenter seulement les entités utiles au corpus et à l’espace de travail ; ajouter les tables audio/export dans leurs lots respectifs. Le modèle logique n’impose pas une table par concept : préférences et profils fixes peuvent rester en configuration. Conserver les contraintes d’intégrité et migrations nécessaires, sans infrastructure générique supplémentaire.
2. Séparer édition, références canoniques, textes candidats, pages et régions. Stocker les textes sources sans les normaliser en place.
3. Créer un importeur partagé pour les ressources livrées et les packs : détection automatique au premier lancement, manifeste, empreintes, fichiers attendus, dimensions et références. Refuser les chemins sortant du dossier cible ; limiter taille et nombre d’entrées lors d’une extraction d’archive.
4. Préparer les fichiers en zone temporaire, vérifier, puis promouvoir l’import. Gérer un arrêt entre promotion de fichiers et transaction SQLite par une procédure de réconciliation.
5. Rendre l’import idempotent pour une même révision ; une nouvelle édition ou empreinte crée une version distincte.
6. Conserver les rects bruts et appliquer les corrections qualifiées séparément. Une correction liée à une empreinte ne s’applique pas à une image différente.
7. Ajouter versions de schéma et provenance. Préparer la migration avec snapshot cohérent préalable.

### Tests et acceptation

- Import complet : 604 pages, 114 sourates, 6 214 références uniques et liens de régions cohérents ; aucun chargement implicite de la base du site.
- Réimport sans doublons ; rejet d’une empreinte erronée, d’un JSON mal formé et d’un chemin malveillant.
- Simulation d’arrêt à plusieurs étapes : reprise ou nettoyage cohérent, sans édition faussement complète.
- Un espace copié après fermeture propre s’ouvre depuis un autre chemin, y compris avec espaces et caractères arabes.

## 7. Lot 4 — Bibliothèque, recherche et navigation

### Travaux

1. Construire l’index de recherche dérivé des textes importés et conserver la version de normalisation. Un index en mémoire est acceptable pour ce volume ; SQLite conserve les données durables.
2. Reprendre les règles fonctionnelles du site : recherche textuelle à partir de trois lettres, recherche de référence indépendante de ce minimum, plusieurs mots recherchés dans leur ordre et de façon contiguë après normalisation.
3. Afficher le texte de chaque résultat, la sourate, le numéro d’ayah entre parenthèses en chiffres latins et un accès direct à la page et aux zones correspondantes.
4. Paginer sans plafonner globalement le nombre de résultats. Tester notamment `الله لا` pour exclure les occurrences où les deux mots sont dispersés.
5. Afficher les images originales avec sélection d’ayah, zoom, navigation de page et prise en charge des ayat présentes sur plusieurs pages.
6. Qualifier le rendu du texte de recherche avec les polices retenues : liaison du ي final, signes particuliers et numéros. Appliquer une police compatible à l’ensemble d’un segment arabe si nécessaire, pour éviter une rupture de liaison par substitution caractère par caractère.
7. Respecter les libellés demandés : `أكتب كلمة أو آية أو رقم` et `مصدرنا الرسمي`. Placer l’attribution complète et le lien du ministère dans À propos.

### Acceptation

Recherche sans tashkeel et avec tashkeel, ponctuation et variantes normalisées couvertes par tests ; source textuelle inchangée ; référence numérique correcte ; clic sur un résultat ouvrant la bonne région de l’édition. Les images restent la référence visuelle, le texte candidat garde son statut explicite.

## 8. Lot 5 — Gestion audio et lecture

### Travaux

1. Créer récitateurs et collections de récitations ; associer chaque source à une collection et une sourate. Un changement de récitateur ne remplace pas les sources ni les annotations existantes.
2. Préremplir la collection initiale et afficher simplement récitateur, sourate, `تحميل التلاوة` et `اختيار ملف صوتي`. Copier les MP3 choisis dans l’espace. L’import d’un manifeste reste une option avancée ; il n’est pas requis pour l’utilisateur courant.
3. Ajouter une file de téléchargements persistante, progression, annulation, reprise et contrôle de l’espace disque. Si le serveur ne respecte pas Range ou si la ressource a changé, recommencer proprement.
4. Vérifier le contenu réellement obtenu avec FFprobe, calculer l’empreinte, enregistrer fréquence, canaux et durée décodée. Une empreinte calculée localement ne prouve pas à elle seule l’authenticité d’une source distante.
5. Implémenter le lecteur selon la décision du lot 1 : lecture/pause, seek, petits déplacements, vitesse, volume et boucle de sélection.
6. Produire les formes d’onde progressivement avec plusieurs niveaux de détail et cache disque ; ne pas décoder quatre heures en mémoire en une seule opération.
7. Signaler les fichiers manquants ou modifiés et proposer leur réimport, sans réaffecter silencieusement des annotations à un autre audio.

### Acceptation

Téléchargement interrompu puis relancé, réponse HTML à la place d’un MP3, changement d’ETag, manque de place, source invalide et fichier long testés. Deux récitateurs peuvent coexister. Les coordonnées temporelles du lecteur concordent avec les exports du prototype, aux limites documentées.

## 9. Lot 6 — Studio d’annotation humain

### Travaux

1. Présenter ensemble lecteur, forme d’onde, page du Mushaf, ayah sélectionnée et bornes modifiables. Afficher un libellé complet de la cible, par exemple `سورة البقرة — الآية 4`, sans référence compacte ambiguë.
2. Afficher un bouton arabe pour placer le début au curseur, avec F7 facultatif, et un horaire modifiable en minutes/secondes. Garder millisecondes brutes et micro-ajustements dans le panneau avancé fermé. Ne pas modifier rétroactivement le passage précédent.
3. Afficher un bouton arabe pour enregistrer la fin et passer à l’ayah suivante (F8/Entrée facultatifs), puis préremplir début suivant = fin enregistrée et avancer le numéro. À la dernière ayah, terminer la session de sourate sans créer une référence hors limites.
4. Préserver position et début lors d’un changement manuel d’ayah. Persister la cible courante, la cible de répétition, les bornes et l’état de session.
5. Mettre les répétitions et contrôles fins dans les options avancées repliées. Choisir ayah cible et type : ayah complète, portion, continuation ou reprise complète/partielle. Aucun choix de wajh.
6. Pour F9, afficher explicitement la cible de la reprise. Après l’enregistrement d’une ayah, une reprise peut viser celle qui vient d’être terminée tandis que la cible principale a avancé ; ce comportement doit être visible et modifiable. Un changement manuel de cible doit suivre la règle de synchronisation documentée, sans maintenir une ancienne cible cachée.
7. Ajouter aperçu du passage, validation humaine, annulation du dernier enregistrement et historique des corrections. Une correction de bornes invalide les compositions qui dépendaient de l’ancienne révision.
8. Protéger contre double clic, raccourci dans un champ de saisie, durée nulle, borne hors source et enregistrement pendant un seek non stabilisé.
9. Exporter/importer les annotations avec version de format, édition et empreinte audio. Un fichier incompatible ne doit pas être accepté par simple égalité du nom de sourate.

### Acceptation

Annoter une série réelle, fermer et reprendre exactement la session ; corriger un début ; revenir manuellement à une ayah ; enregistrer une reprise partielle ; annuler et refaire une opération. La reprise n’avance pas la cible principale et ne devient pas automatiquement une récitation complète exploitable en lecture continue ou en export complet.

## 10. Lot 7 — Préparation des images d’ayat

### Travaux

1. Industrialiser le prototype : service d’extraction déterministe, recette versionnée et cache lié à l’empreinte des sources et aux paramètres.
2. Afficher d’abord le résultat proposé avec `الصورة صحيحة` et `تصحيح الصورة`. Ouvrir uniquement à la demande le panneau original/masque/résultat pour ajouter ou retirer des régions, ajuster les bords et réinitialiser. Ne pas exposer de coordonnées à saisir dans le parcours courant.
3. Garder les coordonnées en pixels source ; le zoom et le DPI Windows ne doivent jamais être enregistrés comme géométrie du Mushaf.
4. Traiter les corrections d’ouverture, marqueurs, signes au-dessus/en dessous, mots voisins et début au milieu de ligne. Éviter une simple marge automatique qui inclurait l’ayah précédente.
5. Gérer plusieurs fragments et plusieurs pages dans une recette. Préserver la disposition interne de chaque fragment ; afficher clairement l’ordre de composition choisi.
6. Produire un PNG sans perte et son manifeste. Séparer aperçu, image à revoir et image revue. Modifier un masque rend les compositions dépendantes à revoir.
7. Pour une ayah trop longue, préparer soit une vue intégrale lisible, soit plusieurs scènes avec frontières temporelles posées manuellement. Ne pas distribuer automatiquement le temps selon le nombre de mots.

### Acceptation

Tous les cas du lot 0 passent la comparaison humaine, y compris une correction sauvegardée puis restaurée. Aucun glyphe n’est redessiné avec une police, aucun traitement génératif ne reconstruit le texte. Les tests automatiques contrôlent dimensions, masques, déterminisme et versionnement ; la revue contrôle le contenu.

## 11. Lot 8 — Composition et export MP4 individuel

### Travaux

1. Créer une composition liée à une ayah, un récitateur et des versions de passages/images. Choisir une prise complète, ou un assemblage explicite de portions avec écoute obligatoire de l’ensemble.
2. Ne pas assembler implicitement toutes les répétitions. Refuser le statut prêt si la couverture de l’ayah n’a pas été vérifiée humainement ; la durée seule ne prouve pas cette couverture.
3. Préparer une prévisualisation fidèle : cadre, marges, fond, taille réelle du texte, scènes et audio final. Une seule image reste affichée pendant toute la récitation dans le cas simple.
4. Afficher seulement les choix horizontal/vertical, l’aperçu et `إنشاء الفيديو`. Fixer en interne MP4 H.264/AAC, marges, dimensions paires, fréquence et format de pixels ; aucun formulaire codecs/débits/fps ni éditeur de profils dans la V1.
5. Construire les appels FFmpeg avec arguments séparés, processus sans fenêtre visible, progression et annulation. Décoder les bornes précisément ; ne pas utiliser une copie MP3 approximative pour la découpe finale.
6. Générer vers un fichier temporaire ; inspecter avec FFprobe puis décoder pour détecter une sortie illisible. Vérifier durée attendue et durée observée avec la tolérance issue du lot 1, tenant compte des trames audio/vidéo.
7. Promouvoir le MP4 et écrire le manifeste : identifiants, empreintes des sources, révisions, bornes, profils, outils, durée et date. Ne pas écraser une ancienne vidéo sans politique explicite de versionnement.

### Acceptation

Un MP4 réel pour chacun des cas suivants : ayah courte, plusieurs lignes, reprise complète choisie, portions assemblées, ayah sur plusieurs pages. Pour un cas multiscène, les transitions sont vérifiées à l’écoute. Chaque vidéo contient uniquement les images et passages choisis, sans son tronqué ni voisin ajouté par défaut.

## 12. Lot 9 — Exports en série et reprise après incident

### Travaux

1. Permettre de choisir une sourate, une plage d’ayat ou des compositions ; présenter prêtes, à revoir et non préparées. Ne pas créer de vidéos vides pour les ayat non annotées.
2. Figer un snapshot de chaque travail avec ses dépendances. Une modification ultérieure ne change pas en silence un export déjà en file.
3. Persister Pending, Running, Completed, Failed, Cancelled et Interrupted, avec erreurs et tentatives. Au redémarrage, reclasser les travaux interrompus et relancer le rendu depuis son début.
4. Limiter initialement à un encodeur simultané ; garder l’interface et la lecture utilisables. Ajouter arrêt de la file, annulation individuelle et nouvelle tentative.
5. Vérifier les dépendances et l’espace avant chaque rendu. Marquer les vidéos antérieures comme dépassées lorsque leurs sources sont corrigées ; conserver leur provenance.
6. Résoudre les collisions de noms par identifiants et versions, sans se fier uniquement aux noms arabes affichés. Nettoyer les temporaires orphelins de façon bornée au dossier de travail.

### Acceptation

Lot mixte contenant succès, source manquante et annulation ; arrêt brutal puis réouverture ; espace disque insuffisant ; composition modifiée en cours de file. Les travaux terminés restent identifiables et ne sont pas réexportés sans raison. Un bilan indique exactement les réussites et échecs.

## 13. Lot 10 — Sauvegarde, installateur obligatoire et autonomie

### Travaux

1. Implémenter une sauvegarde cohérente : bloquer les mutations utiles le temps du snapshot, utiliser l’API de sauvegarde SQLite, puis copier les médias référencés et vérifier le manifeste. Ne pas copier seulement le fichier SQLite ouvert en ignorant son WAL.
2. Proposer une sauvegarde complète via un bouton arabe et un choix de destination, avec estimation de taille et progression. Ne pas multiplier les modes de sauvegarde dans la V1.
3. Tester la restauration vers un nouveau dossier, avec contrôle d’empreintes, chemins relatifs et ouverture de session. Placer les archives hors de leur propre périmètre de sauvegarde.
4. Produire un **Setup.exe Windows x64**, avec script de fabrication reproductible, publication autonome `self-contained` du runtime .NET 9, FFmpeg/FFprobe et polices. L’assistant est arabe RTL, avec raccourcis et désinstallation standard ; aucun SDK ou outil à installer manuellement. Inclure le pack du Coran ou le fournir comme compagnon détecté automatiquement. Documenter versions et notices et vérifier les conditions de redistribution des packs. Un ZIP ou une commande de lancement ne remplace pas cet installateur.
5. Stocker les espaces utilisateur hors des répertoires de programme. Tester installation, réinstallation/mise à jour et désinstallation sans perte des projets et médias. Ne pas ajouter de mise à jour automatique dans la V1.
6. Tester une machine Windows de référence sans SDK, PostgreSQL, site web ni FFmpeg préinstallé. Après acquisition des ressources, couper le réseau et refaire le parcours complet.
7. Vérifier écran réduit, DPI 100/150/200 %, RTL, navigation clavier, chemins arabes et longs, disque externe déconnecté et ouverture simultanée du même espace.

### Acceptation

Setup.exe livré et testé sur machine propre ; assistant arabe RTL, premier lancement guidé, import, annotation, export, sauvegarde et restauration réussis sur la configuration déclarée. Les configurations non testées ne sont pas annoncées comme garanties.

## 14. Lot 11 — Recette finale et transmission

### Matrice minimale de recette

| Domaine | Contrôle indispensable | Preuve conservée |
|---|---|---|
| Corpus | Totaux, unicité, géométrie, empreintes, aucune modification des sources | Rapport automatisé d’import |
| Recherche | Minimum de lettres, références numériques, mots contigus, toutes les pages de résultats | Tests et captures de cas réels |
| Affichage | Texte arabe lié, chiffres latins, sélection correcte | Revue visuelle et références utilisées |
| Audio | Source longue/VBR, précision des bornes, changement de récitateur | Mesures du lot 1 et tests d’intégration |
| Studio | Début modifiable, enchaînement, cible manuelle, reprises, annulation | Scénario enregistré et export d’annotations |
| Images | Aucun voisin, aucun signe coupé, positions préservées, plusieurs pages | Comparatifs avant/après revus |
| Vidéo | Audio complet choisi, image lisible, durée et scènes correctes | MP4, manifestes et rapport FFprobe |
| Persistance | Crash, migration, invalidation des versions et reprise de file | Tests de récupération |
| Sauvegarde | Sauvegarde complète puis restauration dans un autre dossier | Rapport de restauration |
| Windows | Setup.exe, runtime .NET 9 inclus, mise à jour/désinstallation préservant le travail, hors ligne et DPI | Fiche de machine et procès-verbal de recette |
| Simplicité | Utilisateur non informaticien, interface et installateur arabes RTL, aucune configuration technique requise | Parcours observé de la sourate au MP4 |

### Recette de simplicité obligatoire

Faire tester par une personne non informaticienne : installer, choisir Al-Baqara et le récitateur proposé, télécharger ou ouvrir son audio, sélectionner une ayah, marquer début/fin, vérifier l’image, créer une vidéo et retrouver le fichier. Elle doit pouvoir terminer avec les indications de l’interface, sans saisir de millisecondes brutes, de chemin FFmpeg, de codec ou de manifeste. Noter les hésitations et simplifier les écrans avant livraison. Tester aussi fermeture/réouverture avec reprise automatique et absence de perte du travail.

### Documentation à produire pendant le développement

- Guide d’installation et d’import des ressources.
- Guide utilisateur arabe illustré : choisir la source, poser le début, terminer une ayah, corriger, gérer une reprise, vérifier l’image, exporter.
- Guide de sauvegarde/restauration et dépannage des fichiers manquants.
- Documentation technique : schéma, migrations, formats d’échange, algorithme des zones et horloge audio.
- Catalogue des dépendances et notices ; liste de limites connues et incidents non résolus.
- Rapport de livraison indiquant tests réellement exécutés, versions et cas restants. Aucun test non exécuté ne doit être présenté comme réussi.

### Définition de la V1 terminée

L’utilisateur non informaticien peut, après installation par Setup.exe incluant le runtime .NET 9, utiliser une interface arabe RTL guidée pour parcourir tout le corpus importé, gérer plusieurs collections de récitations, annoter précisément une ayah, vérifier/corriger son image, produire un MP4 puis un lot de MP4, et restaurer son travail depuis une sauvegarde.

**Livrer le logiciel ne signifie pas avoir produit ni validé les 6 214 vidéos.** La préparation éditoriale des bornes, des reprises, des masques et des scènes demeure un travail humain progressif. Le tableau de progression doit montrer les ayat non préparées, à revoir et prêtes, sans inventer de validation globale.

## 15. Risques à suivre et réponses prévues

| Risque | Détection | Réponse |
|---|---|---|
| Zones de clic trop larges pour une extraction | Prototype et superposition | Masques corrigeables ; revue avant export |
| Décalage de lecture/export MP3 | Mesure sur signaux connus et VBR | Horloge commune en échantillons, décodeur qualifié |
| Confusion entre éditions ou numérotations | Clés d’édition et contrôle des références | Import rejeté ou migration explicite, jamais correspondance implicite |
| Police imparfaite du texte candidat | Revue des cas de liaison et signes | Police compatible ; vidéo toujours issue des images |
| Ayah longue illisible à l’écran | Prévisualisation à résolution cible | Scènes manuelles et temps vérifiés |
| Reprise partielle prise pour une ayah entière | Type et validation de composition | Assemblage explicite, écoute finale |
| Modification d’une source après annotation | Empreinte et révision | Dépendances à revoir ; historique conservé |
| Perte de travail | Tests de crash et restauration | Transactions, snapshots cohérents, sauvegardes |
| Projet dépendant du site existant | Recette sans serveur/réseau | Packs locaux et contrats autonomes |

## 16. Consigne de démarrage pour le prochain intervenant

Lire les deux documents, respecter les exigences confirmées (.NET 9, Setup.exe, arabe RTL, simplicité), vérifier l’état réel du dépôt, puis exécuter le lot 0 et le prototype du lot 1. Présenter des fichiers PNG/MP4 réels et les mesures de précision avant de généraliser. Consigner les écarts entre conception et constat dans ces documents. Continuer ensuite lot par lot avec les critères de recette indiqués, sans réintroduire les أوجه ni reconstruire le texte des vidéos avec une police.
