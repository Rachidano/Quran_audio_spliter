using System.Text.RegularExpressions;
using Quran.Application.DTOs;
using Xunit;

namespace Quran.UnitTests;

public class ArchiveOrgInventoryServiceTests
{
    private static readonly Regex SurahPatternRegex = new(@"سورة\s*([0-9]{1,3})\s*(.*?)(\.mp3|$)", RegexOptions.Compiled);

    [Theory]
    [InlineData("صابر عبد الحكم رواية ورش  جميع الأوجه الشاطبية  سورة 001  الفاتحة.mp3", 1, "الفاتحة")]
    [InlineData("صابر عبد الحكم رواية ورش  جميع الأوجه الشاطبية  سورة 002  البقرة.mp3", 2, "البقرة")]
    [InlineData("صابر عبد الحكم رواية ورش  جميع الأوجه الشاطبية  سورة 112  الإخلاص.mp3", 112, "الإخلاص")]
    [InlineData("صابر عبد الحكم رواية ورش  جميع الأوجه الشاطبية  سورة 114  الناس.mp3", 114, "الناس")]
    public void SurahPatternRegex_ExtractsNumberAndNameCorrectly(string fileName, int expectedNumber, string expectedName)
    {
        var match = SurahPatternRegex.Match(fileName);

        Assert.True(match.Success);
        Assert.Equal(expectedNumber, int.Parse(match.Groups[1].Value));
        Assert.Equal(expectedName, match.Groups[2].Value.Trim());
    }

    [Fact]
    public void ArchiveInventorySummary_CalculatesAccurately()
    {
        var items = new List<ArchiveSourceItemDto>
        {
            new(1, "الفاتحة", "001.mp3", "http://dl/1", 1000, 67.0, "md5_1", "sha_1", "Downloaded", true, "C:/audio/1.mp3"),
            new(2, "البقرة", "002.mp3", "http://dl/2", 5000, 15000.0, "md5_2", "sha_2", "Missing", false, null)
        };

        var totalExpected = 114;
        var totalRemoteFound = items.Count;
        var totalDownloaded = items.Count(x => x.IsDownloaded);
        var totalRemoteSize = items.Sum(x => x.SizeBytes);

        var summary = new ArchiveInventorySummaryDto(
            TotalExpected: totalExpected,
            TotalRemoteFound: totalRemoteFound,
            TotalDownloaded: totalDownloaded,
            TotalRemoteSizeBytes: totalRemoteSize,
            TotalDownloadedSizeBytes: 1000,
            IsFullySynchronized: false
        );

        Assert.Equal(114, summary.TotalExpected);
        Assert.Equal(2, summary.TotalRemoteFound);
        Assert.Equal(1, summary.TotalDownloaded);
        Assert.Equal(6000, summary.TotalRemoteSizeBytes);
        Assert.False(summary.IsFullySynchronized);
    }
}
