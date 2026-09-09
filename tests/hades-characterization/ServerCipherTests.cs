using Darkages.Network;
using Darkages.Security;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// The characterization client encrypts with the server's own cipher instead of a reimplementation, so
/// these tests only prove the reused assembly loads and applies the transform the server applies.
/// </summary>
public sealed class ServerCipherTests
{
    [Fact]
    public void Server_transform_is_self_inverse()
    {
        byte[] payload = [0x01, 0x02, 0x03, 0x04, 0x05];

        byte[] encrypted = Transform(ordinal: 1, payload);
        byte[] decrypted = Transform(ordinal: 1, encrypted);

        Assert.NotEqual(payload, encrypted);
        Assert.Equal(payload, decrypted);
    }

    private static byte[] Transform(byte ordinal, byte[] payload)
    {
        byte[] body = [0x57, ordinal, .. payload];
        NetworkPacket packet = new(body, body.Length);

        new SecurityProvider().Transform(packet);

        return packet.Data;
    }
}
