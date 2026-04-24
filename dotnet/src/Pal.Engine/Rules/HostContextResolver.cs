using Pal.Engine.Model;

namespace Pal.Engine.Rules;

public static class HostContextResolver
{
    public sealed class MissingVariables
    {
        public IReadOnlyList<string> Variables { get; init; } = [];
        public bool Any => Variables.Count > 0;
    }

    public static MissingVariables FindMissing(Rule rule, HostContext ctx)
    {
        var missing = new List<string>();
        foreach (var condition in rule.Conditions)
        {
            if (condition.Threshold is HostContextThreshold hct)
            {
                if (!ctx.Resolve(hct.HostContextVariable).HasValue)
                    missing.Add(hct.HostContextVariable);
            }
        }
        return new MissingVariables { Variables = missing.Distinct().ToList() };
    }
}
