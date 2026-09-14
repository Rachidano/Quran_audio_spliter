using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace Quran.Infrastructure.Services;

/// <summary>
/// A separate, immutable graphical reference. Its verse numbers MUST NOT be used
/// as keys into the legacy Ayahs table without a reviewed correspondence.
/// </summary>
public sealed class WarshPilotCorpus
{
    public const string Revision = "79a5d21711fc54a98a4f9158eb6865c3be263884";
    public const string EditionCode = "WARSH_KFQC_QURANWS_" + Revision;
    public const string Repository = "https://github.com/quran-ws/quran-svg";
    private readonly Dictionary<int, WarshPilotPage> _pages = new();
    private readonly Dictionary<int, byte[]> _images = new();
    public IReadOnlyList<int> PageNumbers => _pages.Keys.Order().ToArray();

    public WarshPilotCorpus(string directory)
    {
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var manifest = JsonSerializer.Deserialize<BundleManifest>(
            File.ReadAllText(Path.Combine(directory, "manifest.json")), jsonOptions)
            ?? throw new InvalidDataException("Manifeste Warsh absent ou invalide.");
        if (manifest.Revision != Revision || manifest.Repository != Repository)
            throw new InvalidDataException("La révision du paquet Warsh n'est pas celle du pilote.");

        string[] expected = ["001.svg", "002.svg", "003.svg", "001.json", "002.json", "003.json", "NOTICE.md", "LICENSE"];
        if (manifest.Files.Length != expected.Length ||
            !manifest.Files.Select(f => f.File).Order().SequenceEqual(expected.Order()))
            throw new InvalidDataException("Le paquet doit contenir exactement les trois pages et leurs notices.");

        var files = new Dictionary<string, byte[]>();
        foreach (var entry in manifest.Files)
        {
            // The allowlist above also prevents paths escaping the bundle directory.
            var suffix = entry.File.EndsWith(".svg") ? $"mushafs/warsh/kfqc/svg/{entry.File}"
                : entry.File.EndsWith(".json") ? $"mushafs/warsh/kfqc/json/{entry.File}" : entry.File;
            if (entry.Url != $"https://raw.githubusercontent.com/quran-ws/quran-svg/{Revision}/{suffix}")
                throw new InvalidDataException($"Provenance inattendue : {entry.File}");
            var bytes = File.ReadAllBytes(Path.Combine(directory, entry.File));
            var hash = Convert.ToHexString(SHA256.HashData(bytes));
            if (!hash.Equals(entry.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Empreinte SHA-256 incorrecte : {entry.File}");
            files.Add(entry.File, bytes);
        }

        for (var number = 1; number <= 3; number++)
        {
            var name = $"{number:D3}";
            var bytes = files[$"{name}.svg"];
            using var stream = new MemoryStream(bytes);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null
            });
            var root = XDocument.Load(reader).Root ?? throw new InvalidDataException("SVG vide.");
            XNamespace svg = "http://www.w3.org/2000/svg";
            if (root.Name != svg + "svg") throw new InvalidDataException("Racine SVG invalide.");
            // Files are served as images, never injected as active HTML.
            if (root.Descendants().Any(e => e.Name.LocalName is "script" or "foreignObject" or "image" or "use") ||
                root.DescendantsAndSelf().Attributes().Any(a =>
                    a.Name.LocalName.StartsWith("on", StringComparison.OrdinalIgnoreCase) ||
                    a.Name.LocalName == "href"))
                throw new InvalidDataException("Le SVG contient du contenu actif ou externe.");

            var viewBoxText = (string?)root.Attribute("viewBox") ?? "";
            var viewBox = ParseNumbers(viewBoxText);
            if (viewBox.Length != 4 || viewBox[2] <= 0 || viewBox[3] <= 0)
                throw new InvalidDataException("viewBox invalide.");
            var regions = JsonSerializer.Deserialize<RegionSource[]>(files[$"{name}.json"], jsonOptions)
                ?? throw new InvalidDataException("Coordonnées absentes.");
            var paths = root.Elements(svg + "path")
                .Where(e => (string?)e.Attribute("class") == "ayahPolygon").ToArray();
            if (regions.Length == 0 || regions.Length != paths.Length ||
                regions.Select(r => (r.SurahNumber, r.AyahNumber)).Distinct().Count() != regions.Length)
                throw new InvalidDataException("Références SVG/JSON incohérentes.");

            var validated = new List<WarshPilotRegion>();
            foreach (var region in regions)
            {
                if (region.SurahNumber is < 1 or > 114 || region.AyahNumber < 1)
                    throw new InvalidDataException("Référence d'ayah invalide.");
                var geometry = ParsePolygon(region.Polygon);
                var points = geometry.Points;
                for (var i = 0; i < points.Length; i += 2)
                    if (points[i] < viewBox[0] || points[i] > viewBox[0] + viewBox[2] ||
                        points[i + 1] < viewBox[1] || points[i + 1] > viewBox[1] + viewBox[3])
                        throw new InvalidDataException("Polygone hors de la page.");
                var path = paths.SingleOrDefault(p => (string?)p.Attribute("surah") == region.SurahNumber.ToString(CultureInfo.InvariantCulture)
                    && (string?)p.Attribute("ayah") == region.AyahNumber.ToString(CultureInfo.InvariantCulture));
                if (path == null) throw new InvalidDataException("Référence absente du SVG.");
                // This pinned source uses straight M/L/Z paths; reject any different geometry.
                var pathData = (string?)path.Attribute("d") ?? "";
                if (geometry.PathData != ParsePolygon(pathData).PathData)
                    throw new InvalidDataException("Polygones SVG/JSON différents.");
                validated.Add(new(region.SurahNumber, region.AyahNumber, geometry.PathData));
            }
            _images.Add(number, bytes);
            _pages.Add(number, new(number, viewBoxText, viewBox[2], viewBox[3], validated.AsReadOnly(),
                Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()));
        }
    }

