# Proposition visuelle V4 — interface arabe RTL

Sept maquettes exploratoires produites avec l’outil de génération d’images intégré de ChatGPT/Codex. Elles proposent une direction visuelle à discuter, pas une interface implémentée ni un nouveau périmètre validé. Aucun code V3 n’a été changé pour cette proposition.

## Les écrans

| Image | Intention UX |
|---|---|
| [01 — Bibliothèque](01-bibliotheque.png) | Rechercher, télécharger, distinguer disponibilité audio et avancement de l’annotation, reprendre le travail. |
| [02 — Studio de découpage](02-studio.png) | Waveform lisible, une commande de lecture, début/fin éditables, image à côté, répétitions dans un volet replié. |
| [03 — Présentation vidéo](03-presentation.png) | Prévisualisation large, format et fond simples, signature en dehors de l’ayah, réglages secondaires regroupés. |
| [04 — Contrôle avant export](04-export.png) | Identifier exactement l’ayah incomplète, proposer sa correction et empêcher le démarrage de l’export. |
| [05 — Mes vidéos](05-videos.png) | Retrouver ses fichiers, les lire dans un seul lecteur et accéder aux actions secondaires par un menu discret. |
| [06 — Collections](06-collections.png) | Organiser les projets et annoncer clairement combien de fichiers distincts seront produits. |
| [07 — Sources audio](07-sources.png) | Configurer directement les 114 liens par récitateur, enregistrer et télécharger, sans manipulation JSON. |

## Direction proposée

- Navigation fixe à droite, surface de travail à gauche ; même ordre et mêmes icônes dans l’implémentation finale.
- Fond clair, texte sombre, vert pour l’action principale, ambre pour une revue nécessaire. La couleur est toujours accompagnée d’un texte ou d’un symbole.
- Espacement généreux, libellés courts et lisibles ; une action dominante par étape.
- Lecture des temps, formes d’onde et URL en LTR au sein de l’interface RTL.
- Séparer annotation, présentation et fichiers produits pour alléger le studio ; les liens entre ces vues doivent garder le contexte sélectionné.
- Conserver la sélection manuelle d’ayah, le début éditable et l’enchaînement automatique début/numéro.
- Conserver les validations métier V3 et la fidélité des choix. Une refonte visuelle ne doit ni ajouter une validation automatique ni choisir une prise à la place de l’utilisateur.

## Ce qu’il faut préciser avant développement

Les maquettes générées comportent de petites variations : icônes, libellés, décoration, boutons Windows et navigation. En particulier, l’image 04 a omis l’entrée du studio lors de la retouche ; le menu réel doit conserver toutes les entrées. L’image 06 doit disposer d’une action « إضافة مشروع » pour alimenter la collection.

L’ordre canonique du menu est : `المكتبة`, `استوديو التقطيع`, `إعداد الفيديو`, `فيديوهاتي`, `مجموعاتي`, puis `الإعدادات`. Les étapes sont `اختيار الآيات`, `المظهر`, `المعاينة`, `التصدير`.

Les durées, dates, compteurs et URL illustrent les emplacements : utiliser les données réelles de la base, pas les chiffres dessinés. Une URL vide dans les paramètres n’est pas « prête à télécharger » : état explicite à corriger dans l’interface réelle.

Les zones grises d’images sont volontaires : aucune écriture coranique n’a été générée. L’application doit afficher les pixels de ses véritables images sources, avec leurs proportions. Les exemples de séquences restent soumis à la règle d’ayat consécutives d’une même sourate et d’une même source audio.

Pour les raccourcis du studio, conserver ou faire valider explicitement le comportement existant de F7, F8 et Entrée ; ne pas modifier leur sémantique à partir d’un dessin. La navigation doit fonctionner aussi au clavier et rester lisible à différentes mises à l’échelle Windows.

La sélection partielle d’une collection ne permet pas de retirer une ayah au milieu d’un projet multi-ayat. On sélectionne des projets entiers ; toutes les ayat internes sont obligatoires. La suppression d’une collection conserve les médias et les annotations.

Cette série ne représente pas tous les états ni tous les dialogues. Après choix de la direction visuelle, préciser notamment : choix/revue des prises, édition des limites d’image, confirmation de jonction, progression/annulation, état vide et sauvegarde/restauration. Ce sont des compléments de conception, pas des fonctionnalités supprimées.

## Prompts

[Prompts complets de génération](PROMPTS.md). Les deux retouches ont ajusté le menu actif et les quatre positions de signature de l’écran 03, puis les libellés des étapes et le menu actif de l’écran 04. Les images sont des fichiers PNG locaux ; aucun appel de génération n’est nécessaire pour les consulter.
