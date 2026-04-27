using System.Globalization;
using System.Text;
using ScottPlot;

namespace Pal.Reporting.Charts;

public static class ScottPlotRenderer
{
    private const int PlotWidth = 720;
    private const int PlotHeight = 360;

    // Frozen color palette — never use theme objects; always explicit hex
    private static Color DataLine => new(0x19, 0x76, 0xd2);
    private static Color WarningLine => new(0xff, 0xa7, 0x26);
    private static Color CriticalLine => new(0xd3, 0x2f, 0x2f);
    private static Color Background => new(0xff, 0xff, 0xff);

    public static string Render(
        string title,
        IReadOnlyList<(DateTimeOffset ts, double value)> series,
        double? warningThreshold = null,
        double? criticalThreshold = null,
        bool thresholdInverted = false)
    {
        var prevCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            return RenderCore(title, series, warningThreshold, criticalThreshold);
        }
        finally
        {
            CultureInfo.CurrentCulture = prevCulture;
        }
    }

    public static void RenderToFile(
        string title,
        IReadOnlyList<(DateTimeOffset ts, double value)> series,
        string outputPath,
        double? warningThreshold = null,
        double? criticalThreshold = null,
        bool thresholdInverted = false)
    {
        string svg = Render(title, series, warningThreshold, criticalThreshold, thresholdInverted);
        File.WriteAllText(outputPath, svg, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string RenderCore(
        string title,
        IReadOnlyList<(DateTimeOffset ts, double value)> series,
        double? warningThreshold,
        double? criticalThreshold)
    {
        var plt = new Plot();

        plt.FigureBackground.Color = Background;
        plt.DataBackground.Color = Background;

        plt.Title(title);
        plt.HideGrid();

        if (series.Count > 0)
        {
            double[] xs = [.. series.Select(p => p.ts.UtcDateTime.ToOADate())];
            double[] ys = [.. series.Select(p => p.value)];

            var scatter = plt.Add.Scatter(xs, ys);
            scatter.Color = DataLine;
            scatter.LineWidth = 1.5f;
            scatter.MarkerSize = 3f;

            plt.Axes.DateTimeTicksBottom();
        }

        if (warningThreshold.HasValue)
        {
            var hline = plt.Add.HorizontalLine(warningThreshold.Value);
            hline.Color = WarningLine;
            hline.LineWidth = 1f;
            hline.LinePattern = LinePattern.Dashed;
        }

        if (criticalThreshold.HasValue)
        {
            var hline = plt.Add.HorizontalLine(criticalThreshold.Value);
            hline.Color = CriticalLine;
            hline.LineWidth = 1f;
            hline.LinePattern = LinePattern.Dashed;
        }

        string rawSvg = plt.GetSvgXml(PlotWidth, PlotHeight);
        return SvgCanonicalizer.Canonicalize(rawSvg);
    }
}
