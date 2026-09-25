# Volet de lecture simplifié

- Un seul bouton lecture/pause, barre de progression relative au passage et compteur de temps.
- Le premier passage disponible est sélectionné sans démarrage automatique. Plusieurs passages : choix dans une liste sans boutons de lecture supplémentaires.
- Filtre des أوجه conservé, sans substitution si le وجه demandé est indisponible.
- Suppression du lecteur de sourate complète, du lien vers l’ancienne page indépendante, du bouton d’effacement et des commandes de téléchargement du volet.
- Changement d’ayah/وجه : arrêt, retrait de l’ancienne source, commandes désactivées si aucun passage n’existe. Erreurs de chargement signalées.
- Pour la revue explicite, une source complète reste utilisée en interne avec les bornes du passage. Aucun lecteur complet supplémentaire n’est affiché. La consultation publique n’essaie plus de récupérer des brouillons implicitement.
- Espace utilise le même contrôle de lecture ; les commandes et champs focalisés conservent leur comportement natif.

Vérification Edge sur un vrai fichier publié : démarrage, pause, reprise, déplacement en fin, arrêt et redémarrage, changement vers une ayah sans audio (aucune ancienne source conservée). Un seul élément audio, sans contrôles natifs concurrents ; aucune erreur JavaScript. Build réussi. Capture : `artifacts/player-single-control.png`.

## Modes de lecture

Sélecteur arabe « آية بآية » / « قراءة متواصلة », préférence conservée par onglet. Le premier mode s’arrête au terme du passage sélectionné. Le second enchaîne les ayat complètes publiées, dans l’ordre du corpus d’images MARW, jusqu’à la fin du Mushaf. Les répétitions de référence sur deux pages ne sont pas relues comme des ayat différentes.

L’enchaînement conserve le وجه du passage courant, y compris « non défini » lorsqu’aucune qualification n’existe. Une ayah absente, un وجه indisponible, une portion seule ou une erreur de chargement arrête la lecture : aucune ayah n’est sautée. La prévisualisation privée reste en mode individuel.

Le changement de page remplace l’image, les régions, les boutons d’ayat et les liens de navigation, en conservant le même élément audio. L’URL et la sélection sont mises à jour. Un choix manuel ou le passage au mode individuel annule une transition en cours. Les restrictions d’autoplay du navigateur peuvent demander un clic supplémentaire ; un message l’indique.

Vérification reproductible : `node scripts/check-continuous-playback.cjs` (site local lancé et audio publié pour 2:1). Contrôle du vrai audio pour l’arrêt individuel ; réponses audio de test interceptées uniquement dans le navigateur pour le passage 2:4 → 2:5/page 3, reprise automatique et arrêt au وجه absent. Le fichier MP3 et les pages MARW sont réels ; aucune écriture de données. Vérification serveur de 2:285 → 3:1, fin 114:6, référence invalide.
