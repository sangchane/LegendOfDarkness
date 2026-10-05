using Godot;
using Lod.Mobile.Core.Diagnostics;
using Lod.Mobile.Core.Protocol.World;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LodClient;

/// <summary>운영 신호 배선. 버튼 문구·입력칸·노드 이름은 읽지 않는다.</summary>
public partial class Main
{
    private AppActivity? _activity;
    private WorldClient? _activityWorld;
    private double _activityTimer;
    private Task? _activitySend;
    public static Main? ActivityHost { get; private set; }
    public static void NoteActivityError(string code) => ActivityHost?._activity?.Record("app_error", new() { ["error"] = code });

    private void StartActivity()
    {
        ActivityHost = this;
        string version = Godot.FileAccess.FileExists("res://app-version.txt") ? Godot.FileAccess.GetFileAsString("res://app-version.txt").Trim() : "development";
        _activity = new AppActivity(ProjectSettings.GlobalizePath("user://activity.json"), OS.GetName(), OS.GetModelName(), OS.GetVersion(), version);
        GetTree().NodeAdded += WatchActivityNode;
        AppDomain.CurrentDomain.UnhandledException += ActivityUnhandled;
        TaskScheduler.UnobservedTaskException += ActivityTaskError;
        WatchActivityTree(this);
    }
    private void ActivityUnhandled(object sender, UnhandledExceptionEventArgs args) => _activity?.Crash(args.ExceptionObject is Exception error ? error.GetType().Name : "unhandled_exception");
    private void ActivityTaskError(object? sender, UnobservedTaskExceptionEventArgs args) => NoteActivityError(args.Exception.GetBaseException().GetType().Name);
    private void WatchActivityTree(Node node)
    {
        WatchActivityNode(node);
        foreach (Node child in node.GetChildren()) WatchActivityTree(child);
    }
    private void WatchActivityNode(Node node)
    {
        if (node.HasMeta("lod_activity_hook")) return;
        node.SetMeta("lod_activity_hook", true);
        if (node is Control watched && watched.GetType().Namespace == "LodClient")
            watched.VisibilityChanged += () => Callable.From(() => _activity?.Screen(ActivityScreen())).CallDeferred();
        if (node is BaseButton button)
            button.Pressed += () =>
            {
                // 형식과 구조 자리만 고정 식별자로 쓴다. 사람/아이템 이름이 들어갈 수 있는 Text/Name은 제외.
                List<string> parts = [];
                for (Node? parent = button; parent is not null && parent != this && parts.Count < 6; parent = parent.GetParent())
                    parts.Add(parent.GetType().Name + "_" + parent.GetIndex());
                _activity?.Record("app_button", new() { ["screen"] = ActivityScreen(), ["action"] = string.Join('.', parts) });
            };
    }
    private string ActivityScreen()
    {
        string screen = "startup";
        void Visit(Node node)
        {
            if (node is Control control && !control.IsVisibleInTree()) return;
            string type = node.GetType().Name;
            if (node is LoginScreen or CreateScreen or GameScreen or ExitChoice || (node.GetType().Namespace == "LodClient" && type.EndsWith("Panel", StringComparison.Ordinal))) screen = type;
            foreach (Node child in node.GetChildren()) Visit(child);
        }
        Visit(this);
        return screen;
    }
    private void BindActivity(WorldClient? world)
    {
        if (_activityWorld is not null) _activityWorld.Diagnostic -= NoteActivityError;
        _activityWorld = world;
        _activity?.Authenticate(world is not null);
        if (world is not null) world.Diagnostic += NoteActivityError;
    }
    public override void _Process(double delta)
    {
        _activityTimer += delta;
        if (_activityTimer < 2) return;
        _activityTimer = 0;
        _activity?.Screen(ActivityScreen());
        if (_activityWorld is not { IsDisposed: false } world || _activitySend is { IsCompleted: false } || _activity?.Peek() is not { } entry) return;
        _activitySend = SendActivity(world, entry);
    }
    private async Task SendActivity(WorldClient world, string entry)
    {
        try { await world.SendActivityAsync(entry, CancellationToken.None); _activity?.Sent(entry); }
        catch (Exception error) when (error is System.IO.IOException or System.Net.Sockets.SocketException or ObjectDisposedException or OperationCanceledException or ArgumentException) { }
    }
    private void ActivityFocus(bool focused)
    {
        _activity?.Focus(focused);
        _activity?.Record("app_lifecycle", new() { ["action"] = focused ? "focus_in" : "focus_out", ["screen"] = ActivityScreen() });
        if (!focused && _activityWorld is { IsDisposed: false } world && _activity?.Tail(1) is [var entry])
            _activitySend = SendActivity(world, entry);
    }
    public override void _ExitTree()
    {
        GetTree().NodeAdded -= WatchActivityNode;
        AppDomain.CurrentDomain.UnhandledException -= ActivityUnhandled;
        TaskScheduler.UnobservedTaskException -= ActivityTaskError;
        _activity?.Close();
        if (_activityWorld is not null) _activityWorld.Diagnostic -= NoteActivityError;
        _activityWorld = null;
        ActivityHost = null;
    }
    internal async Task FinishActivity(bool quitting)
    {
        _activity?.Screen(ActivityScreen());
        _activity?.FlushScreen();
        _activity?.Record("app_lifecycle", new() { ["action"] = quitting ? "quit" : "logout", ["screen"] = ActivityScreen() });
        // 현재 연결의 마지막 화면/이탈은 소켓을 닫기 전에 전달한다. 오래된 큐는 다음 로그인 때 이어 보낸다.
        if (_activityWorld is { IsDisposed: false } world)
        {
            if (_activitySend is { IsCompleted: false }) await Task.WhenAny(_activitySend, Task.Delay(250));
            foreach (string entry in _activity?.Tail(2) ?? []) await Task.WhenAny(SendActivity(world, entry), Task.Delay(250));
        }
    }
}
