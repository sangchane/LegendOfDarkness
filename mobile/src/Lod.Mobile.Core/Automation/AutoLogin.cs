namespace Lod.Mobile.Core.Automation;

/// <summary>
/// Decides, once per run, whether a launch-time (<c>--login</c> / <c>login.cfg</c>) or a saved account may
/// be filled in and submitted with no tap.
/// </summary>
/// <remarks>
/// An explicit logout (게임 안 [종료] → [로그아웃]) must leave the player at the login screen for the rest
/// of this run — otherwise the convenience would make logging out impossible to act on, putting the same
/// account straight back into the world. A fresh gate starts able to submit, which is the one launch-time
/// submission the app already trusts.
/// </remarks>
public sealed class AutoLoginGate
{
    private bool _loggedOutThisRun;

    /// <summary>Whether an account may be filled in and submitted right now, with no tap.</summary>
    public bool MaySubmit => !_loggedOutThisRun;

    /// <summary>Call when the player explicitly logs out — stops auto sign-in for the rest of this run.</summary>
    public void NoteLogout() => _loggedOutThisRun = true;
}
