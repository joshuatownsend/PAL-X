namespace Pal.Cli;

public static class ExitCodes
{
    public const int Success = 0;
    public const int GeneralFailure = 1;
    public const int InvalidArguments = 2;
    public const int InputCollectorFailure = 3;
    public const int PackValidationFailure = 4;
    public const int AnalysisExecutionFailure = 5;
    public const int ReportGenerationFailure = 6;
}
