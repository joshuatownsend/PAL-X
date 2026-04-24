using Pal.Engine.Model;
using Pal.Engine.Statistics;

namespace Pal.Engine.Rules;

public static class RuleEvaluator
{
    public sealed class Result
    {
        public required bool Fired { get; init; }
        public required double ActualValue { get; init; }
        public required double ThresholdValue { get; init; }
        public required string Expression { get; init; }
        public string? SkipReason { get; init; }
    }

    public static Result Evaluate(Condition condition, TimeSeries series, HostContext hostContext)
    {
        double thresholdValue;
        try
        {
            thresholdValue = condition.Threshold.Resolve(hostContext);
        }
        catch (InvalidOperationException ex)
        {
            return new Result
            {
                Fired = false,
                ActualValue = 0,
                ThresholdValue = 0,
                Expression = BuildExpression(condition, 0),
                SkipReason = ex.Message
            };
        }

        if (condition.Aggregation == "trend")
        {
            var stats = series.Statistics ?? SeriesStatisticsCalculator.Compute(series.Samples);
            double trendValue = stats.TrendPerHour;
            bool fired = Compare(trendValue, condition.Operator, thresholdValue);
            return new Result
            {
                Fired = fired,
                ActualValue = trendValue,
                ThresholdValue = thresholdValue,
                Expression = BuildExpression(condition, thresholdValue)
            };
        }

        var validSamples = series.Samples.Where(s => s.Value.HasValue).ToList();
        if (validSamples.Count == 0)
        {
            return new Result
            {
                Fired = false,
                ActualValue = 0,
                ThresholdValue = thresholdValue,
                Expression = BuildExpression(condition, thresholdValue),
                SkipReason = "No valid samples"
            };
        }

        int satisfying = validSamples.Count(s => Compare(s.Value!.Value, condition.Operator, thresholdValue));
        double actualPercent = satisfying * 100.0 / validSamples.Count;
        bool fires = actualPercent >= condition.DurationPercent;

        // For the "actual value" shown in the report, use the statistical aggregation
        var statsForReport = series.Statistics ?? SeriesStatisticsCalculator.Compute(series.Samples);
        double reportValue = SeriesStatisticsCalculator.GetAggregation(statsForReport, condition.Aggregation);

        return new Result
        {
            Fired = fires,
            ActualValue = reportValue,
            ThresholdValue = thresholdValue,
            Expression = BuildExpression(condition, thresholdValue),
            SkipReason = null
        };
    }

    private static bool Compare(double value, string op, double threshold) => op switch
    {
        "gt" => value > threshold,
        "ge" => value >= threshold,
        "lt" => value < threshold,
        "le" => value <= threshold,
        "eq" => Math.Abs(value - threshold) < double.Epsilon,
        _ => throw new ArgumentException($"Unknown operator: {op}")
    };

    private static string BuildExpression(Condition c, double resolvedThreshold)
    {
        string metric = c.Instance is not null ? $"{c.Metric}[{c.Instance}]" : c.Metric;
        string op = c.Operator switch { "gt" => ">", "ge" => ">=", "lt" => "<", "le" => "<=", "eq" => "==", _ => c.Operator };
        string expr = $"{c.Aggregation}({metric}) {op} {resolvedThreshold:G}";
        if (c.DurationPercent > 1.0)
            expr += $" for >= {c.DurationPercent:G}% of samples";
        return expr;
    }
}
