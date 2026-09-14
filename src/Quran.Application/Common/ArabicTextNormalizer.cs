using System.Text;
using System.Text.RegularExpressions;

namespace Quran.Application.Common;

public static partial class ArabicTextNormalizer
{
    // Tashkeel / Harakat + Quranic annotations
    [GeneratedRegex(@"[\u064B-\u065F\u0670\u06D6-\u06ED\u08D4-\u08ED\uFD3E\uFD3F]")]
    private static partial Regex DiacriticsRegex();

    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        // 1. Remove diacritics / tashkeel and quranic symbols
        var text = DiacriticsRegex().Replace(input, string.Empty);

        // 2. Remove Tatweel / Kashida
        text = text.Replace("\u0640", string.Empty);

        // 3. Normalize specific characters
        var sb = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            switch (c)
            {
                // Alif forms (Alef with Hamza Above, Below, Madda, Wasla) -> Simple Alif
                case 'أ':
                case 'إ':
                case 'آ':
                case 'ٱ':
                    sb.Append('ا');
                    break;

                // Alif Maqsura -> Ya
                case 'ى':
                    sb.Append('ي');
                    break;

                // Ta Marbuta -> Ha
                case 'ة':
                    sb.Append('ه');
                    break;

                default:
                    sb.Append(c);
                    break;
            }
        }

        // 4. Collapse consecutive spaces and trim
        return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
    }
}
