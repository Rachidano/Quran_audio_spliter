namespace QuranVideo.Desktop.Models;

public sealed record SurahInfo(
    int Number,
    string NameArabic,
    string NameLatin,
    int AyahCount,
    int StartPage,
    int EndPage)
{
    public string DisplayName => $"{Number}. سورة {NameArabic} ({AyahCount} آية)";
}
