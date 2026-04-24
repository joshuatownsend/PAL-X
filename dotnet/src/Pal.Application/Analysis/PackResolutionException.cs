namespace Pal.Application.Analysis;

public sealed class PackResolutionException : Exception
{
    public IReadOnlyList<string> Errors { get; }

    public PackResolutionException(IReadOnlyList<string> errors)
        : base(string.Join("; ", errors))
    {
        Errors = errors;
    }
}
