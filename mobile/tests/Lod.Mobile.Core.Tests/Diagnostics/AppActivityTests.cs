using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Lod.Mobile.Core.Diagnostics;

namespace Lod.Mobile.Core.Tests.Diagnostics;

public sealed class AppActivityTests
{
    [Fact]
    public void Queue_is_private_bounded_input_free_and_replayed_only_after_authentication()
    {
        string folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string path = Path.Combine(folder, "activity.json");
        try
        {
            var activity = new AppActivity(path, "iOS", "iPhone16,1", "18", "20261003");
            activity.Record("app_button", new() { ["screen"] = "LoginScreen", ["action"] = "submit", ["password"] = "DO_NOT_SEND" });
            Assert.Null(activity.Peek());
            activity.Authenticate(true);
            var entries = activity.Tail(120);
            Assert.DoesNotContain("DO_NOT_SEND", string.Join("", entries));
            Assert.Contains(entries, entry => entry.Contains("\"beforeLogin\":true"));
            for (int n = 0; n < 150; n++) activity.Record("app_button", new() { ["action"] = "button_" + n });
            Assert.Equal(120, activity.Pending);
            if (!OperatingSystem.IsWindows()) Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
            activity.Authenticate(false);
            Assert.Equal(0, activity.Pending); // 직전 계정의 미전송 기록을 다른 계정으로 보내지 않는다.
        }
        finally { Directory.Delete(folder, true); }
    }
    [Fact]
    public void Unclean_run_preserves_install_and_previous_screen_without_claiming_a_crash()
    {
        string folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string path = Path.Combine(folder, "activity.json");
        try
        {
            var first = new AppActivity(path, "iOS", "model", "os", "version");
            first.Screen("SettingsPanel");
            first.Authenticate(true);
            first.Record("app_button", new() { ["action"] = "previous_account_action" });
            first.Crash("InvalidOperationException");
            var next = new AppActivity(path, "iOS", "model", "os", "version");
            Assert.Equal(first.Install, next.Install);
            Assert.NotEqual(first.Run, next.Run);
            Assert.DoesNotContain(next.Tail(120), entry => entry.Contains("previous_account_action"));
            string fatal = Assert.Single(next.Tail(120), entry => entry.Contains("previous_run_exception"));
            Assert.Contains("InvalidOperationException", fatal);
            Assert.Contains("SettingsPanel", fatal);
            Assert.Contains("\"beforeLogin\":true", fatal);
            using (var saved = JsonDocument.Parse(File.ReadAllText(path))) Assert.Equal("", saved.RootElement.GetProperty("fatal").GetString());
            Assert.Contains(next.Tail(120), entry => entry.Contains("unclean_exit") && entry.Contains("SettingsPanel"));
            next.Authenticate(true);
            foreach (string entry in next.Tail(120)) next.Sent(entry);
            next.Close();
            var clean = new AppActivity(path, "iOS", "model", "os", "version");
            Assert.DoesNotContain(clean.Tail(120), entry => entry.Contains("unclean_exit"));
        }
        finally { Directory.Delete(folder, true); }
    }
    [Fact]
    public void Background_time_is_excluded_and_repeated_focus_does_not_duplicate_duration()
    {
        string folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var activity = new AppActivity(Path.Combine(folder, "activity.json"), "iOS", "model", "os", "version");
            activity.Authenticate(true);
            foreach (string entry in activity.Tail(120)) activity.Sent(entry);
            activity.Focus(false);
            foreach (string entry in activity.Tail(120)) activity.Sent(entry);
            activity.Focus(false);
            Assert.Empty(activity.Tail(120));
            activity.Screen("SettingsPanel");
            activity.FlushScreen();
            Assert.All(activity.Tail(120), entry => Assert.DoesNotContain("seconds", entry));
            foreach (string entry in activity.Tail(120)) activity.Sent(entry);
            activity.Focus(true);
            activity.Focus(true);
            Assert.Empty(activity.Tail(120));
            activity.FlushScreen();
            Assert.Contains(activity.Tail(120), entry => entry.Contains("seconds"));
            activity.Focus(false);
            foreach (string entry in activity.Tail(120)) activity.Sent(entry);
            activity.Close();
            Assert.All(activity.Tail(120), entry => Assert.DoesNotContain("seconds", entry));
        }
        finally { Directory.Delete(folder, true); }
    }
    [Fact]
    public void Restore_discards_expired_queue_and_diagnostics_and_keeps_original_occurrence()
    {
        string folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string path = Path.Combine(folder, "activity.json");
        try
        {
            var activity = new AppActivity(path, "iOS", "model", "os", "version");
            activity.Record("app_button", new() { ["action"] = "old_button" });
            activity.Record("app_button", new() { ["action"] = "kept_button" });
            activity.Crash("OldException");
            var saved = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!;
            foreach (var entry in saved["queue"]!.AsArray())
            {
                if (entry!["meta"]!["action"]?.GetValue<string>() == "old_button") entry["meta"]!["occurredAt"] = DateTime.UtcNow.AddDays(-91).ToString("O");
            }
            string occurrence = saved["queue"]!.AsArray().Single(entry => entry!["meta"]!["action"]?.GetValue<string>() == "kept_button")!["meta"]!["occurredAt"]!.GetValue<string>();
            File.WriteAllText(path, saved.ToJsonString());
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-91));
            var next = new AppActivity(path, "iOS", "model", "os", "version");
            Assert.Equal(activity.Install, next.Install);
            Assert.DoesNotContain(next.Tail(120), entry => entry.Contains("old_button") || entry.Contains("OldException") || entry.Contains("unclean_exit"));
            string kept = Assert.Single(next.Tail(120), entry => entry.Contains("kept_button"));
            Assert.Contains(occurrence, kept);
            next.Record("app_button", new() { ["action"] = "merge" });
            string first = Assert.Single(next.Tail(120), entry => entry.Contains("merge"));
            using var before = JsonDocument.Parse(first);
            string original = before.RootElement.GetProperty("meta").GetProperty("occurredAt").GetString()!;
            next.Record("app_button", new() { ["action"] = "merge" });
            string merged = Assert.Single(next.Tail(120), entry => entry.Contains("merge"));
            Assert.Contains(original, merged);
            using var counted = JsonDocument.Parse(merged);
            Assert.Equal(2, counted.RootElement.GetProperty("count").GetInt32());
        }
        finally { Directory.Delete(folder, true); }
    }
    [Fact]
    public void Repeated_button_counts_and_utf8_stringB_are_bounded()
    {
        string folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var activity = new AppActivity(Path.Combine(folder, "activity.json"), "iOS", "아이폰", "os", "version");
            for (int n = 0; n < 10; n++) activity.Record("app_button", new() { ["action"] = "attack" });
            string button = Assert.Single(activity.Tail(120), entry => entry.Contains("app_button"));
            using var json = JsonDocument.Parse(button);
            Assert.Equal(10, json.RootElement.GetProperty("count").GetInt32());
            string device = activity.Tail(120).First(entry => entry.Contains("app_device"));
            Assert.All(device, character => Assert.True(character <= 127));
            byte[] packet = AppActivity.Packet(device);
            Assert.Equal(packet.Length - 2, BinaryPrimitives.ReadUInt16BigEndian(packet));
            Assert.Equal(device, Encoding.UTF8.GetString(packet.AsSpan(2)));
            Assert.Throws<ArgumentException>(() => AppActivity.Packet(new string('a', 2049)));
        }
        finally { Directory.Delete(folder, true); }
    }
}
