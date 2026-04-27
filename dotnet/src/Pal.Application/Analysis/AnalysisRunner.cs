using Pal.Engine.Normalization;
using Pal.Engine.Rules;
using Pal.Ingestion;
using Pal.Ingestion.HostContext;
using Pal.Packs;

namespace Pal.Application.Analysis;

public sealed class AnalysisRunner : IAnalysisRunner
{
    public AnalysisRunResult Run(AnalysisRunRequest request)
    {
        var registry = MetricAliasRegistry.BuildDefault();
        var collector = CollectorFactory.Create(request.InputFormat, registry);

        var hostCtx = HostContextReader.Read(
            request.HostMemoryMb,
            request.HostCpuCount,
            sidecarPath: request.HostContextSidecarPath);

        var collectResult = collector.Collect(request.InputPath, request.MachineName, request.TimeZone);
        var dataset = collectResult.Dataset with { HostContext = hostCtx };

        var resolver = new PackResolver();
        var presentMetrics = dataset.Series.Select(s => s.CanonicalMetric).ToHashSet();
        var resolveResult = resolver.Resolve(request.PackIds, request.PackDirs, request.AutoResolvePacks, presentMetrics);

        if (resolveResult.Errors.Count > 0)
            throw new PackResolutionException(resolveResult.Errors);

        var engine = new RuleEngine();
        var engineResult = engine.Run(resolveResult.Packs, dataset);

        return new AnalysisRunResult
        {
            Dataset = dataset,
            Findings = engineResult.Findings,
            PackResolutions = resolveResult.Resolutions,
            EngineWarnings = engineResult.Warnings,
            CollectorWarnings = collectResult.Warnings,
            InputDigest = collectResult.InputDigest
        };
    }
}
