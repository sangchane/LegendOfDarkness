using System.Text.RegularExpressions;

namespace Lod.Mobile.Core.World;

/// <summary>Which tab of the full log a line belongs under.</summary>
public enum MessageChannel
{
    /// <summary>People talking: speech nearby (0x0D), whispers, guild lines, a world shout.</summary>
    General,

    /// <summary>The party's own lines — group chat (0x0A type 11) and the server's party notices.</summary>
    Party,

    /// <summary>What the server says of its own accord.</summary>
    System
}

/// <summary>Where on the screen a line goes, besides the log. Every line goes to the log.</summary>
public enum MessagePlace
{
    /// <summary>Kept for reading back, never shown over the world — cast confirmations, "already", refusals.</summary>
    LogOnly,

    /// <summary>The two fading lines above the controls.</summary>
    Ticker,

    /// <summary>The small feed on one side that stacks and fades — what was gained or lost.</summary>
    Toast,

    /// <summary>A short line in the middle of the screen — a level, a death, a place.</summary>
    Banner,

    /// <summary>Over the speaker's head.</summary>
    Bubble
}

/// <summary>
/// One line, sorted. <paramref name="Text" /> is what the log keeps; <paramref name="Short" /> is what the toast or the
/// banner shows, which is often shorter ("쿠룸 +1" rather than "쿠룸을(를) 얻었습니다.").
/// </summary>
public sealed record Notice(MessageChannel Channel, MessagePlace Place, string Text, string Short);

/// <summary>
/// Sorts what the server says into the place it belongs, rather than one box over the world.
/// </summary>
/// <remarks>
/// <para>
/// <b>The type byte first.</b> 0x0A starts with a type (Hades <c>ServerFormat0A.MsgType</c>; the full list is in
/// <c>sources/FallenDev/Arbiter/Arbiter.Net/Types/WorldMessageType.cs</c>): 0 whisper · 1–4, 6 the bar at the bottom
/// · 5 world shout · 7 user settings · 8, 9 pop-ups · 10 sign post · 11 group chat · 12 guild chat · 18 floating.
/// 0x0D starts with how it was said (<c>ServerFormat0D.MsgType</c>): 0 normal · 1 shout · 2 chant.
/// </para>
/// <para>
/// <b>The words only where the type cannot tell.</b> Hades sends almost everything as type 2 — the bar — whether it is
/// "dion을(를) 외웠습니다.", a level, a death or an item (<c>SendMessage(0x02, …)</c> ≈ 88 calls in <c>Hades.Server.Base</c>
/// and ≈ 200 in <c>database/server/scripts</c>). Only gold (<c>Money.cs:69</c>) and the equip line
/// (<c>Item.cs:269</c>) come as 3. So type 2 and 3 fall through to <see cref="Rules" />, the one list of patterns,
/// each taken from the server line that sends it.
/// </para>
/// </remarks>
public static class MessageSort
{
    private const byte Whisper = 0;
    private const byte ClearBar = 1;
    private const byte WorldShout = 5;
    private const byte UserSettings = 7;
    private const byte GroupChat = 11;
    private const byte GuildChat = 12;

    /// <summary>A rule: a pattern over the cleaned line, where it goes, and — for a toast or banner — what it says.</summary>
    private sealed record Rule(Regex Pattern, MessageChannel Channel, MessagePlace Place, Func<Match, string>? Short = null);

