using Quran.Domain.Entities;
using Quran.Domain.Enums;

namespace Quran.UnitTests;

public class DomainEntitiesTests
{
    [Fact]
    public void Surah_CanAddMultipleAyahs()
    {
        // Arrange
        var surah = new Surah
        {
            Number = 1,
            NameArabic = "الفاتحة",
            NameLatin = "Al-Fatihah",
            AyahCount = 7,
            RevelationType = RevelationType.Meccan
        };

        var ayah1 = new Ayah
        {
            SurahId = 1,
            AyahNumber = 1,
            NumberInQuran = 1,
            TextWarsh = "بِسۡمِ ٱللَّهِ ٱلرَّحۡمَٰنِ ٱلرَّحِيمِ",
            SearchText = "بسم الله الرحمن الرحيم"
        };

        // Act
        surah.Ayahs.Add(ayah1);

        // Assert
        Assert.Single(surah.Ayahs);
        Assert.Equal("الفاتحة", surah.NameArabic);
        Assert.Equal(1, surah.Ayahs.First().AyahNumber);
    }

    [Fact]
    public void Ayah_CanSupportMultipleRecitations_WithDifferentAwjuh()
    {
        // Arrange
        var ayah = new Ayah
        {
            Id = 1,
            SurahId = 1,
            AyahNumber = 1,
            NumberInQuran = 1,
            TextWarsh = "بِسۡمِ ٱللَّهِ ٱلرَّحۡمَٰنِ ٱلرَّحِيمِ",
            SearchText = "بسم الله الرحمن الرحيم"
        };

        var reciter = new Reciter
        {
            Id = 1,
            NameArabic = "صابر عبد الحكم",
            NameLatin = "Saber Abdelhakam"
        };

        var riwaya = new Riwaya
        {
            Id = 1,
            NameArabic = "ورش عن نافع",
            NameLatin = "Warsh 'an Nafi'"
        };

        var wajh1 = new Wajh { Id = 1, Code = "w01", NameArabic = "الوجه 1", SortOrder = 1 };
        var wajh2 = new Wajh { Id = 2, Code = "w02", NameArabic = "الوجه 2", SortOrder = 2 };

        var audioAsset1 = new AudioAsset { Id = 1, StorageKey = "published/001/001/warsh/w01.mp3", Checksum = "abc" };
        var audioAsset2 = new AudioAsset { Id = 2, StorageKey = "published/001/001/warsh/w02.mp3", Checksum = "def" };

        var recitation1 = new Recitation
        {
            Id = 1,
            AyahId = ayah.Id,
            Reciter = reciter,
            Riwaya = riwaya,
            Wajh = wajh1,
            AudioAsset = audioAsset1,
            Status = RecitationStatus.Published,
            IsPublished = true
        };

        var recitation2 = new Recitation
        {
            Id = 2,
            AyahId = ayah.Id,
            Reciter = reciter,
            Riwaya = riwaya,
            Wajh = wajh2,
            AudioAsset = audioAsset2,
            Status = RecitationStatus.Reviewed,
            IsPublished = false
        };

        // Act
        ayah.Recitations.Add(recitation1);
        ayah.Recitations.Add(recitation2);

        // Assert (1 Ayah -> N Recitations / Awjuh)
        Assert.Equal(2, ayah.Recitations.Count);
        Assert.Contains(ayah.Recitations, r => r.Wajh?.Code == "w01" && r.IsPublished);
        Assert.Contains(ayah.Recitations, r => r.Wajh?.Code == "w02" && !r.IsPublished);
    }
}
