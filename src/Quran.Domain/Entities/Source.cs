namespace Quran.Domain.Entities;

public class Source
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // ex: "ArchiveOrg", "ManualUpload"
    public string? Description { get; set; }

    public ICollection<AudioAsset> AudioAssets { get; set; } = new List<AudioAsset>();
}
