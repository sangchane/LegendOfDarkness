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

        GameScreen game = new(new Lod.Mobile.Core.World.WorldClient(session));

        game.LoggedOut = () => Callable.From(() => BackToLogin(game)).CallDeferred();
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

        GameScreen game = new(new Lod.Mobile.Core.World.WorldClient(session));
        game.LoggedOut = () => Callable.From(() => BackToLogin(game)).CallDeferred();
        AddChild(game);
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
        _autoLoginGate.NoteLogout();
        AddChild(BuildLoginScreen());
    }
}
