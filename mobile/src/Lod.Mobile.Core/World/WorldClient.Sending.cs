using System.Buffers.Binary;
using System.Collections.Concurrent;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Protocol;
using Lod.Mobile.Core.Protocol.Login;

namespace Lod.Mobile.Core.World;

/// <summary>월드 연결 — 서버로 보내는 요청(걷기·말하기·아이템·기술·거래·나가기).</summary>
public sealed partial class WorldClient
{
    /// <summary>로그아웃이 서버의 0x4C 를 기다리는 가장 긴 시간. 오지 않아도 소켓을 닫으면 서버는 곧 뺀다.</summary>
    public static readonly TimeSpan LogOutWait = TimeSpan.FromSeconds(1);

    private const byte ClickBySerial = 0x01;

    /// <summary>
    /// Which pack slot to put a picked-up thing in. The server finds a free one itself
    /// (<c>Format07Handler</c> hands the item to <c>GiveTo</c>, which does not read this), so nothing is
    /// gained by choosing — and choosing wrongly would be a way to lose things.
    /// </summary>
    private const byte AnyPackSlot = 0;

    /// <summary>소지품 칸을 가리키는 번호. 주문·기술 칸도 같은 명령을 쓴다.</summary>
    private const byte InventoryPane = 0x00;

    /// <summary>Says we are taking one step. The count rises so the server can see how fast we claim to move.</summary>
    public Task WalkAsync(Direction direction, CancellationToken cancellationToken) =>
        Send(ClientOpcode.Walk, [ToServer(direction), _step++], cancellationToken);

    /// <summary>
    /// Turns on the spot, without claiming a step.
    /// </summary>
    /// <remarks>
    /// A blow lands in the direction the server has us facing, and the only other thing that sets it is a
    /// step. Walking into the tile a monster stands on to face it is refused — rightly, it is occupied —
    /// and the refusal sends us back where we were, which on screen reads as being yanked a tile. This is
    /// the packet the original client sends for that, so facing costs nothing.
    /// </remarks>
    public Task TurnAsync(Direction direction, CancellationToken cancellationToken) =>
        Send(ClientOpcode.Turn, [ToServer(direction)], cancellationToken);

    /// <summary>
    /// Picks one of the choices an NPC is offering. <paramref name="choice" /> is the number the server put
    /// beside that line, counted from one.
    /// </summary>
    /// <remarks>
    /// Reading a dialogue was all this client could do — it took <c>0x2F</c> and showed the words, and there
    /// was no way to answer. That leaves every NPC that asks something unreachable, the class chooser
    /// among them, so a character can never stop being a peasant.
    /// <c>GameServerHandlers.Format3AHandler</c> reads a kind byte, the speaker's serial, a script number
    /// and the choice. <b>Not <c>0x39</c></b> — that one answers a menu a script is walking somebody
    /// through, and a choice sent there is looked up in a menu that is not open, so nothing happens at all
    /// and nothing is logged. Numbers go out most significant byte first
    /// (<c>NetworkPacketReader.ReadUInt16</c> shifts the first byte up).
    /// </remarks>
    public Task AnswerAsync(uint speaker, ushort choice, CancellationToken cancellationToken) =>
        Answer(speaker, 0x0000, choice, [NothingTyped], cancellationToken);

    /// <summary>
    /// Answers with words as well — what was typed, or what the window asked to have handed back (the thing to buy,
    /// the slot to sell). <b>Not <c>0x39</c></b> for this either: <c>ClientFormat39</c> reads its words as ASCII, so a
    /// Korean item name arrives as question marks and the shop finds nothing by it. <c>ClientFormat3A</c> reads them
    /// in the server's own code page.
    /// </summary>
    public Task AnswerAsync(uint speaker, ushort choice, string words, CancellationToken cancellationToken) =>
        Answer(speaker, 0x0000, choice, [WordsTyped, .. LegacyKoreanEncoding.EncodeStringA(words)], cancellationToken);

    /// <summary>
    /// Says the window was shut from our side, so the server stops walking us through a menu
    /// (<c>Format3AHandler</c>: step 0 with script 0xFFFF closes the dialog).
    /// </summary>
    public Task ShutDialogueAsync(CancellationToken cancellationToken) =>
        Answer(0, 0xFFFF, 0x0000, [NothingTyped], cancellationToken);

    private Task Answer(uint speaker, ushort script, ushort choice, byte[] tail, CancellationToken cancellationToken) =>
        SendDialog(
            ClientOpcode.Answer,
            [
                MundaneSpeaker,
                (byte)(speaker >> 24), (byte)(speaker >> 16), (byte)(speaker >> 8), (byte)speaker,
                (byte)(script >> 8), (byte)script,
                (byte)(choice >> 8), (byte)choice,
                .. tail
            ],
            cancellationToken);

