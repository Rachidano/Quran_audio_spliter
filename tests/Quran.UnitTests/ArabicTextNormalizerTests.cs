using Quran.Application.Common;

namespace Quran.UnitTests;

public class ArabicTextNormalizerTests
{
    [Theory]
    [InlineData("بِسۡمِ ٱللَّهِ ٱلرَّحۡمَٰنِ ٱلرَّحِيمِ", "بسم الله الرحمن الرحيم")]
    [InlineData("الْحَمْدُ لِلَّهِ رَبِّ الْعَالَمِينَ", "الحمد لله رب العالمين")]
    [InlineData("إِيَّاكَ نَعْبُدُ وَإِيَّاكَ نَسْتَعِينُ", "اياك نعبد واياك نستعين")]
    [InlineData("اهْدِنَا الصِّرَاطَ الْمُسْتَقِيمَ", "اهدنا الصراط المستقيم")]
    [InlineData("صِرَاطَ الَّذِينَ أَنْعَمْتَ عَلَيْهِمْ", "صراط الذين انعمت عليهم")]
    [InlineData("سُورَةُ الْبَقَرَةِ", "سوره البقره")]
    [InlineData("عَلَى", "علي")]
    public void Normalize_ProducesCleanSearchText(string original, string expected)
    {
        var result = ArabicTextNormalizer.Normalize(original);
        Assert.Equal(expected, result);
    }
}