    private static Regex R(string pattern) =>
        new(pattern, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// The patterns, first match wins. Each names the server line it was taken from. A line none of them matches goes to
    /// the ticker — something the server said that nobody sorted yet is better seen than lost.
    /// </summary>
    private static readonly Rule[] Rules =
    [
        // 서버 말은 2026-09-24 부터 한국어다(docs/server-messages-ko.md — 5.99 서버 Novaonline.exe 의 말이 있으면 그것).
        // 영어 꼴은 그 전 빌드가 아직 도는 서버를 위해 같은 규칙 안에 남겨 둔다.

        // 얻은 것 — 옆에 쌓이는 알림. Item.cs:522 (쌓이는 것) · Item.cs:547 (새 칸) · Money.cs:69 (0x03) · Quest.cs:249 ·
        // CursedSachel.cs:74 · monsterexp.cs:283,287 · YouDroppedGoldMsg (GameServerHandlers.cs:1050, 돈을 버림).
        new(R(@"^(?<item>.+?)을\(를\) 얻었습니다\. \((?<count>\d+)개\)$|^Received (?<item>.+?), You now have \((?<count>\d+)\)\.?$"),
            MessageChannel.System, MessagePlace.Toast, m => $"{m.Groups["item"].Value} ({m.Groups["count"].Value}개)"),
        new(R(@"^(?<item>.+?)을\(를\) 얻었습니다\.$|^(?<item>.+?) Received\.$"), MessageChannel.System, MessagePlace.Toast,
            m => $"{m.Groups["item"].Value} +1"),
        new(R(@"^금전 (?<n>\d+)전을 (?:주웠|받았)습니다\.?$|^You've Received (?<n>\d+) coins\.?$|^You are awarded (?<n>\d+) gold\.?$"),
            MessageChannel.System, MessagePlace.Toast, m => $"금화 +{m.Groups["n"].Value}"),
        new(R(@"^(?<item>.+?)을\(를\) 되찾았습니다\.$|^You have recovered (?<item>.+?)\.$"), MessageChannel.System, MessagePlace.Toast,
            m => $"{m.Groups["item"].Value} 되찾음"),
        new(R(@"^경험치가 (?<n>\d+) 올랐습니다\.?$|^You received (?<n>\d+) Experience!?\.?$"), MessageChannel.System, MessagePlace.Toast,
            m => $"경험치 +{m.Groups["n"].Value}"),
        new(R(@"^돈을 버렸습니다\.?$|^you've dropped some gold\.?$"), MessageChannel.System, MessagePlace.Toast, _ => "금화를 버렸다"),

        // 큰일 — 가운데 한 줄. LevelUpMessage "레벨이 올랐습니다!" (Monster.cs:204 · monsterexp.cs:94) ·
        // debuff_reeping.cs:126 "죽었습니다." (죽음) · ReapMessage 첫 줄 "죽어 가고 있습니다." (혼수) · 퀘스트 완료(Hades 에는
        // 아직 그런 말이 없다 — 스크립트가 쓰면 걸리도록 둔다).
        new(R(@"^레벨이 올랐습니다!?$|^Your insight has increased!?$"), MessageChannel.System, MessagePlace.Banner, _ => "레벨이 올랐습니다"),
        new(R(@"^죽었습니다\.?$|^You have died\.?$"), MessageChannel.System, MessagePlace.Banner, _ => "죽었습니다"),
        new(R(@"^죽어 가고 있습니다\.?$|^You are dying\.?$"), MessageChannel.System, MessagePlace.Banner, _ => "혼수 상태"),
        new(R(@"quest (?:is )?complete|퀘스트.*(?:완료|성공)"), MessageChannel.System, MessagePlace.Banner, _ => "퀘스트 완료"),

        // 파티 — Party.cs ("…님 그룹에 참여" · "그룹 해체" · "…님 그룹 해체" · "…님이 그룹장이 되셨습니다") 와
        // GroupRequestDeclinedMsg ("…님은 그룹 거부 상태입니다"). 5.99 서버의 말 그대로다.
        new(R(@"\bparty\b|\bgroup\b|파티|그룹"), MessageChannel.Party, MessagePlace.Ticker),

        // 시끄러운 것 — 기록에만. 외웠다는 확인(Aisling.cs:394 "…을(를) 외웠습니다.", 스크립트 30여 곳), 누가 내게 걸어
        // 주었다는 말("nov님이 armachd을(를) 외워주셨습니다." — 걸린 것은 머리 위 배지가 이미 보여 준다. 실제 서버에서 제 몸에
        // 건 마법마다 한 줄씩 왔다, 2026-09-23), "이미"(Pack599.cs · 스크립트 "이미 걸려있습니다."), 실패·튕김, 마력 모자람
        // (NoManaMessage · MonkStrike.cs:31), 장비 줄(Item.cs:269 "…: 갑옷 강도 5"), 들어올 때 인사(ServerWelcomeMessage),
        // 그리고 걸리고 풀리는 말 — 그것도 머리 위 배지가 보여 준다(buffs/*, debuffs/*).
        new(R(@"을\(를\) 외웠습니다\.?$|외워주셨습니다\.|^의지를 모읍니다|^you (?:cast|invoke)\b|^\S+ casts .+ on you\.?$"),
            MessageChannel.System, MessagePlace.LogOnly),
        new(R(@"^이미 |\balready\b"), MessageChannel.System, MessagePlace.LogOnly),
        new(R(@"^(?:실패했습니다|마법이 실패했습니다|정신을 모으지 못했습니다)|^(?:failed|something backfired|something went wrong|you failed)\b"),
            MessageChannel.System, MessagePlace.LogOnly),
        new(R(@"^걸리지 않습니다|^더 강한 해제 마법이 필요합니다|^Your spell has been deflected|^Another (?:poison|curse)|^A greater cure is required"),
            MessageChannel.System, MessagePlace.LogOnly),
        new(R(@"^마력이 부족합니다|마력량이\s*적습니다|^Your will is too weak"), MessageChannel.System, MessagePlace.LogOnly),
        new(R(@"^.+: 갑옷 강도 -?\d+$|^E: .+, AC: -?\d+$"), MessageChannel.System, MessagePlace.LogOnly),
        new(R(@"에 오신 것을 환영합니다|^Welcome to "), MessageChannel.System, MessagePlace.LogOnly),

        // 기술이 오른 말(GameClient.cs:1203,1225) — 휘두를 때마다 오르므로(TrainSkill) 사냥 중에는 한 대마다 한 줄이다.
        // 실제 서버에서 사냥 1분에 60줄 가까이 왔다(2026-09-23). 오른 값은 기술 창이 보여 준다.
        new(R(@"^.+의 숙련도가 올랐습니다\.(?: \(Lv\. \d+\))?$|^.+ has improved\.(?: \(Lv\. \d+\))?$"), MessageChannel.System, MessagePlace.LogOnly),
        new(R(@"^(?:피부가 (?:돌처럼 단단해집니다|원래대로 돌아옵니다)|방어력이 (?:올랐습니다|원래대로 돌아옵니다)|아이테|" +
              @"두 손(?:에 힘이 깃듭니다|이 원래대로 돌아옵니다)|원래대로 돌아왔습니다|잠이 쏟아져 옵니다|잠에서 깨어났습니다|.+ 끝\.$|" +
              @"허리케인이 지나갔습니다|그림자 (?:속으로 몸을 숨깁니다|밖으로 모습을 드러냅니다)|몸이 굳어 움직일 수 없습니다|" +
              @"다시 움직일 수 있습니다|몸이 얼어 움직일수 없습니다|눈이 멀었습니다|다시 앞이 보입니다|중독되었습니다|" +
              @"마법 반사가 끝났습니다|갑옷이 가벼워진 듯합니다)"),
            MessageChannel.System, MessagePlace.LogOnly),
        new(R(@"^(?:Your skin turns|Your armor (?:has been increased|returns to normal|feels light)|Aite|Your hands (?:are empowered|turn back)|" +
              @"You return to normal|You have been put to sleep|awake!|.+ has ended\.|The hurricane has passed|You (?:blend in to|emerge from) the shadows|" +
              @"You've been incapacitated|Your are free again|Your body (?:is frozen|thaws out)|You are blinded|You can see again|" +
              @"You are infected with poison|you feel better now|Spells attacking you now stop reflecting)"),
            MessageChannel.System, MessagePlace.LogOnly)
    ];

    /// <summary>Hades puts colour codes in a line as <c>{=</c> and a letter (GameServerHandlers.cs:871, GameClient.cs:1532).</summary>
    private static readonly Regex Colour = new(@"\{=[a-zA-Z]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>"이름: 말" (보통) · "이름! 말" (외침) — ServerFormat0D 가 앞에 붙이는 이름 (GameServerHandlers.cs:653-659).</summary>
    private static readonly Regex Speaker = new(@"^[^\s:!""]{1,24}[:!] (?<words>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// Throws away what cannot be read: control characters (the server sends lines that are a single zero byte, or a bare
    /// newline — 실제 서버에서 봤다) and colour codes.
    /// </summary>
    public static string Clean(string text) =>
        Colour.Replace(new string([.. text.Where(letter => !char.IsControl(letter))]), string.Empty).Trim();

    /// <summary>Sorts one line the server said (0x0A) by its type byte, then — where the type is the bar — by its words.</summary>
    /// <returns>Null when there is nothing to show — an empty line, or the "clear the bar" type 1 with no words.</returns>
    public static Notice? FromServer(byte type, string text)
    {
        // 길드 말은 0x02 로 오지만 "{=o이름> {=a말" 꼴이다(GameServerHandlers.cs:871). 색 부호를 지우기 전에 본다.
        bool guild = type == 2 && text.StartsWith("{=o", StringComparison.Ordinal) && text.Contains("> {=a", StringComparison.Ordinal);
        string line = Clean(text);

        if (line.Length == 0)
        {
            return null;
        }

        if (guild)
        {
            return new Notice(MessageChannel.General, MessagePlace.Ticker, line, line);
        }

        switch (type)
        {
            case Whisper:
            case GuildChat:
            case WorldShout:
                return new Notice(MessageChannel.General, MessagePlace.Ticker, line, line);

            case GroupChat:
                return new Notice(MessageChannel.Party, MessagePlace.Ticker, line, line);

            case UserSettings:
                return new Notice(MessageChannel.System, MessagePlace.LogOnly, line, line);

            case ClearBar:
                break;
        }

        foreach (Rule rule in Rules)
        {
            Match match = rule.Pattern.Match(line);

            if (match.Success)
            {
                return new Notice(rule.Channel, rule.Place, line, rule.Short?.Invoke(match) ?? line);
            }
        }

        return new Notice(MessageChannel.System, MessagePlace.Ticker, line, line);
    }

    /// <summary>
    /// Sorts what somebody near us said (0x0D). Normal speech and a shout go over the speaker's head and into the log; a
    /// chant is a spell being said aloud as it is cast, not somebody talking, and is left out.
    /// </summary>
    public static Notice? FromSpeech(SpeechKind kind, string text)
    {
        string line = Clean(text);

        return kind == SpeechKind.Chant || line.Length == 0
            ? null
            : new Notice(MessageChannel.General, MessagePlace.Bubble, line, Words(line));
    }

    /// <summary>The words without the name the server put in front of them ("nov: 안녕" → "안녕").</summary>
    public static string Words(string line)
    {
        Match match = Speaker.Match(line);

        return match.Success ? match.Groups["words"].Value : line;
    }

    /// <summary>A line this screen says itself — a refusal, a lost connection. It is shown, never only logged.</summary>
    public static Notice Own(string text)
    {
        string line = Clean(text);

        return new Notice(MessageChannel.System, MessagePlace.Ticker, line, line);
    }
}
