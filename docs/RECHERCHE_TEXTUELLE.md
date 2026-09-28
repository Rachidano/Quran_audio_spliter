# Recherche textuelle provisoire — 26 septembre 2026

La recherche accepte une expression arabe (avec ou sans tashkīl) ou une référence telle que `2:4` / `٢:٤`. À la demande de l’utilisateur, les résultats textuels présentent désormais le texte intégral du candidat pour identifier l’ayah avant de cliquer. Le lecteur reste en images MARW.

L’index en mémoire est dérivé exclusivement du corpus candidat `qpc-warsh-v10-candidate`, joint aux références MARW existantes. Les numéros de page proviennent de MARW. Il expire après deux minutes ; aucune donnée source n’est réécrite. Les anciennes Ayahs ne servent pas de repli.

La comparaison ignore le tashkīl et le tatweel, rapproche certaines formes de lettres et de hamza, et teste les formes avec/sans alif suscrit mot par mot. Les mots d’une expression doivent se suivre dans le même ordre ; chaque mot doit correspondre à un mot entier pour une expression de plusieurs mots (ainsi « الله لا » ne correspond pas à « الله الذين »). Une recherche portant sur un seul mot accepte les correspondances partielles. Ce n’est pas une recherche morphologique ni une garantie d’exhaustivité.

Les résultats textuels exposent la sourate, l’ayah, la page, le caractère provisoire (`IsProvisional`) et le texte candidat original (`TextWarsh`), sans transformation de son orthographe. `MatchedText` reste vide. Un résultat ouvre l’image du Mushaf avec la référence sélectionnée. La recherche exacte par numéro n’est pas signalée comme provenant du texte candidat.

Interface : champ de recherche sur l’accueil et dans la barre de navigation, avertissement explicite pour les recherches textuelles, tous les résultats accessibles par pagination de 20 entrées avec nombre total et navigation première/précédente/suivante/dernière. L’API renvoie toutes les correspondances par défaut ; un paramètre limit explicite reste possible. Les requêtes sont limitées à 200 caractères et doivent contenir au moins trois lettres après normalisation (espaces, chiffres, tashkīl et tatweel ne comptent pas). Les références telles que 2:4 restent acceptées.

La qualification intégrale du candidat reste à effectuer. Des différences orthographiques ou de délimitation peuvent entraîner des résultats manquants ou mal associés. Le texte aperçu porte la mention arabe « نص مساعد للبحث — قيد المراجعة » ; il ne remplace pas l’image de référence.

Vérifications : tests unitaires des variantes arabes, des chiffres arabes, de l’ordre de la phrase, des références MARW, du maintien des textes en base et de l’absence de texte dans les DTO de lecture et du texte original dans les résultats textuels. Script `node scripts/check-text-search.cjs` pour les recherches réelles et l’ouverture de l’image.
