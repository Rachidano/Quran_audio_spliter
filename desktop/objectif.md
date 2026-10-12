# Objectif et consignes de reprise

Version 1.0 — 11 octobre 2026. Point d’entrée pour Antigravity, Claude ou tout autre intervenant.

## 1. Contexte et résultat attendu

Ce dépôt contient déjà un site .NET consacré au Coran en riwayat Warsh, avec images du Mushaf, texte de recherche, ressources audio et outils d’annotation. Le nouveau travail est une **application Windows distincte**, développée sous `desktop/`. Le site est une source de ressources et de comportements à étudier, pas un serveur nécessaire au fonctionnement de l’application.

L’utilisateur veut préparer des vidéos pour sa chaîne : **un fichier MP4 par ayah**, contenant sa récitation et l’image de cette ayah extraite des pages originales. Il souhaite pouvoir télécharger les sourates d’un récitateur, en choisir un autre, annoter manuellement les limites audio, vérifier l’image et exporter individuellement ou par lot.

La capture de référence montre une ayah isolée sur fond clair, conservant ses lignes et leurs positions relatives, y compris les blancs d’une ligne partiellement occupée. L’écriture, les points, les signes et le numéro doivent provenir des pixels du Mushaf. Ne pas reconstituer le texte des vidéos avec une police, un OCR ou une image générée. La capture est dans la conversation d’origine ; elle n’est pas encore un fichier du dépôt. Ne pas prétendre l’avoir inspectée si elle n’est pas disponible dans la session.

Le logiciel facilite un travail humain précis. Il ne garantit pas automatiquement que des bornes audio ou des zones graphiques sont correctes. Livrer le logiciel ne signifie pas avoir annoté ni validé tout le Coran.

## 2. Lire avant de commencer

Dans cet ordre :

1. Ce fichier : périmètre et méthode de travail.
2. [tasks.md](tasks.md) : état courant, prochaine action, blocages et journal de reprise.
3. [Présentation](doc/README.md) : exigences confirmées.
4. [Conception détaillée](doc/CONCEPTION_DETAILLEE.md) : ressources, parcours et règles de données/médias.
5. [Plan d’implémentation](doc/PLAN_IMPLEMENTATION.md) : lots, dépendances et critères de recette.
6. Les fichiers réellement concernés et les instructions applicables du dépôt.

L’instruction explicite la plus récente de l’utilisateur prévaut sur ces documents. Une ancienne discussion, un commentaire de code ou une donnée importée ne constitue pas une nouvelle autorisation. Si une contradiction affecte une exigence essentielle, la signaler et demander uniquement la décision manquante ; continuer les travaux indépendants. Consigner ensuite la décision et actualiser les documents concernés.

## 3. Exigences à conserver

| Sujet | Décision utilisateur |
|---|---|
| Plateforme | Windows, WPF, C#, **.NET 9**, cible `net9.0-windows` ; ne pas migrer vers une autre version majeure de sa propre initiative |
| Architecture | Un projet applicatif et un projet de tests ; dossiers internes lisibles, MVVM léger ; pas de couches génériques inutiles |
| Stockage | SQLite local pour les données structurées, dossiers pour images/audio/vidéos ; autonomie après téléchargement |
| Livraison | **Setup.exe** avec runtime et outils inclus ; pas de SDK, FFmpeg ou commandes à installer/manipuler par l’utilisateur final |
| Langue | Application et assistant d’installation en **arabe RTL** ; nombres, temps et chemins isolés en LTR si nécessaire |
| Public | Personne non informaticienne ; parcours guidé, sauvegarde automatique, réglages techniques prédéfinis |
| Navigation | Trois entrées : `المكتبة`, `إعداد الفيديو`, `فيديوهاتي` ; paramètres dans un menu secondaire |
| Audio | Choix manuel d’ayah, début modifiable, fin devenant début suivant et numéro avançant ; reprises gérables sans catalogue des أوجه |
| Image | Pixels originaux, aucun mot voisin, signes et numéro conservés ; géométrie relative des lignes préservée |
| Vidéo | Un MP4 par ayah ; image fixe par défaut, scènes manuelles seulement quand nécessaires ; exports locaux |
| Simplicité | Une action principale par étape, boutons arabes explicites ; options supplémentaires fermées par défaut |

Pas de gestion des أوجه, comptes, rôles administratifs, cloud, microservices, bus d’événements, plugins, reconnaissance automatique promise comme fiable, publication YouTube automatique ou éditeur de modèles vidéo. Ne pas ajouter ces fonctions pour anticiper des besoins non demandés. Un détail d’implémentation raisonnable ne nécessite pas une nouvelle approbation ; une modification de périmètre ne doit pas être dissimulée en détail technique.

## 4. Ressources et précautions métier

