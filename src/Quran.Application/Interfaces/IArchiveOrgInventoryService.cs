using Quran.Application.DTOs;

namespace Quran.Application.Interfaces;

public interface IArchiveOrgInventoryService
{
    /// <summary>
    /// Récupère la liste des 114 fichiers MP3 originaux depuis l'API metadata d'Archive.org.
    /// </summary>
    Task<IReadOnlyList<ArchiveSourceItemDto>> FetchRemoteMetadataAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Renvoie l'inventaire local croisé avec l'état des fichiers présents sur le disque.
    /// </summary>
    Task<IReadOnlyList<ArchiveSourceItemDto>> GetLocalInventoryStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Calcule un résumé global de l'inventaire.
    /// </summary>
    Task<ArchiveInventorySummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronise les métadonnées distantes en base de données et dans le fichier JSON de référence.
    /// </summary>
    Task<int> SyncInventoryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Télécharge le fichier MP3 d'une sourate spécifique depuis Archive.org vers le stockage local des sources.
    /// </summary>
    Task<bool> DownloadSurahSourceAsync(int surahNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Télécharge un lot de sourates de manière séquentielle avec gestion des erreurs.
    /// </summary>
    Task<int> DownloadBatchAsync(IEnumerable<int> surahNumbers, CancellationToken cancellationToken = default);
}
