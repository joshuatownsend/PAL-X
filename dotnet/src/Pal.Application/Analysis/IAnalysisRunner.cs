namespace Pal.Application.Analysis;

public interface IAnalysisRunner
{
    AnalysisRunResult Run(AnalysisRunRequest request);
}
