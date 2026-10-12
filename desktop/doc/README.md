# Application Windows de création de vidéos d’ayat

Version documentaire 1.1 — 11 octobre 2026. Statut : implémentation auditée, livraison non validée. Voir le [rapport Codex](validation/AUDIT_CODEX_2026-10-11.md) et les tâches rouvertes.

## Reprise du développement

Commencer par [objectif.md](../objectif.md) pour le contexte et les règles, puis [tasks.md](../tasks.md) pour l’avancement réel et la prochaine tâche. Chaque intervenant met à jour le suivi après chaque tâche, avec ses vérifications et son point de reprise.

## Documents

1. [Conception détaillée](CONCEPTION_DETAILLEE.md) : besoins, écrans, architecture, données, extraction des images, annotations audio, génération MP4, sauvegarde et critères de qualité.
2. [Plan d’implémentation détaillé](PLAN_IMPLEMENTATION.md) : lots ordonnés, dépendances, livrables, tests et critères de passage jusqu’à une version Windows utilisable.

## Résultat recherché

Une application Windows autonome : choisir une sourate et un récitateur, télécharger ou importer sa récitation, délimiter les ayat, vérifier leur image extraite du Mushaf, puis exporter des vidéos MP4. L’écriture provient des images originales ; elle n’est pas recomposée avec une police dans les vidéos.

Stockage retenu : SQLite pour les données structurées et dossiers pour les médias. Pas de gestion des أوجه, pas de serveur PostgreSQL, pas de dépendance à l’exécution du site web.

## Exigences confirmées

- **.NET 9**, avec le SDK déjà installé ; projet WPF `net9.0-windows`.
- **Installateur Windows Setup.exe** incluant runtime et outils nécessaires, sans installation manuelle du SDK ou de FFmpeg.
- **Interface et assistant d’installation en arabe RTL**, avec nombres et horloges lisibles.
- **Utilisateur non informaticien** : trois entrées principales, parcours guidé, sauvegarde automatique et options avancées fermées par défaut. Paramètres vidéo techniques prédéfinis.
- **Pas de suringénierie** : un projet applicatif, un projet de tests, SQLite et fichiers locaux ; pas de multiplication des couches ou des écrans.

## Consignes pour l’implémentation future

- Travailler dans `desktop/` sans modifier le fonctionnement du site actuel.
- Commencer par le lot 0 puis le prototype du lot 1 : qualifier l’extraction des ayat avant d’investir dans toute l’interface.
- Réutiliser les ressources existantes comme entrées versionnées et vérifiées, sans les déplacer ni les modifier.
- Distinguer les besoins confirmés des choix techniques proposés ; les valeurs par défaut peuvent évoluer après le prototype.
- Ne pas déclarer le texte candidat, les masques ou les annotations validés par le seul succès de tests informatiques.
- La création de ces documents n’autorise pas une publication sur YouTube. Les exports seront des fichiers locaux.

La capture fournie par l’utilisateur sert de référence de présentation. La vidéo YouTube n’a pas pu être consultée pendant l’analyse ; aucun comportement animé n’en a été déduit. La capture est dans la conversation, pas encore dans les ressources du dépôt.

## Correction audio du 12 octobre 2026

Une page permet désormais de configurer les 114 liens par récitateur et de télécharger les sourates. Lire le [rapport de correction et les preuves](validation/CORRECTION_LIENS_AUDIO_2026-10-12.md). Le suivi J018 distingue cette livraison ciblée de la validation globale du projet.