    /// <summary>The kind byte for a person standing in the world, as against a sign or a menu of our own.</summary>
    private const byte MundaneSpeaker = 0x01;

    /// <summary>Closes the packet where a typed line would go. <c>0x02</c> there means one follows.</summary>
    private const byte NothingTyped = 0x01;

    private const byte WordsTyped = 0x02;

    /// <summary>
    /// Says something out loud. The server treats a line beginning with a known word as a command when the
    /// speaker is allowed to give one, which is how a test gets an item into an empty pack.
    /// </summary>
    public Task SayAsync(string text, CancellationToken cancellationToken) =>
        Send(ClientOpcode.Talk, [0, .. LegacyKoreanEncoding.EncodeStringA(text)], cancellationToken);

    /// <summary>Asks somebody standing near us to join our group. They are asked, not added (<see cref="Party" />).</summary>
    public Task AskToGroupAsync(string name, CancellationToken cancellationToken) =>
        Send(ClientOpcode.Group, Party.Ask(name), cancellationToken);

    /// <summary>Takes the ask of the person who asked us. There is no "no" on the wire — not answering is the no.</summary>
    public Task AcceptGroupAsync(string name, CancellationToken cancellationToken) =>
        Send(ClientOpcode.Group, Party.Accept(name), cancellationToken);

    /// <summary>Leaves the group the original way: by asking ourselves. Nothing is sent before the server has named us.</summary>
    /// <summary>Turns taking group requests on or off. The server says nothing back — ask the profile again to see it.</summary>
    public Task ToggleGroupAsync(CancellationToken cancellationToken) => Send(ClientOpcode.GroupToggle, [], cancellationToken);

    public Task LeaveGroupAsync(CancellationToken cancellationToken) =>
        _world.Self?.Name is { Length: > 0 } mine
            ? Send(ClientOpcode.Group, Party.Ask(mine), cancellationToken)
            : Task.CompletedTask;

    /// <summary>Says something to everyone in our group (a whisper to "!").</summary>
    public Task SayToGroupAsync(string text, CancellationToken cancellationToken) =>
        Send(ClientOpcode.Whisper, Party.Chat(text), cancellationToken);

    /// <summary>상점에서 여러 품목을 한 번에 사고 판다.</summary>
    public Task BulkTradeAsync(uint merchant, bool selling, IReadOnlyList<(string Name, int Slot, int Quantity)> lines, CancellationToken cancellationToken)
    {
        return Send(ClientOpcode.BulkTrade, EncodeBulkTrade(selling, merchant, lines), cancellationToken);
    }

    private static byte[] EncodeBulkTrade(bool selling, uint merchant, IReadOnlyList<(string Name, int Slot, int Quantity)> lines)
    {
        IReadOnlyList<(string Name, int Slot, int Quantity)> selected = lines.Take(128).ToArray();
        List<byte> body = EncodeTradeHeader(selling, merchant, selected.Count);
        foreach (var line in selected)
        {
            body.AddRange(EncodeTradeLine(selling, line));
        }

        return body.ToArray();
    }

    private static List<byte> EncodeTradeHeader(bool selling, uint merchant, int count) =>
        [selling ? (byte)2 : (byte)1,
         (byte)(merchant >> 24), (byte)(merchant >> 16), (byte)(merchant >> 8), (byte)merchant,
         (byte)(count >> 8), (byte)count];

    private static IEnumerable<byte> EncodeTradeLine(bool selling, (string Name, int Slot, int Quantity) line)
    {
        if (!selling)
        {
            foreach (byte part in LegacyKoreanEncoding.EncodeStringA(line.Name))
            {
                yield return part;
            }
        }
        else
        {
            yield return (byte)line.Slot;
        }

        yield return (byte)(line.Quantity >> 8);
        yield return (byte)line.Quantity;
    }

    /// <summary>상점 구매/판매 첫 메뉴로 돌아간다(0xF2 kind 3).</summary>
    public Task ShopMenuAsync(uint merchant, CancellationToken cancellationToken)
    {
        byte[] body = [3,
            (byte)(merchant >> 24), (byte)(merchant >> 16), (byte)(merchant >> 8), (byte)merchant,
            0, 0];
        return Send(ClientOpcode.BulkTrade, body, cancellationToken);
    }

    /// <summary>Asks for our own profile, which is where the server lists the group.</summary>
    public Task AskProfileAsync(CancellationToken cancellationToken) =>
        Send(ClientOpcode.ProfileRequest, [], cancellationToken);

    /// <summary>
    /// Strikes whatever is in front of us. The server decides whether that hits anything — it knows where
    /// everyone stands and how recently we last swung — so nothing is assumed here about the outcome.
    /// One tap is one blow: it does not chase and does not repeat.
    /// </summary>
    public Task AttackAsync(CancellationToken cancellationToken) =>
        Send(ClientOpcode.Attack, [], cancellationToken);

