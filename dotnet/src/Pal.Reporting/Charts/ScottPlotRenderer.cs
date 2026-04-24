namespace Pal.Reporting.Charts;

// ScottPlot SVG chart rendering — Phase 1.5
// Placeholder: charts are omitted when --include-charts is not set.
// When --include-charts IS set, this class will render threshold-annotated line charts
// using ScottPlot 5.x and save them as SVG files.
public static class ScottPlotRenderer
{
    public static string Render(string title, IReadOnlyList<(DateTimeOffset ts, double value)> series,
        double? warningThreshold = null, double? criticalThreshold = null, bool thresholdInverted = false)
    {
        // Phase 1: return a placeholder SVG
        return $"""
            <svg xmlns="http://www.w3.org/2000/svg" width="600" height="200">
              <rect width="600" height="200" fill="#f5f5f5"/>
              <text x="300" y="100" text-anchor="middle" font-family="sans-serif" font-size="14" fill="#999">
                Chart: {System.Net.WebUtility.HtmlEncode(title)} (chart rendering requires --include-charts)
              </text>
            </svg>
            """;
    }
}
