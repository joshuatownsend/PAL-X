namespace Pal.Ingestion.Blg;

public static class BlgCollectorStub
{
    public static void ThrowNotSupported(string blgPath)
    {
        string stem = Path.GetFileNameWithoutExtension(blgPath);
        string csvPath = Path.Combine(Path.GetDirectoryName(blgPath) ?? ".", stem + ".csv");
        throw new NotSupportedException(
            $"BLG import is not supported in Phase 1. Convert your log first:\n" +
            $"  relog -f CSV \"{blgPath}\" -o \"{csvPath}\"\n" +
            $"Then re-run with the CSV file. PDH interop lands in Phase 1.5.");
    }
}
