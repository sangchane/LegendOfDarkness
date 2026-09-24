using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>
/// Each line the server says goes to one place — over a head, a toast, a banner, the ticker, or the log alone — instead of
/// one box over the world. The lines below are the ones Hades actually sends (the source line is beside each).
/// </summary>
public sealed class MessageSortTests
{
    private static Notice Sorted(byte type, string text) =>
        MessageSort.FromServer(type, text) ?? throw new InvalidOperationException($"'{text}' 가 버려졌다");

    [Theory]
    [InlineData(2, "dion을(를) 외웠습니다.")] // Aisling.cs:394
    [InlineData(2, "beag ioc을(를) 외웠습니다.")] // scripts — "you cast" 30여 곳
    [InlineData(2, "nov님이 armachd을(를) 외워주셨습니다.")] // armachd.cs:40 — 실제 서버, 제 몸에 건 armachd
    [InlineData(2, "이미 걸려있습니다.")] // Pack599.cs:595 · scripts "already" 일곱 가지
    [InlineData(2, "이미 저주가 걸려있습니다. [beag cradh]")] // cradh.cs:84
    [InlineData(2, "실패했습니다.")] // scripts "failed."
    [InlineData(2, "걸리지 않습니다.")] // scripts "Your spell has been deflected."
    [InlineData(2, "마력이 부족합니다.")] // NoManaMessage
    [InlineData(2, "사용하기에 마력량이적습니다. [필요마나 : 30이상]")] // MonkStrike.cs:31
    [InlineData(2, "피부가 돌처럼 단단해집니다.")] // buff_dion.cs:20 — the badge over the head says it already
    [InlineData(2, "동면 끝.")] // debuff_frozen.cs:98
    [InlineData(2, "잠이 쏟아져 옵니다.")] // debuff_sleep.cs:62
    [InlineData(3, "나무막대: 갑옷 강도 5")] // Item.cs:269
    [InlineData(2, "어둠의 전설에 오신 것을 환영합니다!")] // ServerWelcomeMessage
    [InlineData(2, "양의신권의 숙련도가 올랐습니다. (Lv. 46)")] // GameClient.cs:1203 — every blow while hunting
    [InlineData(2, "beag ioc의 숙련도가 올랐습니다.")] // GameClient.cs:1225
    [InlineData(7, "옵션 1 : 켬")] // type 7 = user settings
    public void Noise_goes_to_the_log_only(byte type, string text)
    {
        Notice notice = Sorted(type, text);

        Assert.Equal(MessagePlace.LogOnly, notice.Place);
        Assert.Equal(MessageChannel.System, notice.Channel);
    }

    [Theory]
    [InlineData(2, "쿠룸을(를) 얻었습니다.", "쿠룸 +1")] // Item.cs:547
    [InlineData(2, "쿠룸을(를) 얻었습니다. (5개)", "쿠룸 (5개)")] // Item.cs:522
    [InlineData(3, "금전 120전을 주웠습니다.", "금화 +120")] // Money.cs:69 — type 3
    [InlineData(2, "금전 500전을 받았습니다.", "금화 +500")] // Quest.cs:249
    [InlineData(2, "경험치가 37 올랐습니다", "경험치 +37")] // monsterexp.cs:287 — 5.99 "경험치가 %lu 올랐습니다"
    [InlineData(2, "나무막대을(를) 되찾았습니다.", "나무막대 되찾음")] // CursedSachel.cs:74
    [InlineData(2, "돈을 버렸습니다.", "금화를 버렸다")] // YouDroppedGoldMsg
    public void What_was_gained_or_lost_is_a_toast(byte type, string text, string shown)
    {
        Notice notice = Sorted(type, text);

        Assert.Equal(MessagePlace.Toast, notice.Place);
        Assert.Equal(shown, notice.Short);
        Assert.Equal(text, notice.Text);
    }

    [Theory]
    [InlineData("레벨이 올랐습니다!", "레벨이 올랐습니다")] // LevelUpMessage, Monster.cs:204
    [InlineData("죽었습니다.", "죽었습니다")] // debuff_reeping.cs:126
    [InlineData("죽어 가고 있습니다.", "혼수 상태")] // ReapMessage 첫 줄
    [InlineData("Quest complete!", "퀘스트 완료")]
    public void What_matters_is_a_banner(string text, string shown)
    {
        Notice notice = Sorted(2, text);

        Assert.Equal(MessagePlace.Banner, notice.Place);
        Assert.Equal(shown, notice.Short);
    }

    [Theory]
    [InlineData("공격할 수 없습니다.")] // CantAttack
    [InlineData("죽음의 그림자가 드리웁니다.")] // ReapMessageDuringAction — 혼수 중에 무엇을 하려 할 때마다; 배너가 아니다
    [InlineData("길이 막혀 가까운 곳으로 옮겼습니다.")] // GameClient.cs:686
    [InlineData("nov님이 wren님에게 죽었습니다.")] // GameClient.cs:1160
    [InlineData("wren님이 beag srad(으)로 공격합니다.")] // scripts "Attacks you with"
    public void Anything_else_is_the_ticker(string text)
    {
        Notice notice = Sorted(2, text);

        Assert.Equal(MessagePlace.Ticker, notice.Place);
        Assert.Equal(MessageChannel.System, notice.Channel);
    }

