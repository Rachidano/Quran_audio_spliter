# Reprises audio : Al-Baqara

## État du 13 septembre 2026

Atelier local : <http://127.0.0.1:5098/mushaf/audioreview>.

Le signalement utilisateur concerne la fin de l’ayah 4. La référence de travail est 2:4 dans le corpus MARW affiché, finissant par المفلحون. Les anciens repères de `data/reference/audio_baqarah_1_20_segments.json` suivent une autre numérotation : ils ne qualifient pas cette reprise.

**Les horodatages exacts, les mots repris et l’éventuel wajh ne sont pas encore confirmés à l’écoute.** Les tests techniques ne constituent pas une validation de la récitation.

## Source conservée

- Fichier : `data/audio/sources/002/original.mp3`, 247 766 723 octets.
- Durée mesurée par FFprobe : 15 482,182689 secondes.
- SHA-256 : `7dec4d89cf366aecd3e0e5751ed4f7a834bb2017268a5355e045bc702956b90c`.

`scripts/prepare-baqarah-audio-review.py` produit uniquement une amplitude des six premières minutes. Un silence n’est pas une limite d’ayah. L’original reste intact.

## Utilisation

Écouter l’original, marquer le début et la fin du passage initial et l’ajouter au brouillon. Marquer ensuite la portion répétée, choisir « Reprise à qualifier » et l’associer au passage initial. La lecture d’une plage, sa boucle et les vitesses réduites aident au repérage. Exporter les annotations JSON pour conserver une copie hors navigateur.

Les annotations sont locales au navigateur et liées à l’édition et au hash de l’audio. Elles restent Draft, avec référence PendingReview et wajh non qualifié. Elles ne sont pas importées dans la base ni publiées. Il reste à confirmer chaque limite à l’écoute, vérifier le rattachement selon MARW, puis faire qualifier le wajh avant validation éditoriale.

## Protection des occurrences

Le modèle existant permet plusieurs segments pour une ayah. La publication utilise désormais un nom propre à chaque segment : deux reprises sans wajh renseigné ne s’écrasent plus. Seule une ayah complète sans bornes de mots peut mettre à jour la récitation canonique. Les portions et reprises conservent leurs propres assets. Toute modification du contenu d’un segment retire son approbation ; rendu et publication nécessitent une approbation.

## Vérifications

Compilation réussie. 25 tests unitaires sélectionnés réussis, dont les cas de publication de plusieurs reprises et de refus des segments non approuvés. `scripts/check-baqarah-audio-review.cjs` vérifie les requêtes HTTP Range, la persistance, la modification, les relations entre passages, l’export Draft sans wajh, l’arrêt en fin de plage et l’absence de débordement mobile. Les plages synthétiques de test ne sont pas des annotations de récitation.
