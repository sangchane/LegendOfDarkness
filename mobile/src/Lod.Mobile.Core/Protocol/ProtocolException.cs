namespace Lod.Mobile.Core.Protocol;

/// <summary>
/// Raised when bytes on the wire do not say what the protocol requires. Messages describe the shape that
/// was wrong and never repeat the content, because credentials pass through this code.
/// </summary>
public sealed class ProtocolException : Exception
{
    public ProtocolException(string message) : base(message)
    {
    }

    public ProtocolException(string message, Exception inner) : base(message, inner)
    {
    }
}
