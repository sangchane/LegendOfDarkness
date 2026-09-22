using System.Text.Json;
using Lod.Mobile.Core.Protocol;
using Lod.Mobile.Core.Protocol.Login;

namespace Lod.Mobile.Core.Tests.Protocol;

public sealed class Hades718LoginProtocolTests
{
    private const string SyntheticUsername = "mobile";
    private const string SyntheticPassword = "test";
    private static readonly LoginFixture Fixture = LoadFixture();

    [Fact]
    public void Version_request_matches_the_718_golden_vector()
    {
        byte[] request = Hades718LoginProtocol.CreateVersionRequest();

        Assert.Equal(Convert.FromHexString(Fixture.VersionRequestHex), request);
    }

    [Fact]
    public void Server_parameters_are_read_as_big_endian_and_static_key_bytes()
    {
        PacketFrame frame = DecodeFixture(Fixture.ServerParametersHex);

        EncryptionParameters parameters = Hades718LoginProtocol.ParseServerParameters(frame);

        Assert.Equal((byte)0, parameters.Seed);
        Assert.Equal(Convert.FromHexString("4E65786F6E496E632E"), parameters.Salt.ToArray());
        Assert.Equal(0x01020304u, parameters.ServerTableHash);
    }

    [Fact]
    public void Login_request_matches_the_Hades_default_cipher_vector()
    {
        EncryptionParameters parameters = new(0, "NexonInc."u8.ToArray(), 0);

        byte[] request = Hades718LoginProtocol.CreateLoginRequest(
            SyntheticUsername,
            SyntheticPassword,
            parameters,
            ordinal: 0);

        Assert.Equal(Convert.FromHexString(Fixture.LoginRequestHex), request);
    }

    [Fact]
    public void Account_request_frames_the_username_then_the_password()
    {
        EncryptionParameters parameters = new(0, "NexonInc."u8.ToArray(), 0);

        byte[] request = Hades718LoginProtocol.CreateAccountRequest(
            SyntheticUsername,
            SyntheticPassword,
            parameters,
            ordinal: 0);

        PacketFrame frame = DecodeFrame(request);
        Assert.Equal((byte)0x02, frame.Command);

        byte[] body = HadesCipher.DecodeSecured(frame, parameters);
        byte[] expected =
        [
            .. LegacyKoreanEncoding.EncodeStringA(SyntheticUsername),
            .. LegacyKoreanEncoding.EncodeStringA(SyntheticPassword)
        ];

        Assert.Equal(expected, body);
    }

    [Fact]
    public void Character_request_sends_appearance_then_selected_class()
    {
        EncryptionParameters parameters = new(0, "NexonInc."u8.ToArray(), 0);

        // Three different values in three different slots, so a swapped order in the implementation
        // shows up as a value landing in the wrong slot rather than a coincidental pass.
        const byte hairStyle = 0x0C;
        const byte gender = 0x02;
        const byte hairColor = 0x47;
        const byte path = 0x05;

        byte[] request = Hades718LoginProtocol.CreateCharacterRequest(
            hairStyle,
            gender,
            hairColor,
            path,
            parameters,
            ordinal: 0);

        PacketFrame frame = DecodeFrame(request);
        Assert.Equal((byte)0x04, frame.Command);

        byte[] body = HadesCipher.DecodeSecured(frame, parameters);

        // ClientFormat04 reads HairStyle, Gender, HairColor, then Path, in that order.
        Assert.Equal(new byte[] { hairStyle, gender, hairColor, path }, body);
    }

    [Fact]
    public void Login_request_errors_do_not_disclose_the_password()
    {
        EncryptionParameters invalid = new(10, "NexonInc."u8.ToArray(), 0);

        ProtocolException error = Assert.Throws<ProtocolException>(() =>
            Hades718LoginProtocol.CreateLoginRequest(
                SyntheticUsername,
                SyntheticPassword,
                invalid,
                ordinal: 0));

        Assert.DoesNotContain(SyntheticPassword, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Redirect_response_preserves_endpoint_and_game_entry_parameters()
    {
        PacketFrame frame = DecodeFixture(Fixture.RedirectHex);

        RedirectTarget redirect = Hades718LoginProtocol.ParseRedirect(frame);
        byte[] gameEntry = Hades718LoginProtocol.CreateGameEntryRequest(redirect);

        Assert.Equal("127.0.0.1", redirect.Address.ToString());
        Assert.Equal(2615, redirect.Port);
        Assert.Equal(SyntheticUsername, redirect.CharacterName);
        Assert.Equal(0x01020304u, redirect.Serial);
        Assert.Equal(Convert.FromHexString(Fixture.GameEntryHex), gameEntry);
    }

    [Fact]
    public void Redirect_rejects_a_non_redirect_command()
    {
        PacketFrame frame = DecodeFixture(Fixture.ServerParametersHex);

        Assert.Throws<ProtocolException>(() => Hades718LoginProtocol.ParseRedirect(frame));
    }

    private static PacketFrame DecodeFixture(string hex) => DecodeFrame(Convert.FromHexString(hex));

    private static PacketFrame DecodeFrame(byte[] encoded)
    {
        FrameReadStatus status = PacketFrameCodec.TryDecode(encoded, out PacketFrame? frame, out _);

        Assert.Equal(FrameReadStatus.Complete, status);
        return frame!;
    }

    private static LoginFixture LoadFixture()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "hades-718-login.json");
        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<LoginFixture>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("The Hades 7.18 fixture is empty.");
    }

    private sealed record LoginFixture(
        string VersionRequestHex,
        string ServerParametersHex,
        string LoginRequestHex,
        string RedirectHex,
        string GameEntryHex);
}
