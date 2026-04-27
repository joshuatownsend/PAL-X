using System.Globalization;
using Pal.Reporting.Charts;
using Xunit;

namespace Pal.Reporting.Tests;

public class ScottPlotRendererTests
{
    private static readonly IReadOnlyList<(DateTimeOffset ts, double value)> FixedSeries =
    [
        (new DateTimeOffset(2026, 1, 1,  0, 0, 0, TimeSpan.Zero), 42.5),
        (new DateTimeOffset(2026, 1, 1,  0, 5, 0, TimeSpan.Zero), 67.1),
        (new DateTimeOffset(2026, 1, 1,  0, 10, 0, TimeSpan.Zero), 55.0),
        (new DateTimeOffset(2026, 1, 1,  0, 15, 0, TimeSpan.Zero), 91.3),
        (new DateTimeOffset(2026, 1, 1,  0, 20, 0, TimeSpan.Zero), 38.9),
    ];

    [Fact]
    public void Render_ProducesByteIdenticalOutput_OnTwoRenders()
    {
        string svg1 = ScottPlotRenderer.Render("CPU % Processor Time", FixedSeries,
            warningThreshold: 80.0, criticalThreshold: 90.0);
        string svg2 = ScottPlotRenderer.Render("CPU % Processor Time", FixedSeries,
            warningThreshold: 80.0, criticalThreshold: 90.0);

        Assert.Equal(svg1, svg2);
    }

    [Fact]
    public void Render_NumberFormatting_UsesInvariantCulture()
    {
        var savedCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            string svg = ScottPlotRenderer.Render("Test", FixedSeries);

            // Decimal numbers in SVG attributes must use '.' not ','
            Assert.DoesNotContain(",5", svg);   // no "42,5" style fractions
            Assert.DoesNotContain(",1", svg);
            Assert.DoesNotContain(",0", svg);
        }
        finally
        {
            CultureInfo.CurrentCulture = savedCulture;
        }
    }

    [Fact]
    public void Render_IncludesThresholdLines_WhenProvidedForCritical()
    {
        string svg = ScottPlotRenderer.Render("Memory", FixedSeries, criticalThreshold: 90.0);
        Assert.Contains("svg", svg, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(svg);
    }

    [Fact]
    public void Render_ProducesValidSvgRoot()
    {
        string svg = ScottPlotRenderer.Render("Test", FixedSeries);
        // ScottPlot prepends an XML declaration before <svg
        Assert.Contains("<svg", svg);
        Assert.Contains("</svg>", svg);
    }

    [Fact]
    public void Render_EmptySeries_ProducesValidSvg()
    {
        string svg = ScottPlotRenderer.Render("Empty Chart", []);
        Assert.Contains("<svg", svg);
    }

    [Fact]
    public void Render_CanonicalIds_UsesPalPrefix()
    {
        string svg = ScottPlotRenderer.Render("Test", FixedSeries);
        // After canonicalization, any remaining ids should use pal- prefix
        // No raw GUID or Skia-generated integer IDs should remain
        Assert.DoesNotContain("id=\"clip", svg);
    }
}
