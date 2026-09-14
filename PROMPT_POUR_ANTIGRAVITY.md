# Message à donner à Antigravity

Travaille dans `D:\Projets\ITComp\Quran` sur la solution existante `QuranWarsh.sln`.

Lis intégralement `PLAN_REPRISE_ANTIGRAVITY.md`, puis les quatre documents initiaux qui y sont référencés et les instructions locales applicables. Ce plan constitue le dossier de reprise : il décrit l’état partiel, les modèles à réunifier, les exigences utilisateur, les migrations, les lots et les critères de recette.

Je te demande de **réaliser cette reprise**, pas de me produire un nouveau plan ou une nouvelle démonstration. Commence par sécuriser et inventorier l’existant, puis exécute les lots dans leur ordre de dépendance. Réutilise les six projets .NET existants et conserve la contrainte .NET 9 des spécifications ; traite toute évolution technologique dans une décision distincte.

Les priorités sont :

1. Protéger l’administration et les médias privés.
2. Résoudre proprement l’identité des références : les anciens numéros d’ayat ne correspondent pas automatiquement aux images MARW.
3. Réunifier `AudioSegment` et `MushafPassage`, préserver mes annotations et leurs timestamps.
4. Livrer un référentiel des أوجه réellement administrable, versionné et partagé avec l’éditeur et le lecteur.
5. Livrer un seul éditeur arabe : un clic de fin enregistre l’ayah, prend cette fin comme début suivant et avance la référence ; une reprise reste liée à la bonne ayah.
6. Faire fonctionner toute la chaîne annotation → revue → rendu → publication → écoute sur les images, avec portions, répétitions, choix du وجه, téléchargement et partage.
7. Nettoyer les pages d’essai, généraliser le logiciel à toutes les sourates et livrer les tests, la documentation et l’exploitation.

Ne supprime pas les originaux, les exports ou les associations sans sauvegarde et rapport de migration. Ne recrée pas les anciens awjuh supprimés. Ne publie pas de contenu réel pour simuler une validation. Ne présente pas les 14 propositions actuelles comme tous les أوجه et ne fabrique pas des associations de texte/audio/image pour remplir la couverture.

À chaque lot, mets à jour `docs/BACKLOG.md` et `docs/ETAT_DU_SYSTEME.md` avec les changements et vérifications réels. Poursuis les étapes techniques sans me demander « continuer ? » à chaque fois. Les décisions humaines d’écoute et d’attribution doivent apparaître dans une file de revue claire ; leur attente ne doit pas bloquer les autres travaux indépendants.

La livraison finale doit distinguer **logiciel fonctionnel** et **contenu encore à qualifier**, fournir les URLs et la procédure de test, ainsi que les résultats réels de la recette définie dans le plan.
