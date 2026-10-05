using Godot;

namespace LodClient;

/// <summary>앱 시작 — 로그인·만들기·게임 화면 사이 옮겨 가기.</summary>
public partial class Main : Control
{
    /// <summary>Swaps the login screen for the world the connection leads to.</summary>
    private void Enter(LoginScreen login, Lod.Mobile.Core.Net.WorldSession session)
    {
        RemoveChild(login);
        login.QueueFree();

        var world = new Lod.Mobile.Core.Protocol.World.WorldClient(session);
        BindActivity(world);
        GameScreen game = new(world);

        game.LoggedOut = () => Callable.From(() => BackToLogin(game)).CallDeferred();
        game.Rejoin = () => Callable.From(() => Rejoin(game)).CallDeferred();
        AddChild(game);
    }

    private LoginScreen BuildLoginScreen()
    {
        LoginScreen login = new() { AutomaticLogin = _autoLoginGate.MaySubmit };

        // Deferred, because this runs from the login screen's own frame and the tree may not be changed
        // in the middle of one.
        login.Entered = session => Callable.From(() => Enter(login, session)).CallDeferred();
        login.WantsToCreate = () => Callable.From(() => GoToCreate(login)).CallDeferred();

        return login;
    }

    private CreateScreen BuildCreateScreen()
    {
        CreateScreen create = new();

        create.Cancelled = () => Callable.From(() => BackToLogin(create)).CallDeferred();
        create.Entered = session => Callable.From(() => Enter(create, session)).CallDeferred();

        return create;
    }

    /// <summary>Swaps a completed creation screen directly for its already authenticated game session.</summary>
    private void Enter(CreateScreen create, Lod.Mobile.Core.Net.WorldSession session)
    {
        if (!GodotObject.IsInstanceValid(create) || create.GetParent() != this)
        {
            session.Dispose();
            return;
        }

        RemoveChild(create);
        create.QueueFree();

        var world = new Lod.Mobile.Core.Protocol.World.WorldClient(session);
        BindActivity(world);
        GameScreen game = new(world);
        game.LoggedOut = () => Callable.From(() => BackToLogin(game)).CallDeferred();
        game.Rejoin = () => Callable.From(() => Rejoin(game)).CallDeferred();
        AddChild(game);
    }

    /// <summary>
    /// 자동 사냥 중 끊겼다(대신 사냥에 맡김) — 로그인 화면으로 가서 저장 계정으로 다시 들어가고, 자리를 잡으면 자동 사냥을 다시 켠다.
    /// 스스로 로그아웃한 것이 아니라 자동 로그인을 막지 않는다. 다시 들어가면 서버가 대리를 밀어낸다.
    /// </summary>
    private void Rejoin(GameScreen game)
    {
        if (!GodotObject.IsInstanceValid(game) || game.GetParent() != this)
        {
            return;
        }

        RemoveChild(game);
        game.QueueFree();

        BindActivity(null);
        ResumeAutoHunt = true;
        AddChild(BuildLoginScreen());
    }

    /// <summary>계정이 없어 만들기로 간다.</summary>
    private void GoToCreate(LoginScreen login)
    {
        RemoveChild(login);
        login.QueueFree();

        AddChild(BuildCreateScreen());
    }

    /// <summary>취소를 눌러 로그인 화면으로 돌아간다.</summary>
    private void BackToLogin(CreateScreen create)
    {
        RemoveChild(create);
        create.QueueFree();

        AddChild(BuildLoginScreen());
    }

    /// <summary>The game has already released its connection; replace its node on the next safe tree turn.</summary>
    private void BackToLogin(GameScreen game)
    {
        if (!GodotObject.IsInstanceValid(game) || game.GetParent() != this)
        {
            return;
        }

        RemoveChild(game);
        game.QueueFree();

        // login.cfg, --login and a saved account are all launch conveniences. An explicit logout must not
        // consume them again and immediately put the same account back in the world.
        BindActivity(null);
        ResumeAutoHunt = false;
        _autoLoginGate.NoteLogout();
        AddChild(BuildLoginScreen());
    }
}