    public WarshPilotPage? GetPage(int number) => _pages.GetValueOrDefault(number);
    public byte[]? GetImage(int number) => _images.TryGetValue(number, out var bytes) ? bytes.ToArray() : null;

    public static void Import(string source, string editionsRoot)
    {
        _ = new WarshPilotCorpus(source);
        var root = Path.GetFullPath(editionsRoot);
        var destination = Path.Combine(root, Revision);
        string[] names = ["manifest.json", "NOTICE.md", "LICENSE", "001.svg", "002.svg", "003.svg", "001.json", "002.json", "003.json"];
        if (Directory.Exists(destination))
        {
            _ = new WarshPilotCorpus(destination);
            foreach (var name in names)
                if (!File.ReadAllBytes(Path.Combine(source, name)).SequenceEqual(File.ReadAllBytes(Path.Combine(destination, name))))
                    throw new InvalidDataException("Cette révision existe déjà avec un contenu différent.");
            return;
        }
        Directory.CreateDirectory(root);
        var staging = Path.Combine(root, $".import-{Guid.NewGuid():N}");
        Directory.CreateDirectory(staging);
        try
        {
            foreach (var name in names) File.Copy(Path.Combine(source, name), Path.Combine(staging, name));
            _ = new WarshPilotCorpus(staging);
            Directory.Move(staging, destination);
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        }
    }

    private static (string PathData, double[] Points) ParsePolygon(string text)
    {
        // Opening pages use a point list; regular pages use multiple closed subpaths.
        // Keeping each M...Z is essential: merging them would cover neighbouring ayat.
        var parts = text.Trim().StartsWith('M')
            ? text.Trim().EndsWith('Z') ? text.Trim().Split('Z', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : throw new InvalidDataException("Tracé non fermé.")
            : [text];
        var paths = new List<string>();
        var allPoints = new List<double>();
        foreach (var part in parts)
        {
            var points = ParseNumbers((part.StartsWith('M') ? part[1..].Replace("L", " ") : part).Replace(',', ' '));
            if (points.Length < 6 || points.Length % 2 != 0) throw new InvalidDataException("Polygone invalide.");
            var pairs = points.Chunk(2).Select(p => $"{p[0].ToString(CultureInfo.InvariantCulture)} {p[1].ToString(CultureInfo.InvariantCulture)}");
            paths.Add("M " + string.Join(" L ", pairs) + " Z");
            allPoints.AddRange(points);
        }
        return (string.Join(" ", paths), allPoints.ToArray());
    }

    private static double[] ParseNumbers(string text)
    {
        var parts = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var result = new double[parts.Length];
        for (var i = 0; i < parts.Length; i++)
            if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out result[i]) ||
                !double.IsFinite(result[i]))
                throw new InvalidDataException("Coordonnée non numérique.");
        return result;
    }

    private sealed record BundleManifest(string Repository, string Revision, BundleFile[] Files);
    private sealed record BundleFile(string File, string Url, string Sha256);
    private sealed record RegionSource(int SurahNumber, int AyahNumber, string Polygon);
}

public sealed record WarshPilotRegion(int SurahNumber, int AyahNumber, string PathData, bool ContinuesFromPreviousPage = false)
{
    public string Reference => $"{SurahNumber}:{AyahNumber}";
}

public sealed record WarshPilotPage(int Number, string ViewBox, double Width, double Height,
    IReadOnlyList<WarshPilotRegion> Regions, string Sha256);
