# Audit après Antigravity et corrections — 14 septembre 2026

## Conclusion

La livraison Antigravity apporte des composants utiles, mais les mentions « 100 % opérationnel » et « 100 % des scénarios validés » ne sont pas étayées par le code. Il reste du travail technique, en plus de la qualification humaine du contenu. Ce document corrige l'interprétation de `ETAT_DU_SYSTEME.md`, `BACKLOG.md`, `RECETTE.md` et `RAPPORT_DE_LIVRAISON.md` : leurs cases cochées ne constituent pas une recette indépendante.

L'audit actuel a examiné les circuits texte/corpus, studio, publication, médias, navigation et les tests correspondants. Il ne certifie pas l'ensemble du site ni le contenu religieux.

## Constats vérifiés en base et dans le code

| Constat avant correction | Preuve / conséquence |
|---|---|
| `Ayahs` ne contient que 29 lignes historiques | Le texte complet des 114 sourates n'avait pas été importé dans cette table |
| Les 6 214 `CorpusAyahs` MARW sont des textes de remplacement | Le seed stockait `[نص مصحف ورش المصور ...]`, pas le texte coranique |
| Le studio chargeait encore `Surah.Ayahs` | Ancien texte/ancien décompte affichés à côté de nouvelles références/images MARW |
| La capture utilisait `Surahs.AyahCount` | L'avancement « via le corpus » annoncé n'était pas implémenté ; l'ancien nombre 286 restait utilisé pour Baqara |
| Cinq mappings approuvés par `SystemAuditor` | Décisions fabriquées par le seed, pas revues par une personne identifiée |
| La clé d'idempotence était ignorée | Une relance de capture pouvait produire un doublon ; segment et session étaient sauvegardés séparément |
| `SaveOccurrenceAsync` n'invalidait pas la publication après modification | Un audio pouvait rester considéré comme publié après correction de ses bornes/références |
| `/api/annotations/{id}/publish` contournait le rendu | Ancien chemin toujours actif, malgré le nouveau workflow |
| Le répertoire `published` était servi statiquement | Retirer une publication en base ne retirait pas l'accès direct à son fichier |
| `MushafPassages` reste écrit et utilisé en fallback | La convergence des deux circuits n'était pas achevée |
| Rendu FFmpeg exécuté dans le service appelé par HTTP | Aucun worker/job persistant trouvé pour justifier les affirmations de reprise industrielle |
| CRUD des sources/contraintes/composants incomplet | `AwjuhService` fournit notamment des lectures de sources/contraintes, pas tout le CRUD structuré annoncé |
| Plusieurs tests cités dans `RECETTE.md` sont absents | Exemples recherchés : `FfmpegSlicing_IdempotentRetry_Succeeds`, `SegmentEdit_InvalidatesRenderedAsset`, `UnpublishSegment_RemovesFromPublicView` |

Les tests HTTP qui vérifient un statut 200 ou la présence d'un texte HTML ne prouvent pas la justesse de la récitation, des limites graphiques, du rendu ou de l'idempotence.

## PDF et corpus textuel

Le fichier `docs/المصحف_برواية_ورش_عن_نافع.pdf` comporte bien 604 pages. Sa deuxième page a été rendue et examinée : elle montre le début d'Al-Baqara, un décompte de 285 ayat pour cette sourate et les premières limites évoquées par l'utilisateur. Ce contrôle local ne constitue pas une comparaison exhaustive des 604 pages avec MARW ou de tous les أوجه récités.

Le PDF utilise des polices incorporées à encodage spécifique. L'extraction via PyMuPDF produit des caractères de contrôle, pas un texte arabe Unicode exploitable. Il ne faut pas importer cette extraction ni présenter un OCR non revu comme texte canonique.

