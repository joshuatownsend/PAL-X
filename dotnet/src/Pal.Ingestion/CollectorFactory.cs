using Pal.Engine.Normalization;
using Pal.Ingestion.Blg;
using Pal.Ingestion.Csv;

namespace Pal.Ingestion;

public static class CollectorFactory
{
    public static IDatasetCollector Create(string format, MetricAliasRegistry registry) => format switch
    {
        "blg" => CreateBlgCollector(registry),
        _ => new CsvCollector(registry)
    };

    private static IDatasetCollector CreateBlgCollector(MetricAliasRegistry registry)
    {
        if (OperatingSystem.IsWindows())
            return new BlgCollector(registry);
        return new BlgPlatformGuard();
    }
}
