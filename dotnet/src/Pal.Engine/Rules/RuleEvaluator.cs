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

        if (condition.Window is not null)
            return EvaluateWindowed(condition, series, thresholdValue);

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

    private static Result EvaluateWindowed(Condition condition, TimeSeries series, double thresholdValue)
    {
        var win = condition.Window!;
        var windows = RollingWindowAggregator.Compute(
            series.Samples,
            TimeSpan.FromSeconds(win.DurationSeconds),
            win.StepSeconds.HasValue ? TimeSpan.FromSeconds(win.StepSeconds.Value) : null,
            condition.Aggregation,
            win.MinSamples);

        if (windows.Count == 0)
        {
            return new Result
            {
                Fired = false,
                ActualValue = 0,
                ThresholdValue = thresholdValue,
                Expression = BuildWindowExpression(condition, thresholdValue),
                SkipReason = "No windows had enough samples"
            };
        }

        bool fired = windows.Any(w => Compare(w.Value, condition.Operator, thresholdValue));

        // Report the "worst" window value: highest for gt/ge, lowest for lt/le, first match for eq
        double worstValue = condition.Operator switch
        {
            "gt" or "ge" => windows.Max(w => w.Value),
            "lt" or "le" => windows.Min(w => w.Value),
            _ => windows.FirstOrDefault(w => Compare(w.Value, condition.Operator, thresholdValue))?.Value
                 ?? windows[0].Value
        };

        return new Result
        {
            Fired = fired,
            ActualValue = worstValue,
            ThresholdValue = thresholdValue,
            Expression = BuildWindowExpression(condition, thresholdValue)
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

    private static string OperatorSymbol(string op) => op switch
    {
        "gt" => ">", "ge" => ">=", "lt" => "<", "le" => "<=", "eq" => "==", _ => op
    };

    private static string BuildExpression(Condition c, double resolvedThreshold)
    {
        string metric = c.Instance is not null ? $"{c.Metric}[{c.Instance}]" : c.Metric;
        string expr = $"{c.Aggregation}({metric}) {OperatorSymbol(c.Operator)} {resolvedThreshold:G}";
        if (c.DurationPercent > 1.0)
            expr += $" for >= {c.DurationPercent:G}% of samples";
        return expr;
    }

    private static string BuildWindowExpression(Condition c, double resolvedThreshold)
    {
        string metric = c.Instance is not null ? $"{c.Metric}[{c.Instance}]" : c.Metric;
        int secs = c.Window!.DurationSeconds;
        string windowLabel = secs % 3600 == 0 ? $"{secs / 3600}h"
            : secs % 60 == 0 ? $"{secs / 60}m"
            : $"{secs}s";
        return $"{c.Aggregation}({metric}) over {windowLabel} rolling window {OperatorSymbol(c.Operator)} {resolvedThreshold:G}";
    }
}
