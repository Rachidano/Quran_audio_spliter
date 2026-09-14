# Circuit intégré — 14 septembre 2026

## Utilisation

1. `/mushaf/awjuh` : ajouter un وجه, modifier nom, voie, description, source et état ; supprimer une entrée non utilisée ou archiver une entrée liée. Les données sont dans PostgreSQL, pas dans le fichier JSON de recherche. Le code reste stable. Le fichier embarqué ne sert qu’au bootstrap explicite et aux métadonnées scientifiques initiales.
2. `/mushaf/audioreview` : « استرجاع مقاطع النظام » charge les passages sauvegardés ; l’import JSON permet de reprendre un ancien export. Le bouton de sauvegarde du formulaire enregistre directement en base. En cas d’échec réseau ou de conflit, la copie locale est conservée et un message explique que la base n’a pas été mise à jour.
3. « معاينة على المصحف » ouvre le lecteur MARW avec les brouillons de l’ayah. Le lecteur joue les bornes de l’original : aucun besoin de découper physiquement le MP3 pour prévisualiser ou écouter une portion.
4. Après vérification humaine du rattachement MARW, cocher la confirmation et publier le passage. Un وجه sélectionné doit être approuvé dans le référentiel. Le lecteur normal ne propose que les passages publiés dont le وجه est approuvé, ou ceux sans attribution de وجه. Le filtre regroupe les occurrences par وجه ; chaque reprise garde son temps et son lien.

Les cinq passages du dernier export utilisateur ont été importés en Draft, sans publication ni certification de leurs références. Ils sont récupérables depuis l’annotateur. L’intervalle non annoté reste absent.

## Données et cohérence

- `Awjuh` partagé avec l’ancien éditeur : nouveaux champs RouteCode, ReviewStatus, SourceCitation, DefinitionJson et Version. CRUD avec contrôle de version. Une modification retire les publications/approbations des occurrences liées. Une suppression liée est refusée ; l’archivage est disponible.
- `MushafPassages` : identifiant stable du JSON, édition MARW, clé et SHA du fichier original, référence d’ayah propre à cette édition, temps, type, lien de reprise, clé étrangère WajhId, observations, état et version.
- Les anciens AudioSegments utilisent les anciens AyahId : aucun rapprochement automatique avec les références MARW n’a été effectué. L’ancien éditeur partage le référentiel, tandis que les nouvelles annotations se prévisualisent et se publient par le circuit MARW.
- Import transactionnel : empreinte réelle de l’audio, durée FFprobe, références du corpus, codes de wajh actifs, contexte de sourate, liens des reprises et versions contrôlés avant sauvegarde. Réimporter un même ID modifié sans sa version serveur retourne un conflit. Charger d’abord la version serveur pour éditer.
- Sauvegarder un passage inchangé conserve son état. Modifier son contenu le remet en Draft. Les versions empêchent l’écrasement silencieux d’un travail concurrent.

## Mise en service

Migration EF `20260914102312_IntegratedAwjuhAndMushafPassages` appliquée. Bootstrap des 14 propositions effectué une fois par `--initialize-system`. Ne pas relancer ce bootstrap après des suppressions volontaires : il réinsère les codes du fichier initial s’ils manquent. Le démarrage normal ne les recrée pas.

Binaire actif : `artifacts/marw-system-final/Quran.Web.dll`, port 5098. La navigation donne accès au référentiel et à l’annotateur. Les anciennes pages de test restent accessibles par leur URL.

## Validation et limites

18 tests unitaires ciblés réussis. `scripts/check-integrated-system.cjs` vérifie réellement PostgreSQL et le navigateur : CRUD temporaire, conflits de version, suppression liée refusée, import, édition avec sauvegarde automatique, prévisualisation, arrêt aux bornes, refus de publication d’un wajh non approuvé, publication, maintien de la publication si inchangé et invalidation après modification du référentiel. Ses données temporaires sont supprimées en fin de test.

L’intégration concerne le site interne. Les contrôleurs d’administration du projet n’ont toujours pas de séparation authentifiée des rôles ; la prévisualisation des brouillons utilise un paramètre explicite, pas une autorisation utilisateur. Avant ouverture publique, il faut appliquer cette séparation à l’ensemble des API d’administration. Le corpus des awjuh reste incomplet et les contraintes savantes ne sont pas exécutées automatiquement. Les portions sont surlignées au niveau de l’ayah, pas mot par mot. La lecture d’un passage s’arrête à sa fin ; ce n’est pas encore une lecture continue synchronisée de toute la sourate.