    /// <summary>Activates one learned technique by its server-owned pane slot.</summary>
    public Task UseSkillAsync(int slot, CancellationToken cancellationToken) =>
        Send(ClientOpcode.UseSkill, [(byte)slot], cancellationToken);

    /// <summary>
    /// Casts one learned spell. Hades reads the four bytes following the slot as the target serial; zero
    /// means the caster, which is also the safe answer for a spell that does not ask for a target.
    /// The final zero terminates the legacy argument field read by the same packet parser.
    /// </summary>
    public Task UseSpellAsync(int slot, uint target, CancellationToken cancellationToken) =>
        Send(
            ClientOpcode.UseSpell,
            [(byte)slot, (byte)(target >> 24), (byte)(target >> 16), (byte)(target >> 8), (byte)target, 0],
            cancellationToken);

    /// <summary>
    /// Uses what is in one pack slot. What that means is the item's own business — boots are worn,
    /// food is eaten — so nothing is assumed here beyond the slot number. The server answers a piece
    /// of clothing by describing us again, which is how the figure comes to be redrawn.
    /// </summary>
    public Task UseAsync(int slot, CancellationToken cancellationToken) =>
        Send(ClientOpcode.Use, [(byte)slot], cancellationToken);

    /// <summary>
    /// Throws one pack slot on the floor. The server decides whether it may be thrown at all — some things
    /// are not — and it lands on the tile we name, which is our own.
    /// </summary>
    public Task DropAsync(int slot, int amount, Tile where, CancellationToken cancellationToken) =>
        Send(
            ClientOpcode.Drop,
            [
                (byte)slot,
                (byte)(where.X >> 8), (byte)where.X,
                (byte)(where.Y >> 8), (byte)where.Y,
                (byte)(amount >> 24), (byte)(amount >> 16), (byte)(amount >> 8), (byte)amount
            ],
            cancellationToken);

    /// <summary>
    /// Throws gold on the floor. Gold is not a pack slot — the server takes the amount straight off the
    /// character — so this says only how much and where. The original merges what lands where gold already
    /// lies, so two throws on one tile leave one larger pile rather than two.
    /// </summary>
    public Task DropGoldAsync(int amount, Tile where, CancellationToken cancellationToken) =>
        Send(
            ClientOpcode.DropGold,
            [
                (byte)(amount >> 24), (byte)(amount >> 16), (byte)(amount >> 8), (byte)amount,
                (byte)(where.X >> 8), (byte)where.X,
                (byte)(where.Y >> 8), (byte)where.Y
            ],
            cancellationToken);

    /// <summary>
    /// Takes off whatever is in one worn place. The place is the server's own number — the one it gave us
    /// in <c>0x37</c> when it said the place was filled — and the item goes back into the pack, so nothing
    /// here has to say where. The server answers by describing us again, which redraws the figure.
    /// </summary>
    public Task TakeOffAsync(int place, CancellationToken cancellationToken) =>
        Send(ClientOpcode.TakeOff, [(byte)place], cancellationToken);

    /// <summary>
    /// Taps someone. This is how a conversation starts: the server finds whatever carries that serial and
    /// hands it the tap, and an NPC with a script answers with a dialogue window. Tapping a monster or a
    /// player does something else or nothing, which is the server's business, not ours — we only say what
    /// was tapped. The first byte picks how we name it; one means by serial.
    /// </summary>
    public Task ClickAsync(uint serial, CancellationToken cancellationToken) =>
        Send(
            ClientOpcode.Click,
            [
                ClickBySerial,
                (byte)(serial >> 24), (byte)(serial >> 16), (byte)(serial >> 8), (byte)serial
            ],
            cancellationToken);

    /// <summary>
    /// Picks up whatever lies on one tile. The original has no automatic looting — walking over a thing
    /// leaves it there, and only asking for it takes it — so this is sent when the tile is tapped and at
    /// no other time. The server takes the topmost thing within <c>ClickLootDistance</c> (10 tiles) of us
    /// and says nothing at all when there is none, so a tap on bare floor costs nothing.
    /// </summary>
    public Task PickUpAsync(Tile where, CancellationToken cancellationToken) =>
        Send(
            ClientOpcode.PickUp,
            [
                AnyPackSlot,
                (byte)(where.X >> 8), (byte)where.X,
                (byte)(where.Y >> 8), (byte)where.Y
            ],
            cancellationToken);

