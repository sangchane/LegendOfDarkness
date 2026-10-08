using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Protocol;

namespace Lod.Mobile.Core.Tests.Net;

/// <summary>
/// 로그인 전체 제한시간(리뷰 2026-10-08 #8) — 접속만 받고 말이 없는 서버 앞에서 어느 단계든 제한시간 안에
/// <see cref="TimeoutException" /> 으로 끝나고, 그 뒤 같은 함수로 다시 들어갈 수 있다.
/// </summary>
public sealed class HadesLoginClientTests
{
    private static readonly TimeSpan Short = TimeSpan.FromMilliseconds(300);

    /// <summary>제한시간이 안 걸리면 시험이 멈추지 않게 — 이만큼 지나도 안 끝나면 실패. 자격 증명 뒤 기다림(10초)보다 짧다.</summary>
    private static readonly TimeSpan Guard = TimeSpan.FromSeconds(5);

    private static readonly byte[] Hello = PacketFrameCodec.Encode([0x7E, 0x1B]);

    /// <summary><c>hades-718-login.json</c> serverParametersHex.</summary>
    private static readonly byte[] Parameters = Convert.FromHexString("AA001100000102030400094E65786F6E496E632E");

    /// <summary>
    /// 단계 0 첫 인사 전 · 1 암호 매개변수 전 · 2 로비 인사 전 · 3 로그인은 자격 증명 뒤(안내 기다림), 만들기는 계정 요청 뒤.
    /// </summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public async Task A_silent_server_fails_within_the_deadline_at_each_step_and_the_next_try_works(int stage, bool create)
    {
        using FakeServer server = new();
        byte[] greeting = [.. Hello, .. Parameters, .. server.Redirect()];
        server.Expect(stage switch
        {
            0 => [[]],
            1 => [Hello],
            2 => [greeting, []],
            _ => [greeting, [.. Hello, .. Hello]],
        });

        Task<WorldSession> silent = Attempt(server, create, Short);
        Assert.Same(silent, await Task.WhenAny(silent, Task.Delay(Guard)));
        TimeoutException late = await Assert.ThrowsAsync<TimeoutException>(() => silent);
        Assert.Equal("서버가 응답하지 않습니다", late.Message);

        // 서버가 답하기 시작하면 같은 함수로 다시 들어간다.
        byte[] lobby = [.. Hello, .. Hello, .. server.Redirect()];
        server.Expect(create ? [greeting, [.. Hello, .. Hello, .. Hello, .. Hello], greeting, lobby, []] : [greeting, lobby, []]);
        using WorldSession session = await Attempt(server, create, Guard);
        Assert.Equal(server.Port, session.Character.Port);
    }

    [Fact]
    public async Task The_callers_own_cancel_stays_a_cancel_not_a_timeout()
    {
        using FakeServer server = new();
        using CancellationTokenSource leaving = new(Short);

        Task<WorldSession> attempt = Attempt(server, create: false, TimeSpan.FromSeconds(30), leaving.Token);

        Assert.Same(attempt, await Task.WhenAny(attempt, Task.Delay(Guard)));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => attempt);
    }

    private static Task<WorldSession> Attempt(FakeServer server, bool create, TimeSpan timeout, CancellationToken token = default) => create
        ? HadesLoginClient.CreateCharacterAsync(IPAddress.Loopback, server.Port, "tester", "1234", 1, 1, 1, 1, cancellationToken: token, timeout: timeout)
        : HadesLoginClient.LoginAsync(IPAddress.Loopback, server.Port, "tester", "1234", cancellationToken: token, timeout: timeout);

    /// <summary>접속을 받을 때마다 정해 둔 바이트를 차례로 한 번에 보내고 입을 닫는(연결은 열어 둔) 서버.</summary>
    private sealed class FakeServer : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly ConcurrentQueue<byte[]> _scripts = new();
        private readonly List<TcpClient> _accepted = [];

        public FakeServer()
        {
            _listener.Start();
            _ = Serve();
        }

        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

        public void Expect(byte[][] scripts)
        {
            foreach (byte[] script in scripts)
            {
                _scripts.Enqueue(script);
            }
        }

        /// <summary>로비·게임 안내(<c>hades-718-login.json</c> redirectHex) — 포트만 이 서버 것으로.</summary>
        public byte[] Redirect()
        {
            byte[] frame = Convert.FromHexString("AA001E030100007F0A371600094E65786F6E496E632E066D6F62696C6501020304");
            frame[8] = (byte)(Port >> 8);
            frame[9] = (byte)Port;
            return frame;
        }

        private async Task Serve()
        {
            try
            {
                while (true)
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    lock (_accepted)
                    {
                        _accepted.Add(client);
                    }

                    if (_scripts.TryDequeue(out byte[]? script))
                    {
                        await client.GetStream().WriteAsync(script);
                    }
                }
            }
            catch (Exception failed) when (failed is SocketException or ObjectDisposedException or IOException)
            {
                // 시험이 끝나 닫혔다.
            }
        }

        public void Dispose()
        {
            _listener.Stop();
            lock (_accepted)
            {
                _accepted.ForEach(client => client.Dispose());
            }
        }
    }
}