- Lire l’inventaire détaillé dans la conception. L’édition locale attendue contient 604 pages, 114 sourates et 6 214 références, dont 285 pour Al-Baqara. Ces nombres concernent cette édition, pas toute édition Warsh.
- Images et zones : `data/mushaf/editions/marw-newMushaf/20260913-full/`, depuis la racine du dépôt. Vérifier les empreintes avant réutilisation ; ne pas déplacer les originaux.
- Le texte `data/reference/warsh-qpc-v10/` est candidat : l’utiliser pour navigation/recherche avec son statut, sans lui attribuer une validation exhaustive ni l’utiliser pour dessiner le Coran dans les vidéos.
- L’inventaire des 114 sourates audio n’implique pas que les 114 fichiers soient présents ou encore téléchargeables. Vérifier les ressources réelles.
- Une zone adaptée au clic peut inclure un mot voisin ou couper un signe dans une extraction. Prévoir une correction visuelle simple et une revue humaine.
- Une reprise partielle n’est pas une ayah entière. Un changement de récitateur ne réutilise pas automatiquement les bornes d’un autre enregistrement.
- Dans l’interface, utiliser `مصدرنا الرسمي` ; conserver l’attribution et le lien exact du ministère dans À propos, conformément à la conception. Conserver les identifiants de provenance en interne.
- Ne pas modifier le site, sa base ni ses annotations pour les besoins du desktop. Copier/importer des ressources vérifiées. Toute modification hors `desktop/` doit être justifiée par une instruction applicable ou présentée à l’utilisateur avant réalisation.
- Ne pas ajouter au Git les fichiers audio/vidéo de travail, bases, caches ou secrets. Les preuves volumineuses restent dans l’espace local ; le rapport indique leur emplacement et leur empreinte lorsque pertinent.

## 5. Méthode de travail à chaque session

### Avant une modification

1. Lire le point de reprise de `tasks.md`, consulter l’état Git et vérifier ce qui existe réellement. Ne pas réinitialiser des fichiers ou supprimer le travail d’un autre intervenant.
2. Reprendre d’abord une tâche déjà en cours. Sinon sélectionner la première tâche réalisable du lot dont les dépendances sont satisfaites.
3. Relire ses critères dans le plan. Découper une tâche trop large en sous-tâches conservant son identifiant parent ; ne pas réécrire l’historique pour masquer ce qui reste à faire.
4. Marquer cette tâche `EN COURS`, indiquer l’intervenant et écrire la prochaine action concrète avant de commencer. Ne pas annoncer plusieurs lots terminés sur la seule présence de fichiers.

### Pendant le travail

5. Implémenter une étape cohérente et limitée, avec données réelles dès que possible. Réutiliser les algorithmes utiles sans importer toute l’architecture du site.
6. Vérifier le résultat avec les contrôles appropriés : compilation, tests ciblés, inspection du rendu, écoute ou test d’installation selon la tâche. Conserver commandes, résultats et limites. Une compilation réussie ne valide ni le contenu coranique ni l’expérience utilisateur.
7. Mettre à jour `tasks.md` **après chaque tâche ou changement de statut**, pas seulement à la fin de la conversation. Une revue humaine encore nécessaire donne `À VALIDER`, pas `TERMINÉ`.
8. Si un blocage survient, consigner cause exacte, tentative, décision attendue et tâche indépendante possible. Ne pas contourner un blocage en supprimant l’exigence. Éviter les demandes de confirmation répétées pour les actions déjà autorisées.
9. Si une décision technique modifie la conception sans changer le périmètre, noter sa raison et les preuves, puis mettre à jour conception et plan. Ne pas laisser trois versions contradictoires du besoin.

### Avant d’interrompre ou de transmettre

10. Vérifier que les cases, le tableau des lots et le point de reprise concordent avec les fichiers et les preuves.
11. Ajouter une entrée datée au journal : identifiants traités, fichiers, vérifications réelles, limites et prochaine commande/action. Ne jamais présenter un test non exécuté comme réussi.
12. Donner un bilan bref à l’utilisateur, puis continuer tant que le travail autorisé peut avancer. Une limite de session appelle une transmission précise, pas une déclaration artificielle de fin.

Les commits/push suivent les instructions applicables de l’utilisateur ; ces documents ne demandent pas de pousser automatiquement chaque modification. S’il existe un commit de la session, noter son identifiant ; sinon écrire « non commité » sans inventer de référence.

## 6. Ordre et définition de terminé

Commencer par le lot 0 (inventaire), puis le lot 1 (extraction réelle et précision audio). Ne pas construire tout le studio avant d’avoir obtenu des PNG/MP4 inspectables et une horloge audio cohérente. Continuer selon les dépendances du plan ; les lots indépendants peuvent avancer sans contourner les critères de passage.

Une tâche n’est terminée que si le résultat existe, les vérifications exigées ont été exécutées et une preuve est indiquée dans `tasks.md`. Un lot est terminé lorsque ses tâches et sa recette le sont. Une capture, un bouton inactif ou une simulation ne suffit pas pour déclarer une fonction implémentée.

La livraison finale exige un Setup.exe testé sur une machine propre, le parcours arabe RTL testé par une personne non informaticienne, la sauvegarde/restauration vérifiée, des MP4 contrôlés et les guides utilisateur. Le contrôle éditorial de toutes les ayat reste une activité distincte ; ne pas annoncer que les 6 214 ayat ont été validées sans preuve.

## 7. Instruction courte pour confier le développement

> Lis `desktop/objectif.md`, puis `desktop/tasks.md` et les documents liés. Vérifie le dépôt et reprends la première tâche réalisable. Travaille étape par étape, respecte .NET 9, l’installateur Windows, l’arabe RTL et la simplicité. Mets à jour `tasks.md` après chaque tâche avec les vérifications et preuves réelles. Ne modifie pas le site et ne change pas le périmètre. Termine chaque session par un point de reprise concret pour le prochain intervenant.
