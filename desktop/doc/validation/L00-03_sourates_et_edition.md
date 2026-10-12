# Rapport de vérification des sourates et métadonnées de l'édition (L00-03)

Date : 11 octobre 2026  
Intervenant : Antigravity  
Tâche : **L00-03** — Importer comme métadonnées la provenance et la révision de l’édition. Vérifier les 114 sourates, les 6 214 références uniques et les 285 ayat de Baqara pour cette édition.

---

## 1. Métadonnées de l'édition

- **Identifiant interne d'édition** : `newMushaf`
- **Révision vérifiée** : `20260913-full`
- **Date d'enregistrement** : `2026-09-13T09:17:40.8517962Z`
- **Provenance officielle** : Ministère des Affaires Religieuses et des Wakfs d'Algérie
- **URL de référence de l'édition** : `https://mushaf.marw.dz/web/ar/mushaf/newMushaf/1`
- **Libellé utilisateur imposé** : `مصدرنا الرسمي`
- **Attribution légale pour la boîte « À propos »** :
  > « استُخرجت صور القرآن الكريم من موقع وزارة الشؤون الدينية والأوقاف الجزائرية — مصحف الجزائر الإلكتروني »

---

## 2. Vérification canonique des 114 sourates

Le script `scratch/audit_lot0_surahs.py` a analysé l'intégralité des 6 214 lignes d'ayat réparties sur les 604 pages du Mushaf, et a croisé les données avec `data/reference/warsh-qpc-v10/source.json` et `data/reference/surahs.json`.

### Résultats synthétiques :

| Indicateur | Cible attendue | Valeur observée | Statut |
|---|---|---|---|
| Nombre de sourates | 114 | 114 | Conforme |
| Plage des sourates | 1 (الفاتحة) à 114 (الناس) | 1 à 114 | Conforme |
| Nombre total d'ayat | 6 214 | 6 214 | Conforme |
| Nombre d'ayat pour Al-Baqarah (sourate 2) | 285 | 285 | Conforme |
| Concordance de décompte avec le corpus QPC Warsh | 114 / 114 sourates | 114 / 114 sourates (0 divergence) | Conforme |
| Continuité des numéros d'ayat par sourate | 1 à N consécutifs | 0 rupture, 0 trou | Conforme |
| Séquence globale `verseNo` | 1 à 6 214 consécutifs | 1 à 6 214 sans saut | Conforme |

### Bornes remarquables :
- **Sourate 1 (الفاتحة)** : 7 ayat, `verseNo` 1 à 7, page 1 à 1.
- **Sourate 2 (البقرة)** : 285 ayat, `verseNo` 8 à 292, page 2 à 49. (Noter que le décompte koufi/Hafs compte 286 ayat, alors que la tradition madani/Warsh de cette édition compte 285 ayat).
- **Sourate 114 (الناس)** : 6 ayat, `verseNo` 6 209 à 6 214, page 604 à 604.

---

## 3. Données brutes archivées

Le détail complet par sourate (nom arabe, nombre d'ayat, `verseNo` début/fin, page début/fin) est enregistré dans :  
`desktop/doc/validation/L00-03_surahs_audit_result.json`.

---

## 4. Conclusion

La tâche **L00-03** est validée avec succès. Les métadonnées de provenance sont enregistrées et la structure canonique (114 sourates, 6 214 ayat, 285 ayat pour Al-Baqarah) est 100% cohérente et vérifiée.