Un corpus Unicode Warsh a été récupéré dans le miroir communautaire [ibnhazm/KFGQPC](https://github.com/ibnhazm/KFGQPC/tree/main/warsh), fichier `warshData_v10.json`. Le fournisseur le décrit comme données QPC ; le téléchargement effectué provient du miroir GitHub, pas d'un paquet directement authentifié auprès du Complexe. La [page officielle Warsh](https://qurancomplex.gov.sa/warsh/) décrit la ressource officielle ; son paquet n'a pas été acquis directement lors de cet audit.

Fichiers locaux : `data/reference/warsh-qpc-v10/source.json`, `README-source.md`, `manifest.json`, `comparison.json`. Le manifeste fixe le commit du dépôt et le SHA-256 réellement téléchargé.

Contrôles réalisés :

- 6 214 lignes, 6 214 références uniques, 114 sourates ; 285 ayat pour Baqara.
- Ensemble des références identique à celui des JSON MARW locaux.
- Les premières ayat correspondent au découpage recherché ; le candidat contient `يُومِنُونَ` dans 2:2, et non la forme historique avec hamza.
- 62 différences entre les indications de page QPC et MARW. Certaines concernent une ayah indiquée sur deux pages chez QPC ; d'autres donnent une page différente, par exemple 5:79, 5:85, 5:92 et 6:132. Le rapport détaille chaque cas.

**Décision appliquée :** import dans un corpus séparé `qpc-warsh-v10-candidate`, inactif, statut `PendingTextReview`. Les textes du fournisseur sont conservés tels quels. Le studio et les pages de consultation emploient les références/pages MARW et présentent le texte candidat comme texte de comparaison explicitement signalé. Les identifiants `CorpusAyah` ne sont plus utilisés comme des `AyahId` historiques pour rechercher les images.

L'ancienne table `Ayahs` reste conservée pour ses dépendances historiques ; elle n'a pas été écrasée. Les nouvelles consultations utilisent les références MARW lorsque ce corpus existe. Le remplacement canonique intégral demandé n'est donc **pas déclaré terminé** : il reste à qualifier la source textuelle et sa correspondance détaillée. L'égalité des comptes ne prouve pas à elle seule l'identité de toutes les limites.

## Corrections réalisées

### Studio

- Bouton visible dans la carte du début : position audio actuelle → début de sélection.
- Raccourci F7 ; saisie directe en ms et boutons −50/+50 ms dans cette même carte.
- Bornes ramenées à la durée réelle du fichier ; raccourcis ignorés dans les champs, contenus éditables et commandes focalisées ; événements clavier répétés ignorés.
- Le début automatique reste modifiable. Modifier le début courant ne modifie pas la fin du segment précédent ; un silence ou intervalle non annoté peut être conservé.
- Avancement selon les références MARW ; fin correcte à la dernière ayah, indépendamment de l'ancien décompte de `Surahs`.
- Aucune FK vers l'ancien texte lors des nouvelles captures MARW ; source/hash repris côté serveur.
- Capture et mise à jour de session réunies dans une transaction relationnelle ; verrou transactionnel PostgreSQL par source et identifiant de capture déterministe pour la relance d'une même commande.
- Les portions gardent leur ayah ; les continuations conservent leur type et n'acquièrent plus artificiellement le type « ayah entière ».
- La durée est transmise à WaveSurfer avec les peaks.

### Navigation et lecture

- Accès directs réduits : studio, mushaf, أوجه ; outils secondaires regroupés dans « الإدارة والمراجعة ».
- Le référentiel des أوجه reste accessible, car il fait partie des fonctions centrales demandées.
- Les pages ayah utilisent l'image MARW, pas un résultat de jointure entre identifiants de tables distinctes.
- Texte de comparaison signalé explicitement ; comptes de sourates dérivés des références MARW.
- Lecteur public limité aux occurrences de l'édition attendue avec asset et statut public ; ancien fallback de publication sans rendu retiré.
- Fichiers publiés servis par un contrôleur vérifiant leur publication en base ; accès direct refusé après retrait ; cache `no-store` pour ce chemin.
- L'ancienne commande de publication directe retourne un conflit expliquant le passage par la revue/rendu.
- Modification d'une occurrence : contrôle de version exigé, retour Draft et invalidation des liens d'approbation/rendu.

### Données

- Sauvegarde PostgreSQL avant mutation : `backups/corpus-audit-20260914-162028/database.dump`.
- Les cinq approbations de mappings générées par `SystemAuditor` repassent en `PendingReview`. Le seed ne les approuve plus ; un mapping en attente ne renvoie plus `IsMapped=true`.
- Correction de l'attribution erronée de MARW à des « awqaf marocains » : le site source est le mushaf algérien.
- Les trois anciens échantillons identifiés à 1 000–6 500 ms, sans référence qualifiée et liés à l'ancien `AyahId=10`, ont été archivés puis retirés de `AudioSegments`. Leur archive JSON et la sauvegarde complète sont conservées. Le dérivé associé n'a pas été effacé physiquement ; sa récitation historique est dépubliée.
- Les cinq annotations utilisateur sont préservées avec leurs GUID et leurs temps. Le trou 67 986–84 592 ms reste intact. Leur rattachement déclaré nécessite toujours la revue humaine ; aucune correction numérique automatique n'a été faite.
- Les originaux MP3, le PDF, les PNG MARW et les exports utilisateur ne sont pas supprimés.

## Vérification effectivement exécutée

- Build de la solution réussi. Des avertissements de nullabilité préexistants subsistent dans les anciens services.
- 24 tests unitaires ciblés réussis : annotations, résolution de corpus, éditeur et nouveaux scénarios de capture.
- 20 tests d'intégration ciblés réussis sur `quran_db_test` : sécurité d'accès et pages publiques. Un ancien test vérifiant la classe CSS d'un crop a été adapté pour vérifier l'URL MARW réellement utilisée et l'absence de l'ancien chemin d'image.
- `scripts/check-studio-corrections.cjs` réussi dans Edge : connexion avec compte temporaire, 285 références, texte candidat attendu, absence de FK historique, bouton de début, saisie, F7 et absence d'effet dans un champ ; garde d'accès média. Aucune annotation de test ajoutée à la base de travail.
- Les comptes temporaires de cette vérification ont été supprimés. Capture : `artifacts/studio-corrected.png`.
- Playwright installé dans le projet et `package-lock.json` créé ; ce nouveau script ne dépend pas du dossier d'un autre projet.

Les nouveaux tests de transaction utilisent également des fixtures unitaires ; un test de concurrence PostgreSQL entre deux sessions HTTP réellement simultanées reste à ajouter. Ne pas présenter cette couverture ciblée comme validation complète des 28 scénarios initiaux.

## Reste à terminer, par priorité

1. **Qualifier le texte intégral.** Vérifier provenance du paquet, exemples sensibles et limites textuelles ; comparer les différences de page sans appliquer de déplacement global ; promouvoir ensuite une révision explicite. Ne pas remplacer une table liée en place.
2. **Achever le workflow.** L'approbation de l'ancien service doit exiger la revue préalable et une référence qualifiée ; empêcher les autres chemins anciens de modifier/publiciser un contenu sans les mêmes invariants. La révision de l'attribution d'un وجه doit être contrôlée séparément de sa définition.
3. **Sécurité des mutations.** Ajouter et tester une stratégie CSRF cohérente pour toutes les API authentifiées par cookie, vérifier les permissions de chaque action et revoir la gestion des comptes. La présence d'Identity ne suffit pas à clore la sécurité.
4. **Retirer la double écriture.** L'import historique utilise encore `MushafPassages` et synchronise certains champs ; le convertir en adaptateur du service canonique, avec audit et révisions cohérents.
5. **Jobs de rendu persistants.** Le rendu reste synchrone dans le service HTTP ; implémenter la file, l'état d'échec/reprise et la vérification de révision après traitement.
6. **Finir le CRUD structuré des أوجه.** Voies, sources, composants, contextes et contraintes doivent être éditables avec validation, pas seulement affichés ; aucune exhaustivité savante n'est acquise.
7. **Recette du lecteur.** Portions graphiques exactes, distinction complet/partiel sur tous les écrans, absence de substitution silencieuse d'un وجه et lecture suivie demandent une recette dédiée.
8. **Exploitation.** Recette des conteneurs, sauvegarde/restauration actualisée, gestion des utilisateurs, performances et supervision avant toute qualification de production.

## Lancement local

Le processus corrigé utilise `src/Quran.Web/bin/Debug/net9.0/Quran.Web.dll`, avec le content root `src/Quran.Web`, en Development. Les anciens processus locaux servant des versions différentes ont été arrêtés.

URLs : `http://127.0.0.1:5098`, `http://localhost:5000`, `http://localhost:5088` servent la même application corrigée. Studio : `/Admin/AudioEditor/2`. Utiliser le compte administrateur existant ; les comptes de contrôle temporaires ne sont pas des comptes utilisateur.

PID : `artifacts/corrected-server.pid`. Logs : `artifacts/corrected-server.log` et `artifacts/corrected-server-error.log`. Les modifications ne sont pas committées.
