using System.Net;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 그룹(화면에서는 파티) — 모바일 알맹이로 원작 흐름을 끝까지 탄다: 청하기(0x2E 2) → 상대에게 묻기(0x63) → 받아들이기
/// (0x2E 3) → 프로필(0x39)에 둘이 선다 → 그룹말(0x19 "!" → 0x0A 11) → 한 사람이 잡은 괴물의 경험치가 곁의 그룹원에게도
/// 간다 → 제 이름을 청해 나간다.
/// </summary>
/// <remarks>
/// 전에는 청하는 즉시 넣어 버렸고(묻지 않았다) 그룹말이 없었다 — "!" 는 그런 사람이 없다는 답만 받았다.
/// </remarks>
[Collection(TimedCollection.Name)]
public sealed class PartyTests : IDisposable
{
    private const string Leader = "partylead";
    private const string Member = "partymate";

    /// <summary>노비스평원A 가운데. 괴물이 서 있는 곳이라 경험치 나누기까지 한 자리에서 본다(NoviceHuntingGroundTests).</summary>
    private static readonly (int Map, int X, int Y) Plain = (20393, 25, 25);

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(6));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task Asked_accepted_talked_shared_and_left()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: Plain);
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, Leader);
        LoginFlow.TryCreateAccount(server, Member);

        (WorldSession leadSession, WorldClient lead, Heard leadHeard) = await Enter(server, Leader);
        using WorldSession _ = leadSession;
        (WorldSession mateSession, WorldClient mate, Heard mateHeard) = await Enter(server, Member);
        using WorldSession __ = mateSession;

        await Waiting.Until(() => lead.State is not null && mate.State is not null && mate.Self?.Name is { Length: > 0 },
            "둘이 월드에 서지 못했습니다.", _deadline.Token);

        // 청하면 묻는다 — 아직 들어오지 않았다. 들어온 직후 서버가 화면을 새로 보내는 동안은 청을 버리므로
        // (Format2EHandler 의 IsRefreshing) 물어 올 때까지 다시 청한다.
        string? asker = null;

        for (int tries = 0; tries < 15 && asker is null; tries++)
        {
            await lead.AskToGroupAsync(Member, _deadline.Token);

            try
            {
                await Waiting.Until(() => mate.TakeAsk(out asker), "0x63", _deadline.Token, TimeSpan.FromSeconds(1));
            }
            catch (TimeoutException)
            {
            }
        }

        Assert.True(asker is not null, $"0x63 으로 묻지 않았습니다. 청한 쪽이 들은 것: {leadHeard.All()}");
        Assert.Equal(Leader, asker);

        await Roster(lead);
        Assert.False(lead.Roster.Grouped, "받아들이기 전에 벌써 그룹이 되었습니다.");

        // 받아들이면 둘이 선다. 청한 이가 그룹장이다.
        await mate.AcceptGroupAsync(Leader, _deadline.Token);
        await Waiting.Until(() => leadHeard.Any(line => line.Text.Contains(Member, StringComparison.Ordinal)),
            "그룹장이 들어왔다는 말을 듣지 못했습니다.", _deadline.Token);

        await Roster(lead);
        await Roster(mate);
        // 차례는 서버가 사람을 담아 둔 차례다 — 누가 먼저인지는 뜻이 없다.
        Assert.Equal([new PartyMember(Leader, true), new PartyMember(Member, false)], lead.Roster.Members.OrderBy(m => m.Name));
        Assert.Equal(lead.Roster.Members.OrderBy(m => m.Name), mate.Roster.Members.OrderBy(m => m.Name));

        // 그룹말은 둘 다 듣는다 — 0x0A 11.
        await lead.SayToGroupAsync("이쪽으로", _deadline.Token);
        string said = $"[그룹말]{Leader}: 이쪽으로";

        await Waiting.Until(() => mateHeard.Any(line => line.Type == 11 && line.Text == said) &&
                                  leadHeard.Any(line => line.Type == 11 && line.Text == said),
            $"그룹말이 둘 다에게 가지 않았습니다. 들은 것: {mateHeard.All()}",
            _deadline.Token);

        // 그룹장이 잡은 괴물의 경험치가 곁의 그룹원에게도 간다 (monsterexp.cs GenerateExperience).
        long mateBefore = mate.Vitals?.Experience ?? 0;
        await Hunt(lead, () => mateHeard.Any(line => line.Text.StartsWith("경험치가 ", StringComparison.Ordinal)));

        Assert.True(leadHeard.Any(line => line.Text.StartsWith("경험치가 ", StringComparison.Ordinal)), leadHeard.All());
        await Waiting.Until(() => (mate.Vitals?.Experience ?? 0) > mateBefore,
            "그룹원의 경험치 숫자가 오르지 않았습니다.", _deadline.Token);

        // 제 이름을 청하면 나간다. 둘뿐이었으니 그룹이 흩어진다.
        await mate.LeaveGroupAsync(_deadline.Token);
        // 5.99 서버의 말: 나간 이는 "…님 그룹 해체", 흩어진 그룹은 "그룹 해체".
        await Waiting.Until(() => leadHeard.Any(line => line.Text == "그룹 해체"),
            "그룹이 흩어졌다는 말을 듣지 못했습니다.", _deadline.Token);

        await Roster(lead);
        await Roster(mate);
        Assert.False(lead.Roster.Grouped);
        Assert.False(mate.Roster.Grouped);

        // 그룹이 없으면 그룹말도 없다.
        await lead.SayToGroupAsync("아무도", _deadline.Token);
        await Waiting.Until(() => leadHeard.Any(line => line.Text == "그룹이 없습니다."),
            "그룹 없이 한 그룹말을 거절하지 않았습니다.", _deadline.Token);
    }

    /// <summary>프로필을 새로 받아 올 때까지 기다린다.</summary>
    private async Task Roster(WorldClient world)
    {
        int before = world.RosterCount;
        await world.AskProfileAsync(_deadline.Token);
        await Waiting.Until(() => world.RosterCount > before, "프로필(0x39)이 오지 않았습니다.", _deadline.Token);
    }

    /// <summary>
    /// 가장 가까운 괴물에게 걸어가 때린다 — 곁에 오면 돌아서서 치고, 아니면 한 칸 다가간다. 막히면 옆으로 한 칸.
    /// </summary>
    private async Task Hunt(WorldClient world, Func<bool> done)
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromMinutes(3);
        int turn = 0;

        while (!done())
        {
            if (DateTime.UtcNow > giveUp)
            {
                throw new TimeoutException($"3분 안에 괴물을 잡아 그룹원에게 경험치가 가지 않았습니다. 서버가 한 말: {world.Said}");
            }

            if (world.State?.Where is not { } here ||
                world.Creatures.Where(c => c.Kind == CreatureKind.Hostile)
                    .OrderBy(c => Math.Abs(c.Where.X - here.X) + Math.Abs(c.Where.Y - here.Y))
                    .FirstOrDefault() is not { } prey)
            {
                await world.RefreshAsync(_deadline.Token);
                await Task.Delay(500, _deadline.Token);
                continue;
            }

            int dx = prey.Where.X - here.X;
            int dy = prey.Where.Y - here.Y;
            Direction toward = Math.Abs(dx) >= Math.Abs(dy)
                ? dx > 0 ? Direction.East : Direction.West
                : dy > 0 ? Direction.South : Direction.North;

            if (Math.Abs(dx) + Math.Abs(dy) == 1)
            {
                await world.TurnAsync(toward, _deadline.Token);
                await world.AttackAsync(_deadline.Token);
                await Task.Delay(700, _deadline.Token);
                continue;
            }

            // 막혀 제자리면 옆으로 비킨다.
            Direction step = turn++ % 5 == 4 ? (Direction)(((int)toward + 1) % 4) : toward;
            await world.WalkAsync(step, _deadline.Token);
            await Task.Delay(450, _deadline.Token);
        }
    }

    private async Task<(WorldSession Session, WorldClient World, Heard Heard)> Enter(IsolatedHadesServer server, string who)
    {
        WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, who, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        Heard heard = new();
        _ = heard.Collect(world, _deadline.Token);

        return (session, world, heard);
    }

    /// <summary>서버가 한 말(0x0A)을 종류와 함께 모두 모은다 — 알맹이는 꺼내 가면 지우므로 한 곳에서 꺼낸다.</summary>
    private sealed class Heard
    {
        private readonly List<(byte Type, string Text)> _lines = [];

        public bool Any(Func<(byte Type, string Text), bool> match)
        {
            lock (_lines)
            {
                return _lines.Any(match);
            }
        }

        public string All()
        {
            lock (_lines)
            {
                return string.Join(" | ", _lines.Select(line => $"{line.Type}:{line.Text}"));
            }
        }

        public async Task Collect(WorldClient world, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                while (world.TakeTold(out byte type, out string text))
                {
                    lock (_lines)
                    {
                        _lines.Add((type, text));
                    }
                }

                try
                {
                    await Task.Delay(50, token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }
}
