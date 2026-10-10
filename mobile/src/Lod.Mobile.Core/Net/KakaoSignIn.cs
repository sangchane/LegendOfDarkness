using System.Text.Json;
using System.Text.RegularExpressions;
using Lod.Mobile.Core.Ui;

namespace Lod.Mobile.Core.Net;

/// <summary>app-token 을 물은 답이 무엇이었나.</summary>
public enum KakaoWait
{
    /// <summary>아직 브라우저에서 로그인을 안 마쳤다 — 계속 묻는다(못 닿은 것·서버 오류도 여기).</summary>
    Waiting,

    /// <summary>관리자 승인 대기 — 초대 번호를 넣거나 기다린다. 계속 묻는다.</summary>
    Pending,

    /// <summary>게임 표를 받았다(한 번만 온다).</summary>
    Granted,

    /// <summary>거부된 카카오 계정 — 그만 묻는다.</summary>
    Refused,

    /// <summary>모르는 표·지난 표 — 처음부터 다시.</summary>
    StartOver
}

/// <summary>app-token 답 하나. 이름은 카카오 닉네임, 표는 <see cref="KakaoWait.Granted" /> 일 때만.</summary>
public sealed record KakaoReply(KakaoWait State, string Name = "", string Token = "", string Error = "");

/// <summary>내 캐릭터 목록(<c>/api/kakao/characters</c>)의 한 줄. 직업·성별은 서버 이름 그대로(<c>Monk</c>·<c>Female</c>).</summary>
public sealed record KakaoCharacter(string Name, int Level, string Path, string Gender, int HairStyle, int HairColor)
{
    // 서버 Class 차례(평민 0 ~ 무도가 5) — 0x04 의 직업 번호와 같다.
    private static readonly string[] Paths = ["Peasant", "Warrior", "Rogue", "Wizard", "Priest", "Monk"];
    private static readonly string[] Jobs = ["평민", "전사", "도적", "마법사", "성직자", "무도가"];

    /// <summary>화면에 적을 직업, 모르는 이름이면 그대로.</summary>
    public string Job => Array.IndexOf(Paths, Path) is >= 0 and int at ? Jobs[at] : Path;

    /// <summary>0x04 의 직업 번호(모르면 평민) — 생김새 바꾸기는 서버가 무시하니 미리보기 옷에만 쓰인다.</summary>
    public byte PathNumber => (byte)Math.Max(0, Array.IndexOf(Paths, Path));

    /// <summary>0x04 의 성별 번호 1=남·2=여.</summary>
    public byte GenderNumber => Gender == "Female" ? (byte)2 : (byte)1;
}

/// <summary>
/// 기기에 남기는 카카오 로그인(<c>user://kakao.cfg</c> 두 줄: 게임 표 · 닉네임). 파일 읽고 쓰기는 화면 쪽이 하고,
/// 여기는 두 줄과 계정 사이만 바꾼다. 예전 자동 로그인(<c>autologin.cfg</c>, 이름·비밀번호 평문)을 대신한다 — 비밀번호를 기기에 두지 않는다.
/// </summary>
public sealed record KakaoAccount(string Token, string Name)
{
    /// <summary>저장된 두 줄 → 계정. 표 꼴이 틀리면 없음.</summary>
    public static KakaoAccount? Parse(string token, string name) =>
        KakaoSignIn.IsGameToken(token.Trim()) ? new KakaoAccount(token.Trim(), KakaoSignIn.OneLine(name)) : null;

    /// <summary>적을 두 줄, <see cref="Parse" /> 가 읽는 차례대로.</summary>
    public string[] ToLines() => [Token, KakaoSignIn.OneLine(Name)];
}

/// <summary>
/// 앱 카카오 로그인(사용자 2026-10-11, <c>autopilot/game-kakao-login/SPEC.md</c>) — 딥링크 없이 「브라우저에서 로그인,
/// 앱은 기다렸다 받기」. 홈페이지에서 표(ticket)를 받아 브라우저로 start 를 열고, 2초마다 app-token 을 물어 게임 표를 받는다.
/// 6자리 번호는 없앴다(사용자 2026-10-11, <c>autopilot/game-kakao-accounts/SPEC.md</c>). 받은 게임 표가 캐릭터 비밀번호 칸으로 가고,
/// 내 캐릭터 목록을 묻고, 지우기·생김새 바꾸기는 비밀번호 칸 동사(<c>표|delete</c>·<c>표|look</c>)로 한다.
/// 여기는 주소·답 읽기·동사 칸만(순수 함수) — 묻는 것은 화면이 한다.
/// </summary>
public static class KakaoSignIn
{
    /// <summary>홈페이지 — 내려받기 페이지(<see cref="AppUpdate.DownloadPage" />)와 같은 사이트.</summary>
    public static readonly string Site = new Uri(AppUpdate.DownloadPage).GetLeftPart(UriPartial.Authority);

    public static string TicketUrl => Site + "/api/kakao/app-ticket";

    public static string TokenUrl => Site + "/api/kakao/app-token";

    public static string CharactersUrl => Site + "/api/kakao/characters";

    /// <summary>몇 초마다 묻나.</summary>
    public const double PollSeconds = 2;

    /// <summary>이만큼 기다려도 안 끝나면 처음으로 — 서비스의 표 수명(10분)과 같다.</summary>
    public const double WaitSeconds = 600;

