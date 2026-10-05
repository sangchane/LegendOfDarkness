using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Lod.Mobile.Core.Diagnostics;

/// <summary>입력 내용 없이 운영 신호만 남긴다. 인증 전 큐는 다음 성공 접속 때 전달한다.</summary>
public sealed class AppActivity
{
    private readonly string _path;
    private readonly object _gate = new();
    private readonly List<string> _queue = [];
    private readonly Dictionary<string, string> _device;
    private bool _authenticated;
    private bool _closed;
    private bool _focused = true;
    private string _fatal = "";
    private string _screen = "startup";
    private DateTime _screenAt = DateTime.UtcNow;
    public string Install { get; private set; } = Guid.NewGuid().ToString("N");
    public string Run { get; } = Guid.NewGuid().ToString("N");
    public int Pending { get { lock (_gate) return _queue.Count; } }
    private static readonly HashSet<string> Keys = ["platform", "model", "os", "version", "screen", "action", "error", "previousScreen"];

    public AppActivity(string path, string platform, string model, string os, string version)
    {
        _path = path;
        _device = new() { ["platform"] = platform, ["model"] = model, ["os"] = os, ["version"] = version };
        bool unclean = false;
        string previous = "startup";
        string fatal = "";
        try
        {
            if (File.Exists(path) && new FileInfo(path).Length <= 262144)
            {
                using var saved = JsonDocument.Parse(File.ReadAllText(path));
                var root = saved.RootElement;
                if (Guid.TryParse(root.GetProperty("install").GetString(), out var install)) Install = install.ToString("N");
                if (File.GetLastWriteTimeUtc(path) >= DateTime.UtcNow.AddDays(-90))
                {
                    unclean = root.GetProperty("dirty").GetBoolean();
                    previous = Code(root.GetProperty("screen").GetString() ?? "startup");
                    if (root.TryGetProperty("fatal", out var savedFatal) && savedFatal.ValueKind == JsonValueKind.String) fatal = Code(savedFatal.GetString() ?? "");
                }
                foreach (var entry in root.GetProperty("queue").EnumerateArray().TakeLast(120))
                {
                    // 기록 파일도 신뢰 경계: 이 앱이 만든 한정 필드/종류만 다시 받는다.
                    string kind = entry.GetProperty("kind").GetString() ?? "";
                    if (!Kinds.Contains(kind)) continue;
                    var meta = entry.GetProperty("meta");
                    // 직전 인증 계정의 행동은 다음 실행의 다른 계정에게 귀속하지 않는다.
                    if (!meta.TryGetProperty("beforeLogin", out var before) || before.ValueKind != JsonValueKind.True || !OccurredAt(meta, out string occurredAt)) continue;
                    var fields = new Dictionary<string, string>();
                    foreach (var field in meta.EnumerateObject())
                        if (Keys.Contains(field.Name) && field.Value.ValueKind == JsonValueKind.String)
                            fields[field.Name] = Clean(field.Value.GetString() ?? "");
                    int count = Math.Clamp(entry.GetProperty("count").GetInt32(), 1, 1000);
                    double seconds = meta.TryGetProperty("seconds", out var duration) ? Math.Clamp(duration.GetDouble(), 0, 86400) : 0;
                    string oldRun = meta.TryGetProperty("run", out var run) && Guid.TryParse(run.GetString(), out var parsed) ? parsed.ToString("N") : Run;
                    _queue.Add(Encode(kind, fields, count, seconds, true, oldRun, occurredAt));
                }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or FormatException or KeyNotFoundException) { }
        if (fatal.Length > 0) Record("app_error", new() { ["action"] = "previous_run_exception", ["error"] = fatal, ["previousScreen"] = previous });
        if (unclean) Record("app_error", new() { ["error"] = "unclean_exit", ["previousScreen"] = previous });
        Record("app_device", _device);
        Record("app_lifecycle", new() { ["action"] = "start", ["screen"] = _screen });
    }

    private static readonly HashSet<string> Kinds = ["app_device", "app_screen", "app_button", "app_error", "app_lifecycle"];
    private static string Clean(string value) => new(value.Where(c => !char.IsControl(c)).Take(64).ToArray());
    public static string Code(string value) => new(value.Where(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '.' or '-').Take(128).ToArray());

    public void Authenticate(bool authenticated)
    {
        lock (_gate)
        {
            if (_authenticated && !authenticated) _queue.Clear();
            _authenticated = authenticated;
            Save();
            if (authenticated)
            {
                Record("app_device", _device);
                string device = _queue[^1];
                _queue.RemoveAt(_queue.Count - 1);
                _queue.Insert(0, device);
                Save();
            }
        }
    }

    public void Screen(string screen)
    {
        lock (_gate)
        {
            screen = Code(screen);
            if (_closed || screen == _screen) return;
            string previous = _screen;
            Record("app_screen", new() { ["screen"] = _screen, ["action"] = "leave" }, seconds: ScreenSeconds());
            _screen = screen;
            _screenAt = DateTime.UtcNow;
            Record("app_screen", new() { ["screen"] = _screen, ["previousScreen"] = previous, ["action"] = "enter" });
            Save();
        }
    }

    public void Record(string kind, Dictionary<string, string> fields, int count = 1, double seconds = 0)
    {
        lock (_gate)
        {
            if (_closed || !Kinds.Contains(kind)) return;
            Prune();
            fields = fields.Where(p => Keys.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Key is "screen" or "action" or "error" or "previousScreen" ? Code(p.Value) : Clean(p.Value));
            count = Math.Clamp(count, 1, 1000);
            string encoded = Encode(kind, fields, count, Math.Clamp(seconds, 0, 86400), !_authenticated, Run);
            // ponytail: 작은 큐 선형 검색; 신호가 초당 수천 개가 되면 별도 집계기로 바꾼다.
            if (kind is "app_button" or "app_error")
            {
                using var incoming = JsonDocument.Parse(encoded);
                var metadata = incoming.RootElement.GetProperty("meta");
                int same = _queue.FindIndex(item =>
                {
                    using var old = JsonDocument.Parse(item);
                    return old.RootElement.GetProperty("kind").GetString() == kind && SameMetadata(old.RootElement.GetProperty("meta"), metadata);
                });
                if (same >= 0)
                {
                    using var old = JsonDocument.Parse(_queue[same]);
                    int previous = old.RootElement.GetProperty("count").GetInt32();
                    _queue[same] = Encode(kind, fields, Math.Min(previous + count, 1000), 0, !_authenticated, Run, old.RootElement.GetProperty("meta").GetProperty("occurredAt").GetString());
                    Save();
                    return;
                }
            }
            if (_queue.Count >= 120) _queue.RemoveAt(0);
            _queue.Add(encoded);
            Save();
        }
    }

    private string Encode(string kind, Dictionary<string, string> fields, int count, double seconds, bool beforeLogin, string run, string? occurredAt = null)
    {
        using var bytes = new MemoryStream();
        using (var writer = new Utf8JsonWriter(bytes))
        {
            writer.WriteStartObject(); writer.WriteString("kind", kind); writer.WriteNumber("count", count);
            writer.WriteStartObject("meta"); writer.WriteString("install", Install); writer.WriteString("run", run);
            writer.WriteBoolean("beforeLogin", beforeLogin);
            writer.WriteString("occurredAt", occurredAt ?? DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            foreach (var field in fields) writer.WriteString(field.Key, field.Value);
            if (seconds > 0) writer.WriteNumber("seconds", (int)Math.Floor(seconds));
            writer.WriteEndObject(); writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    private static bool SameMetadata(JsonElement old, JsonElement incoming) =>
        old.EnumerateObject().Count(p => p.Name != "occurredAt") == incoming.EnumerateObject().Count(p => p.Name != "occurredAt")
        && incoming.EnumerateObject().Where(p => p.Name != "occurredAt").All(p => old.TryGetProperty(p.Name, out var value) && value.GetRawText() == p.Value.GetRawText());
    private static bool OccurredAt(JsonElement meta, out string value)
    {
        value = meta.TryGetProperty("occurredAt", out var field) && field.ValueKind == JsonValueKind.String ? field.GetString() ?? "" : "";
        return Regex.IsMatch(value, @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})$")
            && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var at)
            && at >= DateTimeOffset.UtcNow.AddDays(-90) && at <= DateTimeOffset.UtcNow.AddDays(1);
    }
    private void Prune()
    {
        if (_queue.RemoveAll(entry => { using var doc = JsonDocument.Parse(entry); return !OccurredAt(doc.RootElement.GetProperty("meta"), out _); }) > 0) Save();
    }
    public string? Peek() { lock (_gate) { Prune(); return _authenticated && _queue.Count > 0 ? _queue[0] : null; } }
    public string[] Tail(int count) { lock (_gate) { Prune(); return _queue.TakeLast(count).ToArray(); } }
    public void Sent(string entry) { lock (_gate) { if (_queue.Remove(entry)) Save(); } }
    private double ScreenSeconds() => _focused ? Math.Max(0, (DateTime.UtcNow - _screenAt).TotalSeconds) : 0;
    public void FlushScreen() { lock (_gate) { Record("app_screen", new() { ["screen"] = _screen }, seconds: ScreenSeconds()); _screenAt = DateTime.UtcNow; } }
    public void Focus(bool focused)
    {
        lock (_gate)
        {
            if (_closed || _focused == focused) return;
            if (!focused) FlushScreen();
            _focused = focused;
            _screenAt = DateTime.UtcNow;
        }
    }
    public void Crash(string code)
    {
        lock (_gate) { _fatal = Code(code); Save(); }
    }
    public void Close()
    {
        lock (_gate)
        {
            if (_closed) return;
            Record("app_screen", new() { ["screen"] = _screen }, seconds: ScreenSeconds());
            Record("app_lifecycle", new() { ["action"] = "clean_exit", ["screen"] = _screen });
            _closed = true;
            Save();
        }
    }
    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            using var bytes = new MemoryStream();
            using (var writer = new Utf8JsonWriter(bytes))
            {
                writer.WriteStartObject(); writer.WriteString("install", Install); writer.WriteBoolean("dirty", !_closed); writer.WriteString("screen", _screen);
                writer.WriteString("fatal", _fatal);
                writer.WriteStartArray("queue"); foreach (string entry in _queue) writer.WriteRawValue(entry); writer.WriteEndArray(); writer.WriteEndObject();
            }
            string temp = _path + ".tmp";
            var options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write };
            if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            using (var file = new FileStream(temp, options)) file.Write(bytes.ToArray());
            File.Move(temp, _path, true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    }
    public static byte[] Packet(string json)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        if (bytes.Length > 2048) throw new ArgumentException("활동 기록 길이 초과", nameof(json));
        byte[] result = new byte[bytes.Length + 2];
        BinaryPrimitives.WriteUInt16BigEndian(result, (ushort)bytes.Length);
        bytes.CopyTo(result, 2);
        return result;
    }
}
