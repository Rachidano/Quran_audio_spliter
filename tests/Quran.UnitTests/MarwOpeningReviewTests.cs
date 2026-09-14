using Quran.Infrastructure.Services;

namespace Quran.UnitTests;

public class MarwOpeningReviewTests
{
    [Fact]
    public void RejectsCorrectionsForDifferentImageMapOrPage()
    {
        var entry = MarwOpeningReview.Current.Entries[59];
        Assert.Throws<InvalidDataException>(() => MarwOpeningReview.Start(entry.Page, entry.Surah, "changed", entry.ImageSha256));
        Assert.Throws<InvalidDataException>(() => MarwOpeningReview.Start(entry.Page, entry.Surah, entry.MapSha256, "changed"));
        Assert.Throws<InvalidDataException>(() => MarwOpeningReview.Start(entry.Page + 1, entry.Surah, entry.MapSha256, entry.ImageSha256));
        Assert.Equal(100.0 / 15, MarwOpeningReview.Start(entry.Page, entry.Surah, entry.MapSha256, entry.ImageSha256), 8);
    }
}
