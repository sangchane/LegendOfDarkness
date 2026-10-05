using Lod.Mobile.Core.Diagnostics;

namespace Lod.Mobile.Core.Protocol.World;

public sealed partial class WorldClient
{
    /// <summary>인증된 세계 연결로만 운영 신호를 보낸다.</summary>
    public Task SendActivityAsync(string json, CancellationToken cancellationToken) => Send(0xF3, AppActivity.Packet(json), cancellationToken);
    public event Action<string>? Diagnostic;
}
