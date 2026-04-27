namespace Pal.Packs.Signing;

public sealed class PackSignatureException : Exception
{
    public PackSignatureException(string message) : base(message) { }
    public PackSignatureException(string message, Exception inner) : base(message, inner) { }
}
