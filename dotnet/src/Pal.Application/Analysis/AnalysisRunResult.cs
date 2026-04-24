using Pal.Engine.Model;
using Pal.Engine.Rules;

namespace Pal.Application.Analysis;

public sealed class AnalysisRunResult
{
    public required Dataset Dataset { get; init; }
    public required IReadOnlyList<Finding> Findings { get; init; }
    public required IReadOnlyList<PackResolutionInfo> PackResolutions { get; init; }
    public required IReadOnlyList<RuleEngine.EngineWarning> EngineWarnings { get; init; }
    public required IReadOnlyList<string> CollectorWarnings { get; init; }
}
