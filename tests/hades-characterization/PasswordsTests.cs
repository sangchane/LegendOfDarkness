using Darkages.Security;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// Passwords used to be written into the character file exactly as typed. The thing that must not break
/// while fixing that is the people who already have accounts: their stored value is still plain, and they
/// have to be able to sign in with it once so it can be moved.
/// </summary>
public sealed class PasswordsTests
{
    [Fact]
    public void A_hashed_value_does_not_contain_the_password()
    {
        string stored = Passwords.Hash("test1234");

        Assert.DoesNotContain("test1234", stored);
        Assert.True(Passwords.IsHashed(stored));
    }

    [Fact]
    public void The_same_password_hashes_differently_every_time()
    {
        // Different salts. Two accounts with one password must not look alike in the files.
        Assert.NotEqual(Passwords.Hash("same"), Passwords.Hash("same"));
    }

    [Fact]
    public void A_hashed_value_accepts_its_own_password_and_nothing_else()
    {
        string stored = Passwords.Hash("test1234");

        Assert.True(Passwords.Verify(stored, "test1234", out bool rehash));
        Assert.False(rehash);

        Assert.False(Passwords.Verify(stored, "test1235", out _));
        Assert.False(Passwords.Verify(stored, "", out _));
        Assert.False(Passwords.Verify(stored, "TEST1234", out _));
    }

    [Fact]
    public void An_account_from_before_the_change_still_signs_in_and_asks_to_be_moved()
    {
        // What such an account holds: the password, as typed.
        Assert.True(Passwords.Verify("test1234", "test1234", out bool rehash));
        Assert.True(rehash);
    }

    [Fact]
    public void An_account_from_before_the_change_still_rejects_a_wrong_password()
    {
        Assert.False(Passwords.Verify("test1234", "nope", out bool rehash));
        Assert.False(rehash);
    }

    [Fact]
    public void A_stored_value_that_is_damaged_is_refused_rather_than_throwing()
    {
        foreach (string damaged in new[]
        {
            "pbkdf2-sha256$",
            "pbkdf2-sha256$210000$onlythree",
            "pbkdf2-sha256$notanumber$c2FsdA==$aGFzaA==",
            "pbkdf2-sha256$210000$!!!notbase64!!!$aGFzaA==",
            "pbkdf2-sha256$0$c2FsdA==$aGFzaA=="
        })
        {
            Assert.False(Passwords.Verify(damaged, "test1234", out _), damaged);
        }
    }

    [Fact]
    public void Nothing_stored_and_nothing_given_are_both_refused()
    {
        Assert.False(Passwords.Verify(null, "test1234", out _));
        Assert.False(Passwords.Verify(Passwords.Hash("test1234"), null, out _));
    }
}
