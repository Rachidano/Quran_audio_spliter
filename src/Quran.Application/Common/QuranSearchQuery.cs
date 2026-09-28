using System.Text;
using System.Text.RegularExpressions;

namespace Quran.Application.Common;

public static class QuranSearchQuery
{
    public static string Normalize(string input)
    {
        var normalized = ArabicTextNormalizer.Normalize(input.Normalize(NormalizationForm.FormKC))
            .Replace('ؤ', 'و').Replace('ئ', 'ي').Replace('ی', 'ي').Replace("ء", "");
        return Regex.Replace(Regex.Replace(normalized, @"[^\p{L}\s]", " "), @"\s+", " ").Trim();
    }

    public static bool TryParseReference(string query, out int surah, out int ayah)
    {
        surah = ayah = 0;
        var digits = string.Concat(query.Trim().Select(c => char.IsDigit(c) ? (char)('0' + (int)char.GetNumericValue(c)) : c));
        var match = Regex.Match(digits, @"^(\d{1,3})\s*[:\s\-]\s*(\d{1,3})$");
        return match.Success && int.TryParse(match.Groups[1].Value, out surah) && int.TryParse(match.Groups[2].Value, out ayah);
    }

    public static bool HasMinimumLetters(string query) => Normalize(query).Count(char.IsLetter) >= 3;
}
