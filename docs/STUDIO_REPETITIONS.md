# Répétitions et autres أوجه dans le studio

Le bloc « تكرار / وجه آخر », placé dans les options avancées repliées par défaut, permet de choisir l'ayah cible indépendamment de l'ayah courante, de distinguer l'ayah entière d'une portion et de choisir un wajh indépendant de la lecture principale.

## Annoter une répétition

1. Ouvrir les options avancées, puis sélectionner l'ayah répétée dans le bloc « تكرار / وجه آخر ».
2. Choisir « الآية كاملة » ou « جزء من الآية ». Ce choix est obligatoire.
3. Choisir le wajh s'il est identifié. Sinon laisser « لم يُحدَّد الوجه » et, si pertinent, choisir « يبدو وجهًا آخر — للمراجعة ». Cette observation n'est pas une attribution validée.
4. Préciser en note la portion répétée lorsque nécessaire.
5. Ajuster le début avec F7 ou le champ de début. Placer le curseur audio à la fin et utiliser « حفظ المقطع المُعاد » / F9.

La fin devient le prochain début. Le numéro de l'ayah principale ne change pas. Le wajh principal n'est pas modifié. La portée doit être choisie de nouveau pour chaque répétition ; le wajh de répétition reste sélectionné et doit être vérifié.

Un passage antérieur de la même ayah est lié automatiquement lorsqu'il existe avant le début choisi. En son absence, le passage reste rattaché à la référence canonique choisie. Le serveur vérifie la référence et la sourate du fichier source.

## Données et lecture

- Répétition complète : type FullAyah ; peut servir à la lecture continue après revue, préparation audio et publication.
- Répétition partielle : type PartialAyah ; disponible en lecture individuelle après publication, exclue de la lecture continue.
- Ancienne répétition sans portée explicite : type Repetition, conservé sans présumer qu'elle est complète.
- Observation et portée enregistrées dans ObservationJson ; notes descriptives visibles dans le tableau de révision.
- Le bouton de capture partielle utilise maintenant le parcours canonique et garde le numéro courant. Le bouton de continuation utilise Continuation et avance le numéro ; il ne crée plus une fausse ayah complète.
- Le lecteur filtre les morceaux publiés selon le wajh. En mode continu, si aucun passage complet n'est disponible pour ce wajh, il s'arrête. Les portions et continuations ne sont pas assemblées automatiquement.

Le surlignage porte toujours sur l'ayah entière. La note de portion n'est pas une sélection géométrique de mots. Les anciennes annotations ne sont ni reclassées ni publiées automatiquement.

## Vérification

Tests AnnotationServiceTests : conservation du numéro suivant, portée complète/partielle, observations, refus des références invalides et types des portions/continuations. Contrôle navigateur avec requêtes interceptées : cible 2:4 alors que l'ayah courante est 2:5, wajh indépendant, bornes audio et filtrage de la lecture continue. Aucune annotation de test ajoutée à la base principale.

Si F9 est utilisé sans portée sélectionnée, les options avancées s’ouvrent et le champ de portée reçoit le focus. Aucun passage n’est enregistré avant un choix valide.
