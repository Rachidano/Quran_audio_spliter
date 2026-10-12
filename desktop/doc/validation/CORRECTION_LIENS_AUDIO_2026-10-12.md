# Configuration des 114 liens audio par récitateur

12 octobre 2026 — correction du parcours demandé par l’utilisateur. Cette intervention ne constitue pas une nouvelle certification globale de l’application.

## Constat sur les modifications précédentes

Les paramètres proposaient un nom de récitateur, un chemin de fichier JSON, un dossier local et un modèle d’URL. Cela ne répondait pas à la demande de gérer directement les 114 adresses. Le modèle de secours omettait les noms de sourates présents dans les véritables URL de l’inventaire.

Le lecteur recherchait aussi des fichiers dans plusieurs dossiers, y compris ceux d’autres récitateurs. L’import attribuait 30 secondes lorsqu’il ne parvenait pas à analyser l’audio. L’annotateur pouvait réaffecter une source manquante à un autre récitateur. Ces comportements ont été retirés du parcours corrigé.

## Résultat livré

- Fenêtre arabe RTL **القراء وروابط التلاوات**, ouverte depuis les paramètres ou le bouton **روابط السور** du studio.
- Tableau de **114 sourates** : numéro, nom, URL directement modifiable, état local et bouton **تنزيل**.
- **الشيخ صابر الحكم** par défaut sur un nouvel espace. Les 114 URL exactes proviennent de l’inventaire du projet web et sont embarquées dans l’application ; aucun fichier JSON à fournir ou éditer.
- Ajout d’un récitateur, modification de son nom et de ses liens, choix du récitateur par défaut. Un nouveau récitateur reçoit 114 lignes vides ; aucun lien de Saber ne lui est attribué silencieusement.
- Sauvegarde SQLite : les changements, liens volontairement vides et préférences sont conservés au redémarrage. Les colonnes techniques et identifiants ne sont pas exposés.
- Téléchargement direct depuis l’adresse enregistrée, progression, annulation et message d’erreur. Le bouton de la ligne sauvegarde d’abord les modifications pour utiliser le lien visible.
- Sélecteur de récitateur du studio alimenté depuis ce catalogue. Lecture et export utilisent la source identifiée en base ; pas de recherche d’un audio de secours appartenant à un autre récitateur.
- FFprobe lit la durée, la fréquence et le nombre de canaux réels. Un contenu invalide ne reçoit plus de durée fictive et ne remplace pas l’ancien audio.
- Les fichiers sont isolés par collection et empreinte. Un audio déjà lié à des annotations ou travaux d’export ne peut pas être remplacé silencieusement par un contenu différent.
- L’ancien service de paramètres audio JSON et ses champs d’interface ont été retirés. Les anciens fichiers utilisateur ne sont pas supprimés ; les collections déjà enregistrées en SQLite restent conservées.

Le lot d’exports utilise le récitateur configuré par défaut. L’export individuel utilise celui sélectionné dans le studio. La généralisation de la recette audio/vidéo reste distincte de cette correction.

## Vérifications réellement effectuées

1. Compilation .NET 9 et suite complète : **85 tests réussis**, dont 12 cas ciblés dans `RecitationCatalogTests`. [Résultats finaux](audio-catalog-2026-10-12/full-suite-final.trx).
2. Cas ciblés : 114 liens intégrés, persistance d’une modification et d’un lien vide, ajout/renommage/default, conservation d’une collection existante, rejet d’URL invalides sans sauvegarde partielle, téléchargement du lien modifié, métadonnées audio réelles, protection de l’audio précédent, isolation des récitateurs, annulation et refus de substitution d’une source d’annotation.
3. Comparaison automatique des 114 URL intégrées avec `data/reference/archive_org_114_inventory.json` : correspondance exacte.
4. Téléchargement réel d’Al-Fatiha par le nouveau service : **1 129 009 octets**, durée FFprobe **67,325567 secondes** ; SHA-1 `b8b8e9d1843379a7294126b0b451c3976a14c201`, identique à l’inventaire web. [Résultat](audio-catalog-2026-10-12/smoke-run/download-result.txt).
5. Création d’un espace isolé sans configuration audio préalable ; rendu WPF hors écran de la fenêtre, contrôle de 114 lignes, du récitateur sélectionné et du RTL. [Capture inspectée](audio-catalog-2026-10-12/smoke-render/audio-sources.png).
6. Publication Release autonome Windows x64. L’installateur reconstruit est indiqué dans le journal de livraison associé après compilation Inno Setup.

Les tests de l’ancien service JSON ont été adaptés au nouveau catalogue ; les tests d’annotation sélectionnent maintenant explicitement le récitateur de leur fixture, au lieu de dépendre d’une substitution implicite. Certains anciens tests du projet peuvent encore retourner lorsque leurs ressources locales sont absentes : le total vert n’est pas une preuve de recette exhaustive.

## Limites de validation

Les 114 fichiers n’ont pas tous été téléchargés. L’accessibilité réseau réelle a été vérifiée sur Al-Fatiha ; les autres liens sont repris exactement du catalogue web et peuvent être modifiés si leur hébergement change. L’installation sur une machine Windows vierge et une recette complète par un utilisateur non informaticien ne sont pas exécutées dans cette intervention. La capture WPF hors écran n’est pas une simulation complète des interactions souris/clavier.

Les autres points du rapport d’audit initial doivent être suivis séparément ; les affirmations de clôture générale de J017 ne sont pas revalidées par cette correction ciblée.
