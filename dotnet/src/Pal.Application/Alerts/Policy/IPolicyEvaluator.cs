using Pal.Engine.Model;

namespace Pal.Application.Alerts.Policy;

public interface IPolicyEvaluator
{
    /// <summary>
    /// Inspects current findings against historical job results in the same workspace and
    /// returns side-channel adjustments for <see cref="AlertService"/> to apply.
    /// Findings themselves are immutable; adjustments are keyed by ruleId.
    /// </summary>
    Task<PolicyResult> EvaluateAsync(
        Guid workspaceId,
        IReadOnlyList<Finding> findings,
        CancellationToken ct = default);
}

public sealed record PolicyResult(
    IReadOnlyDictionary<string, PolicyEscalation> Escalations,
    IReadOnlySet<string> NotificationSuppressed)
{
    public static PolicyResult Empty { get; } = new(
        new Dictionary<string, PolicyEscalation>(StringComparer.OrdinalIgnoreCase),
        new HashSet<string>(StringComparer.OrdinalIgnoreCase));
}

public sealed record PolicyEscalation(
    string NewSeverity,
    string PolicyRuleId,
    string Reason);
