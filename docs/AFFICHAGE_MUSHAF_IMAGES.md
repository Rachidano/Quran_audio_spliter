# Présentation du Coran uniquement en images — 25 septembre 2026

Décision utilisateur : conserver comme présentation de lecture uniquement le Mushaf en images accessible via `/mushaf/warsh/page/2`. Cette route est un alias du lecteur MARW actuel ; aucune image n’a été remplacée ou générée.

- `/surah/{number}` ouvre la première page correspondante du Mushaf.
- `/quran/{surah}/{ayah}` ouvre la page portant cette référence MARW, avec sa sélection et l’éventuel filtre de وجه conservés. Une référence inexistante retourne 404 ; aucune translation depuis la numérotation historique.
- Aucun texte coranique recomposé dans les pages de sourate, d’ayah, de recherche ou dans le studio. Celui-ci conserve les références et l’image MARW avec ses régions.
- Les API de lecture renvoient le champ TextWarsh vide ; aucun repli sur le texte historique ni chargement du texte candidat. Les métadonnées et références restent disponibles.
- Recherche par référence uniquement (exemple `2:4`). La recherche par mots est suspendue tant qu’aucun texte n’est qualifié.
- Les données historiques et le corpus candidat sont conservés en base, sans modification ni suppression ; ils ne sont pas exposés comme texte de lecture. Les enregistrements audio et annotations restent intacts.
- Les mentions de texte candidat affiché dans les audits précédents décrivent un état désormais remplacé par cette décision.

Vérifications : 2 tests unitaires du lecteur, 12 tests HTTP des pages publiques et de la revue réussis. Edge : redirection de Baqara, chargement réel de l’image, recherche sans texte de verset, absence de texte dans le studio et l’API, maintien du filtre de وجه sans substitution lorsqu’il est indisponible.

Cette décision ne vaut pas qualification savante automatique de toutes les images ou des délimitations : elle fixe la présentation choisie par l’utilisateur et retire le texte numérique non qualifié.
