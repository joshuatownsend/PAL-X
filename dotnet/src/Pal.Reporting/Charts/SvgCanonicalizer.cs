using System.Text;
using System.Text.RegularExpressions;

namespace Pal.Reporting.Charts;

internal static partial class SvgCanonicalizer
{
    // Matches id="anything-here" within SVG elements
    [GeneratedRegex(@"id=""([^""]+)""", RegexOptions.Compiled)]
    private static partial Regex IdAttributeRegex();

    // Matches url(#something) references in attributes like clip-path, fill, etc.
    [GeneratedRegex(@"url\(#([^)]+)\)", RegexOptions.Compiled)]
    private static partial Regex UrlRefRegex();

    // Matches XML/SVG comments <!-- ... -->
    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Compiled | RegexOptions.Singleline)]
    private static partial Regex CommentRegex();

    // Matches <metadata>...</metadata> blocks (may contain version stamps)
    [GeneratedRegex(@"<metadata>.*?</metadata>", RegexOptions.Compiled | RegexOptions.Singleline)]
    private static partial Regex MetadataRegex();

    public static string Canonicalize(string svg)
    {
        // 1. Strip comments (may embed build timestamps or ScottPlot version)
        svg = CommentRegex().Replace(svg, string.Empty);

        // 2. Strip metadata blocks
        svg = MetadataRegex().Replace(svg, string.Empty);

        // 3. Collect all id= definitions in document order and build a canonical mapping
        var idMatches = IdAttributeRegex().Matches(svg);
        var idMap = new Dictionary<string, string>(StringComparer.Ordinal);
        int counter = 0;
        foreach (Match m in idMatches)
        {
            string original = m.Groups[1].Value;
            if (!idMap.ContainsKey(original))
                idMap[original] = $"pal-{counter++}";
        }

        if (idMap.Count == 0)
            return svg;

        // 4. Replace id="..." definitions
        svg = IdAttributeRegex().Replace(svg, m =>
        {
            string orig = m.Groups[1].Value;
            return idMap.TryGetValue(orig, out string? canonical)
                ? $"id=\"{canonical}\""
                : m.Value;
        });

        // 5. Replace url(#...) references
        svg = UrlRefRegex().Replace(svg, m =>
        {
            string orig = m.Groups[1].Value;
            return idMap.TryGetValue(orig, out string? canonical)
                ? $"url(#{canonical})"
                : m.Value;
        });

        return svg;
    }
}
