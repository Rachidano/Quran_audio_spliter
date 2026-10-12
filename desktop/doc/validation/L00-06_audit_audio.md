# Rapport d'audit des ressources audio (L00-06)

Date : 11 octobre 2026  
Intervenant : Antigravity  
Tâche : **L00-06** — Relever les fichiers MP3 effectivement présents et leurs caractéristiques avec FFprobe. Importer les URL disponibles dans `data/reference/archive_org_114_inventory.json`, sans assimiler inventaire et fichiers téléchargés.

---

## 1. Inventaire des sources distantes

- **Fichier de référence** : `data/reference/archive_org_114_inventory.json`
- **Récitateur** : صابر عبد الحكم (Saber Abdelhakam) — Riwayat Warsh ‘an Nafi’.
- **Nombre total d'entrées distantes** : 114 sourates cataloguées.
- **Serveur de téléchargement** : `https://archive.org/download/z0603_202406x/`
- **URLs disponibles** : 114 URLs directes avec tailles et durées indicatives.
- **Principe d'exploitation** : L'inventaire fournit les URLs sources. L'application ne considère une sourate comme disponible localement qu'après téléchargement réel, vérification FFprobe et promotion atomique.

---

## 2. Fichiers audio physiquement présents sur le disque

L'inspection physique du dossier `data/audio/sources/` au moyen de FFprobe 8.0 donne le relevé suivant :

| Sourate | Fichier relatif | Taille (octets) | Durée (s) / Format | Échantillonnage / Canaux | Empreinte SHA-256 | Statut |
|---|---|---|---|---|---|---|
| **001 (الفاتحة)** | `data/audio/sources/001/original.mp3` | 1 129 009 | 67.33 s (1 min 07 s) | 44 100 Hz, Stéréo (128 kbps) | `318878f5983da82a52114b2440ceb50376a2a886ea17b55d99c9ce503f36f7c8` | **Complet & Valide** |
| **002 (البقرة)** | `data/audio/sources/002/original.mp3` | 247 766 723 | 15 482.18 s (~4 h 18 min) | 44 100 Hz, Stéréo (128 kbps) | `7dec4d89cf366aecd3e0e5751ed4f7a834bb2017268a5355e045bc702956b90c` | **Complet & Valide** |
| **003 (آل عمران)** | `data/audio/sources/003/original.mp3.tmp` | 43 646 487 | 2 724.67 s (partiel) | 44 100 Hz, Stéréo (128 kbps) | `f1aebe075406abeacb193eb0a1c2cc949b16501b62e4ce3983db2f7676f7f56f` | **Incomplet (.tmp)** |
| **112 (الإخلاص)** | `data/audio/sources/112/original.mp3` | 571 451 | 32.48 s (32 s) | 44 100 Hz, Stéréo (128 kbps) | `7f9ab4b5ee444aa90e93bae6bd566b7e23678aa714393a76a76143eb6b054691` | **Complet & Valide** |

### Synthèse :
- **Sourates complètes prêtes à l'emploi** : 3 sourates (1, 2, 112).
- **Sourates incomplètes** : 1 sourate (3, fichier `.tmp` résiduel d'un téléchargement interrompu).
- **Sourates à télécharger** : 111 sourates (les URLs d'archive.org sont répertoriées pour le service de téléchargement).

---

## 3. Enseignements pour le lecteur audio et l'export (Lots 1 et 5)

1. **Prise en charge des gros fichiers audio** :
   Le fichier de la sourate Al-Baqarah (`002/original.mp3`) fait **247,7 Mo** et dure plus de **4 heures et 18 minutes** (15 482 secondes).
   - Ne **jamais** décoder ce fichier en un seul tableau PCM en mémoire vive (cela consommerait plusieurs gigaoctets de RAM).
   - Les formes d'onde doivent être calculées par fenêtres / blocs et mises en cache sur disque (`cache/peaks/`).
2. **Précision temporelle et VBR / MP3 padding** :
   Le prototype du lot 1 doit qualifier la concordance de position entre le seek NAudio/Windows et le découpage temporel FFmpeg sur des positions réelles d'Al-Baqarah.

---

## 4. Données brutes archivées

Le rapport JSON complet FFprobe est disponible sous :  
`desktop/doc/validation/L00-06_audio_audit_result.json`.

---

## 5. Conclusion

La tâche **L00-06** est validée. L'inventaire des 114 URLs distantes est importé et les caractéristiques techniques des fichiers locaux réels sont établies avec précision.
