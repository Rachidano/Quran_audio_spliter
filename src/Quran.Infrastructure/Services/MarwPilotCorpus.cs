using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace Quran.Infrastructure.Services;

/// <summary>Original MARW PNGs with their own nested crop/text/ayah coordinates.</summary>
public sealed class MarwPilotCorpus
{
    public const string Revision = "20260913-full";
    public const int PageCount = 604;
    public const string EditionCode = "MARW_newMushaf_" + Revision;
    private readonly Dictionary<int, WarshPilotPage> _pages = new();
    private readonly Dictionary<int, string> _images = new();
    public IReadOnlyList<int> PageNumbers => _pages.Keys.Order().ToArray();

    public MarwPilotCorpus(string directory)
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "manifest.json")));
        if (manifest.RootElement.GetProperty("edition").GetString() != "newMushaf")
            throw new InvalidDataException("Édition MARW inattendue.");
        var entries = manifest.RootElement.GetProperty("files").EnumerateArray().ToArray();
        var expected = Enumerable.Range(1, PageCount).SelectMany(n => new[] { $"page-{n:D3}.png", $"page-{n:D3}.json" }).ToArray();
        if (!entries.Select(e => e.GetProperty("file").GetString()).Order().SequenceEqual(expected.Order()))
            throw new InvalidDataException("Fichiers MARW incomplets ou inattendus.");
        var files = new Dictionary<string, string>();
        foreach (var entry in entries)
        {
            var name = entry.GetProperty("file").GetString()!;
            var bytes = File.ReadAllBytes(Path.Combine(directory, name));
            if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(entry.GetProperty("sha256").GetString(), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Empreinte MARW incorrecte : {name}");
            files.Add(name, Path.Combine(directory, name));
        }
        var references = new Dictionary<int, (int Surah, int Ayah)>();
        var lastAyahBySurah = new Dictionary<int, int>();
        foreach (var number in Enumerable.Range(1, PageCount))
        {
            var image = File.ReadAllBytes(files[$"page-{number:D3}.png"]);
            if (image.Length < 24 || !image.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
                throw new InvalidDataException("Image MARW non PNG.");
            var width = BinaryPrimitives.ReadInt32BigEndian(image.AsSpan(16, 4));
            var height = BinaryPrimitives.ReadInt32BigEndian(image.AsSpan(20, 4));
            if (width <= 0 || height <= 0) throw new InvalidDataException("Dimensions PNG invalides.");
            var mapBytes = File.ReadAllBytes(files[$"page-{number:D3}.json"]);
            var mapHash = Convert.ToHexString(SHA256.HashData(mapBytes));
            var imageHash = Convert.ToHexString(SHA256.HashData(image));
            using var map = JsonDocument.Parse(mapBytes);
            var root = map.RootElement;
            if (root.GetProperty("page").GetInt32() != number || root.GetProperty("mushafId").GetString() != "newMushaf")
                throw new InvalidDataException("La carte ne correspond pas à la page MARW.");
            var crop = root.GetProperty("boxes").GetProperty("cropBox");
            var text = root.GetProperty("boxes").GetProperty("textBox");
            var regions = new List<WarshPilotRegion>();
            foreach (var row in root.GetProperty("rows").EnumerateArray())
            {
                var surah = row.GetProperty("surah").GetInt32();
                var ayah = row.GetProperty("ayah").GetInt32();
                if (surah is < 1 or > 114 || ayah < 1) throw new InvalidDataException("Référence MARW invalide.");
                var minimumTop = ayah == 1 ? MarwOpeningReview.Start(number, surah, mapHash, imageHash) : 0;
                var paths = new List<string>();
                foreach (var rect in row.GetProperty("rects").EnumerateArray())
                {
                    // Clip only the visually reviewed header interval. A middle
                    // rectangle may include BOTH a basmala and genuine text lines.
                    var rectTop = N(rect, "top");
                    var rectHeight = N(rect, "height");
                    if (ayah == 1)
                    {
                        var bottom = rectTop + rectHeight;
                        rectTop = Math.Max(rectTop, minimumTop);
                        rectHeight = bottom - rectTop;
                        if (rectHeight <= 1e-8) continue;
                    }
                    // Each level is a percentage of its parent. Preserve all subregions.
                    var x = (N(crop,"left") + N(crop,"width") * (N(text,"left") + N(text,"width") * N(rect,"left") / 100) / 100) / 100;
                    var y = (N(crop,"top") + N(crop,"height") * (N(text,"top") + N(text,"height") * rectTop / 100) / 100) / 100;
                    var w = N(crop,"width") * N(text,"width") * N(rect,"width") / 1000000;
                    var h = N(crop,"height") * N(text,"height") * rectHeight / 1000000;
                    if (x < 0 || y < 0 || w <= 0 || h <= 0 || x + w > 1.000001 || y + h > 1.000001)
                        throw new InvalidDataException("Rectangle MARW hors image.");
                    paths.Add($"M {F(x*width)} {F(y*height)} L {F((x+w)*width)} {F(y*height)} L {F((x+w)*width)} {F((y+h)*height)} L {F(x*width)} {F((y+h)*height)} Z");
                }
                if (paths.Count == 0) throw new InvalidDataException("Ayah sans régions.");
                var verseNo = row.GetProperty("verseNo").GetInt32();
                var continuation = row.GetProperty("continuousFromPrevPage").GetBoolean();
                if (references.TryGetValue(verseNo, out var existing))
                {
                    if (!continuation || existing != (surah, ayah)) throw new InvalidDataException("Référence MARW dupliquée ou incohérente.");
                }
                else
                {
                    if (verseNo != references.Count + 1 || ayah != lastAyahBySurah.GetValueOrDefault(surah) + 1)
                        throw new InvalidDataException("Rupture de numérotation MARW.");
                    references.Add(verseNo, (surah, ayah));
                    lastAyahBySurah[surah] = ayah;
                }
                regions.Add(new(surah, ayah, string.Join(" ", paths), continuation));
            }
            if (regions.Count == 0 || regions.Select(r => r.Reference).Distinct().Count() != regions.Count)
                throw new InvalidDataException("Références MARW inattendues.");
            _images.Add(number, files[$"page-{number:D3}.png"]);
            _pages.Add(number, new(number, $"0 0 {width} {height}", width, height, regions.AsReadOnly(), Convert.ToHexString(SHA256.HashData(image)).ToLowerInvariant()));
        }
        if (references.Count != 6214 || lastAyahBySurah.Count != 114)
            throw new InvalidDataException("Le corpus MARW ne couvre pas 114 sourates et 6214 ayat.");
    }

    public WarshPilotPage? GetPage(int number) => _pages.GetValueOrDefault(number);
    public byte[]? GetImage(int number)
    {
        if (!_images.TryGetValue(number, out var path)) return null;
        var bytes = File.ReadAllBytes(path);
        if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(_pages[number].Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Image modifiée après validation du corpus.");
        return bytes;
    }
    public IReadOnlyDictionary<int, int> SurahStartPages => _pages.Values
        .SelectMany(p => p.Regions.Select(r => new { r.SurahNumber, p.Number }))
        .GroupBy(r => r.SurahNumber).ToDictionary(g => g.Key, g => g.Min(r => r.Number));
    private static string F(double number) => number.ToString("G17", CultureInfo.InvariantCulture);
    private static double N(JsonElement element, string property)
    {
        var n = element.GetProperty(property).GetDouble();
        return double.IsFinite(n) ? n : throw new InvalidDataException("Coordonnée MARW non finie.");
    }
}