    // g<카카오 회원번호>.<만료>.<서명 hex 64> — 서비스 kakao_session(kind="game") 꼴. \d 는 다른 문자의 숫자까지 맞으니 [0-9].
    private static readonly Regex GameToken = new("^g[0-9]{1,19}\\.[0-9]{1,11}\\.[0-9a-f]{64}$", RegexOptions.CultureInvariant);

    public static bool IsGameToken(string? token) => token is not null && GameToken.IsMatch(token);

    /// <summary>app-ticket 의 답 → (표, 브라우저로 열 주소). 200 이 아니거나 꼴이 틀리면 없음.</summary>
    public static (string Ticket, string Browser)? ReadTicket(long status, string body)
    {
        JsonElement? json = Parse(body);
        string ticket = Text(json, "ticket"), start = Text(json, "start");

        // 우리 사이트의 시작 주소만 연다 — 답이 무엇이든 바깥 주소로 브라우저를 보내지 않는다.
        return status == 200 && ticket.Length > 0 && start.StartsWith("/api/kakao/start?", StringComparison.Ordinal)
            ? (ticket, Site + start)
            : null;
    }

    /// <summary>app-token 에 실어 보낼 본문.</summary>
    public static string TokenRequest(string ticket) => $"{{\"ticket\":\"{JsonEncodedText.Encode(ticket)}\"}}";

    /// <summary>캐릭터 목록에 실어 보낼 본문.</summary>
    public static string CharactersRequest(string token) => $"{{\"token\":\"{JsonEncodedText.Encode(token)}\"}}";

    /// <summary>
    /// 캐릭터 목록의 답 → 줄들(서버 차례 그대로). 200 이 아니거나 꼴이 틀리면 없음 — 401·403 이면 화면이 표를 지운다.
    /// 이름 없는 줄은 건너뛴다.
    /// </summary>
    public static IReadOnlyList<KakaoCharacter>? ReadCharacters(long status, string body)
    {
        if (status != 200 || Parse(body) is not { } json
            || !json.TryGetProperty("characters", out JsonElement list) || list.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return
        [
            .. list.EnumerateArray()
                .Where(entry => entry.ValueKind == JsonValueKind.Object && OneLine(Text(entry, "name")).Length > 0)
                .Select(entry => new KakaoCharacter(
                    OneLine(Text(entry, "name")), Number(entry, "level"), Text(entry, "path"), Text(entry, "gender"),
                    Number(entry, "hairStyle"), Number(entry, "hairColor")))
        ];
    }

    /// <summary>app-token 의 상태 코드와 본문 → 기다림 / 대기(닉네임) / 받음(표·닉네임) / 거부 / 처음부터.</summary>
    public static KakaoReply ReadToken(long status, string body)
    {
        JsonElement? json = Parse(body);
        string name = OneLine(Text(json, "name")), token = Text(json, "token"), error = OneLine(Text(json, "error"));

        return status switch
        {
            202 when Flag(json, "pending") => new KakaoReply(KakaoWait.Pending, name),
            202 => new KakaoReply(KakaoWait.Waiting),
            200 => IsGameToken(token) ? new KakaoReply(KakaoWait.Granted, name, token) : new KakaoReply(KakaoWait.StartOver),
            403 => new KakaoReply(KakaoWait.Refused, Error: error.Length > 0 ? error : "들어올 수 없는 카카오 계정입니다."),
            >= 400 and < 500 => new KakaoReply(KakaoWait.StartOver, Error: error),
            // 0(못 닿음)·5xx — 잠깐 끊긴 것. 시간 안이면 계속 묻는다.
            _ => new KakaoReply(KakaoWait.Waiting)
        };
    }

    /// <summary>답의 "error" 글, 없으면 <paramref name="fallback" />.</summary>
    public static string ErrorOf(string body, string fallback) =>
        OneLine(Text(Parse(body), "error")) is { Length: > 0 } error ? error : fallback;

    /// <summary>생김새 바꾸기 — 0x02 비밀번호 칸에 <c>표|look</c>, 이어지는 0x04 가 머리 모양·색만 바꾼다(서버 KakaoLogin 이 처음 「|」에서 가른다).</summary>
    public static string Look(string token) => token + "|look";

    /// <summary>캐릭터 지우기 — 0x02 비밀번호 칸에 <c>표|delete</c>.</summary>
    public static string Delete(string token) => token + "|delete";

    /// <summary>
    /// 서버가 표를 더 받지 않는다(만료·서명 틀림) — 앱은 표를 지우고 카카오 단추로 돌아간다. 서버 문구는 CP949 로 오니
    /// 한글 부분만 맞춘다(「—」 같은 글자는 「?」로 온다).
    /// </summary>
    public static bool IsSignedOut(string refusal) => refusal.Contains("카카오 로그인이 끝났습니다", StringComparison.Ordinal);

    /// <summary>한 줄로 — 닉네임이 저장 줄을 깨지 않게.</summary>
    internal static string OneLine(string text) => text.ReplaceLineEndings(" ").Trim();

    private static JsonElement? Parse(string body)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            return document.RootElement.ValueKind == JsonValueKind.Object ? document.RootElement.Clone() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool Flag(JsonElement? json, string key) =>
        json is { } found && found.TryGetProperty(key, out JsonElement value) && value.ValueKind == JsonValueKind.True;

    private static int Number(JsonElement json, string key) =>
        json.TryGetProperty(key, out JsonElement value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number) ? number : 0;

    private static string Text(JsonElement? json, string key) =>
        json is { } found && found.TryGetProperty(key, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
}
