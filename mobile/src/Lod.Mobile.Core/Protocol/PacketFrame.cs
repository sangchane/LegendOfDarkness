namespace Lod.Mobile.Core.Protocol;

/// <summary>One complete message: the command byte that selects a handler, and the bytes after it.</summary>
public sealed record PacketFrame(byte Command, ReadOnlyMemory<byte> Data);

/// <summary>What a decode attempt found. Incomplete is normal — the rest of the frame is still in flight.</summary>
public enum FrameReadStatus
{
    Incomplete,
    Complete,
    Invalid
}