    /// <summary>
    /// Swaps two pack slots. Tidying the pack is a run of these — the server keeps no order of its own, so
    /// whatever order there is, the player made it.
    /// </summary>
    public Task MoveAsync(int from, int to, CancellationToken cancellationToken) =>
        Send(ClientOpcode.Move, [InventoryPane, (byte)from, (byte)to], cancellationToken);

    /// <summary>
    /// Spends one of the points a level handed out, on one attribute.
    /// </summary>
    /// <remarks>
    /// The server refuses with a message and changes nothing when there are no points left
    /// (<c>Format47Handler</c>), so asking too often costs nothing but the packet. It answers a successful
    /// spend with the whole of our numbers, which is how the new maximum health arrives.
    /// </remarks>
    public Task RaiseAsync(Stat which, CancellationToken cancellationToken) =>
        Send(ClientOpcode.Raise, [(byte)which], cancellationToken);

    /// <summary>Asks the server to say where we are again, which it answers with the map and the tile.</summary>
    public Task RefreshAsync(CancellationToken cancellationToken) =>
        Send(ClientOpcode.Refresh, [], cancellationToken);

    /// <summary>
    /// 월드맵에서 고른 곳의 맵 번호. 서버는 이 번호로 자기 목록에서 곳을 찾는다
    /// (`GameServerHandlers.cs:1853`) — 그림 위의 점이 아니라 갈 맵이 열쇠다.
    /// </summary>
    public static byte[] FieldChoice(int areaId)
    {
        byte[] body = new byte[4];

        BinaryPrimitives.WriteInt32BigEndian(body, areaId);

        return body;
    }

    /// <summary>
    /// 월드맵에서 한 곳을 골라 보낸다. 창이 열려 있는 동안 서버는 이것 말고 이 접속의 패킷을 모두
    /// 버리므로(`NetworkServer.cs:141`), 고르기 전에는 걷지도 말하지도 못한다 — 닫기로 취소(맵 번호 0)를
    /// 보내면 조작이 돌아온다.
    /// </summary>
    public Task ChooseFieldAsync(int areaId, CancellationToken cancellationToken) =>
        Send(ClientOpcode.ChooseField, FieldChoice(areaId), cancellationToken);

    /// <summary>
    /// 월드맵을 열어 달라고 서버에 말한다. 원작에는 없는 말이다 — 원작은 바닥의 숨은 칸을 밟아야 열렸다.
    /// 마을이 아니면 서버가 거절하고 말 한 줄만 돌려준다(싸우는 중에 열면 손이 묶이기 때문이다).
    /// </summary>
    public Task OpenFieldAsync(CancellationToken cancellationToken) =>
        Send(ClientOpcode.OpenField, [], cancellationToken);

    /// <summary>
    /// 월드맵을 그냥 닫는다. 갈 맵 번호 0 이 취소라고 서버와 약속했다 — 0 은 어느 맵의 번호도 아니다.
    /// </summary>
    public Task CloseFieldAsync(CancellationToken cancellationToken) =>
        Send(ClientOpcode.ChooseField, FieldChoice(0), cancellationToken);

    /// <summary>
    /// Sends one of the two answers an NPC takes. They go in a different envelope — six bytes of header and
    /// a second layer of enciphering — which <see cref="HadesCipher.EncodeDialogSecured" /> explains.
    /// </summary>
    private Task SendDialog(byte command, byte[] fields, CancellationToken cancellationToken) =>
        SendInTurn(() => HadesCipher.EncodeDialogSecured(command, _ordinal++, fields, session.Parameters), cancellationToken);

    private Task Send(byte command, byte[] body, CancellationToken cancellationToken) =>
        SendInTurn(() => HadesCipher.EncodeSecured(command, _ordinal++, body, session.Parameters), cancellationToken);

    private async Task SendInTurn(Func<byte[]> frame, CancellationToken cancellationToken)
    {
        await _sending.WaitAsync(cancellationToken);

        try
        {
            await session.Connection.SendAsync(frame(), cancellationToken);
        }
        finally
        {
            _sending.Release();
        }
    }

    /// <summary>
    /// 원작 클라이언트처럼 나간다고 먼저 말하고(0x0B, 종류 1) 서버가 캐릭터를 뺐다는 답(0x4C)을 잠깐 기다린 뒤 소켓을 닫는다.
    /// 말이 닿지 않아도(끊긴 망) 닫는 것은 반드시 한다 — 서버는 닫힌 소켓으로도 곧 뺀다.
    /// </summary>
    public async Task LogOutAsync(CancellationToken cancellationToken)
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            using CancellationTokenSource wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            wait.CancelAfter(LogOutWait);
            await Send(ClientOpcode.Exit, [1], wait.Token);
            await _exited.Task.WaitAsync(wait.Token);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // 답이 늦거나 망이 끊겼다 — 닫는 것으로 충분하다.
        }
        finally
        {
            Dispose();
        }
    }
}
