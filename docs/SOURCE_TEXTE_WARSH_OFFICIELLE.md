# Vérification des sources textuelles — 26 septembre 2026

## Archive proposée

https://archive.org/details/Quranwarsh

Métadonnées vérifiées via https://archive.org/metadata/Quranwarsh : original `Quran Ver  001.pdf` au format Image Container PDF, 512884282 octets ; 609 pages dans le scandata. Les fichiers TXT, EPUB et PDF texte sont des dérivés. OCR déclaré : Tesseract 5.3.0-6-g76ae, langue arabe.

Un échantillon du fichier `Quran Ver  001_djvu.txt` contient de nombreuses erreurs de reconnaissance et mélange texte, chiffres et commentaires. Ne pas importer ce dérivé comme texte validé. L'identité précise de l'édition et sa correspondance au MARW restent à établir.

## Publication officielle identifiée

https://qurancomplex.gov.sa/warsh/

Le contenu indexé de la page officielle du Complexe du roi Fahd annonce une police Unicode pour Warsh ainsi qu'un document Word contenant le Coran complet, signé numériquement. Cela établit une piste officielle pour obtenir le texte ; cela ne certifie pas le fichier local ni sa correspondance aux images MARW.

Portails à consulter pour le téléchargement :
- https://qurancomplex.gov.sa/techquran/dev/
- https://fonts.qurancomplex.gov.sa/

Les accès directs à ces portails ont expiré lors de cette vérification, y compris via une seconde méthode HTTP. Aucun paquet officiel n'a été récupéré et aucune signature n'a été vérifiée.

## État du projet

Le manifeste `data/reference/warsh-qpc-v10/manifest.json` indique un miroir communautaire, https://github.com/ibnhazm/KFGQPC, révision `25f2f8886d6ad0b47190f252d9d7fb1e296f3231`, statut `CandidatePendingComparison`.

Le rapport local `comparison.json` contient 6214 lignes, un ensemble de références concordant avec MARW et 62 différences de métadonnées de page. Certaines entrées utilisent une plage de pages : ce nombre ne signifie donc pas 62 erreurs de texte. Ce rapport constitue une vérification structurelle, pas une validation exhaustive du texte ou des délimitations.

## Suite nécessaire avant qualification

1. Obtenir le paquet depuis le portail officiel ; conserver URL, date, version, conditions de réutilisation et empreinte SHA-256.
2. Vérifier les informations de signature disponibles du document et identifier précisément l'édition et le système de décompte.
3. Comparer le texte officiel au candidat local en conservant les caractères d'origine ; consigner les différences sans correction automatique du texte source.
4. Vérifier les délimitations et les correspondances avec les images MARW, notamment les différences de pages déjà recensées. Ne pas déduire l'équivalence des textes de la seule égalité des numéros d'ayat.
5. Traiter séparément la validation du rendu avec la police appropriée et la validation éditoriale des divergences. Promouvoir uniquement les éléments effectivement vérifiés.

Aucun remplacement de base ni changement de statut de qualification n'a été effectué pendant cette recherche.
