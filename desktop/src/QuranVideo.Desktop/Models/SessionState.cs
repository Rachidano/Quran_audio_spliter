namespace QuranVideo.Desktop.Models;

public sealed class SessionState
{
    public int LastSurahNumber { get; set; } = 1;
    public int LastAyahNumber { get; set; } = 1;
    public string LastOrientation { get; set; } = "Horizontal";
    public string? LastReciterId { get; set; } = "husary";
    public string ActiveView { get; set; } = "Library";
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
}