    [Fact]
    public void The_type_byte_decides_before_the_words()
    {
        // 귓속말은 "already" 가 들어 있어도 사람의 말이다 — 타입이 먼저다.
        Notice whisper = Sorted(0, "wren\" I already left");
        Assert.Equal(MessageChannel.General, whisper.Channel);
        Assert.Equal(MessagePlace.Ticker, whisper.Place);

        Assert.Equal(MessageChannel.Party, Sorted(11, "wren: 이쪽으로").Channel);
        Assert.Equal(MessageChannel.General, Sorted(12, "wren: 길드").Channel);
        Assert.Equal(MessageChannel.General, Sorted(5, "서버 점검 10분 전").Channel);
    }

    [Fact]
    public void Party_notices_go_under_the_party_tab() // Party.cs — 5.99 서버의 말 그대로
    {
        Assert.Equal(MessageChannel.Party, Sorted(2, "wren님 그룹에 참여").Channel);
        Assert.Equal(MessageChannel.Party, Sorted(2, "wren님 그룹 해체").Channel);
        Assert.Equal(MessageChannel.Party, Sorted(2, "그룹 해체").Channel);
        Assert.Equal(MessageChannel.Party, Sorted(2, "wren님이 그룹장이 되셨습니다").Channel);
        Assert.Equal(MessageChannel.Party, Sorted(2, "wren님은 그룹 거부 상태입니다").Channel);

        // 청하고 받는 길에 새로 적은 말(GameServerHandlers AskToGroup·AcceptGroup·그룹말) — "이미" 로 시작해도 기록에만
        // 묻히지 않고 파티 탭과 기록 줄에 뜬다.
        Assert.Equal(MessageChannel.Party, Sorted(2, "이미 그룹 중 입니다.").Channel);
        Assert.Equal(MessagePlace.Ticker, Sorted(2, "이미 그룹 중 입니다.").Place);
        Assert.Equal(MessageChannel.Party, Sorted(2, "그룹장만 할 수 있습니다.").Channel);
        Assert.Equal(MessageChannel.Party, Sorted(2, "그룹이 없습니다.").Channel);
        Assert.Equal(MessageChannel.Party, Sorted(11, "[그룹말]wren: 이쪽으로").Channel);
    }

    [Fact]
    public void A_guild_line_sent_as_the_bar_is_still_a_person_talking() // GameServerHandlers.cs:871
    {
        Notice notice = Sorted(2, "{=onov> {=a모두 안녕");

        Assert.Equal(MessageChannel.General, notice.Channel);
        Assert.Equal("nov> 모두 안녕", notice.Text);
    }

    /// <summary>한국어로 바꾸기 전 빌드가 아직 도는 서버의 말도 같은 곳으로 간다.</summary>
    [Theory]
    [InlineData(2, "you cast dion.", MessagePlace.LogOnly)]
    [InlineData(2, "nov casts armachd on you.", MessagePlace.LogOnly)]
    [InlineData(2, "Your skin turns to stone.", MessagePlace.LogOnly)]
    [InlineData(2, "You received 37 Experience!.", MessagePlace.Toast)]
    [InlineData(3, "You've Received 120 coins.", MessagePlace.Toast)]
    [InlineData(2, "Your insight has increased!", MessagePlace.Banner)]
    [InlineData(2, "You are dying.", MessagePlace.Banner)]
    public void Lines_of_the_older_english_server_still_sort(byte type, string text, MessagePlace place) =>
        Assert.Equal(place, Sorted(type, text).Place);

    [Fact]
    public void Colour_codes_and_control_characters_are_cleaned_away() // GameClient.cs:1532
    {
        Notice notice = Sorted(3, "{=q안내 : 이동할수 없습니다. [남아있는 몬스터수 : 3]\n");

        Assert.Equal("안내 : 이동할수 없습니다. [남아있는 몬스터수 : 3]", notice.Text);
    }

    [Theory]
    [InlineData(1, "\0")] // MessageComponent.cs:27 — clears the bar
    [InlineData(2, "\n")]
    [InlineData(2, "")]
    public void An_empty_line_is_nothing(byte type, string text) => Assert.Null(MessageSort.FromServer(type, text));

    [Fact]
    public void Speech_goes_over_the_head_without_the_name_in_front()
    {
        Notice said = MessageSort.FromSpeech(SpeechKind.Normal, "nov: 안녕하세요") ?? throw new InvalidOperationException();
        Notice shout = MessageSort.FromSpeech(SpeechKind.Shout, "nov! 도와줘") ?? throw new InvalidOperationException();

        Assert.Equal(MessagePlace.Bubble, said.Place);
        Assert.Equal(MessageChannel.General, said.Channel);
        Assert.Equal("nov: 안녕하세요", said.Text);
        Assert.Equal("안녕하세요", said.Short);
        Assert.Equal("도와줘", shout.Short);
    }

    [Fact]
    public void A_chant_is_not_somebody_talking() => Assert.Null(MessageSort.FromSpeech(SpeechKind.Chant, "beag ioc"));

    [Fact]
    public void A_line_the_server_sends_as_0x0A_keeps_its_type()
    {
        (byte Type, string Text)? told = WorldClient.ReadTold([0x03, 0x00, 0x02, (byte)'h', (byte)'i']);

        Assert.Equal((byte)3, told?.Type);
        Assert.Equal("hi", told?.Text);
        Assert.Null(WorldClient.ReadTold([0x01]));
    }
}
