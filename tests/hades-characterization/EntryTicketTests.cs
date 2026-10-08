using Darkages.Security;
using Darkages.Types;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// P0-12: a game entry ticket admits one connection. Format10Handler called EnterGame before checking the
/// ticket at all, so an unverified connection reached the world first and was disconnected only afterwards.
/// </summary>
public sealed class EntryTicketTests
{
    private const string TicketHolder = "ticketuser";

    [Fact]
    public void One_entry_ticket_admits_exactly_one_connection()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.GameTicket ticket = LoginFlow.LoginAndCaptureTicket(server, TicketHolder);

        using Hades718TestClient first = Hades718TestClient.Connect(ticket.Target.Port);
        using Hades718TestClient second = Hades718TestClient.Connect(ticket.Target.Port);

        first.SendRedirectRequest(ticket.Target);
        second.SendRedirectRequest(ticket.Target);

        string welcome = LoginFlow.WelcomeMessage(TicketHolder);
        LoginFlow.WaitForLog(server, welcome, TimeSpan.FromSeconds(30));

        // Wait past the first entry so a second one would have been logged by now if the server allowed it.
        Thread.Sleep(2000);

        Assert.Equal(1, CountOccurrences(server.ConsoleOutput, welcome));
    }

    /// <summary>
    /// 리뷰 2026-10-08 #1: 게임 서버가 이름만 보고 들였다. 로그인한 그 접속이 받은 Id·암호 매개변수가 아니면 들이지 않고,
    /// 틀린 시도가 정상 입장을 막지도 않는다.
    /// </summary>
    [Fact]
    public void A_ticket_with_the_right_name_but_the_wrong_id_or_salt_is_refused_and_the_real_one_still_enters()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.GameTicket ticket = LoginFlow.LoginAndCaptureTicket(server, TicketHolder);
        RedirectTarget real = ticket.Target;
        RedirectTarget wrongId = real with { Serial = Flipped(real.Serial) };
        RedirectTarget wrongSalt = real with { Salt = Flipped(real.Salt) };

        using Hades718TestClient forgedId = Hades718TestClient.Connect(real.Port);
        using Hades718TestClient forgedSalt = Hades718TestClient.Connect(real.Port);
        forgedId.SendRedirectRequest(wrongId);
        forgedSalt.SendRedirectRequest(wrongSalt);

        string welcome = LoginFlow.WelcomeMessage(TicketHolder);
        Thread.Sleep(2000);
        Assert.Equal(0, CountOccurrences(server.ConsoleOutput, welcome));

        using Hades718TestClient owner = Hades718TestClient.Connect(real.Port);
        owner.SendRedirectRequest(real);
        LoginFlow.WaitForLog(server, welcome, TimeSpan.FromSeconds(30));
        Assert.Equal(1, CountOccurrences(server.ConsoleOutput, welcome));
    }

    [Fact]
    public void A_ticket_admits_only_its_own_id_and_parameters_and_only_once()
    {
        DateTime now = new(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);
        byte[] salt = "ABC123XYZ"u8.ToArray();
        EntryTickets.Issue("Holder", 42, new SecurityParameters(3, salt), now);

        Assert.False(EntryTickets.TryConsume("holder", 43, new SecurityParameters(3, salt), now));
        Assert.False(EntryTickets.TryConsume("holder", 42, new SecurityParameters(4, salt), now));
        Assert.False(EntryTickets.TryConsume("holder", 42, new SecurityParameters(3, "ABC123XYQ"u8.ToArray()), now));
        Assert.False(EntryTickets.TryConsume("other", 42, new SecurityParameters(3, salt), now));
        Assert.True(EntryTickets.TryConsume("HOLDER", 42, new SecurityParameters(3, salt), now.AddSeconds(5)));
        Assert.False(EntryTickets.TryConsume("holder", 42, new SecurityParameters(3, salt), now.AddSeconds(6)));
    }

    [Fact]
    public void A_ticket_left_unused_expires()
    {
        DateTime now = new(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);
        byte[] salt = "QWE456RTY"u8.ToArray();
        EntryTickets.Issue("late", 7, new SecurityParameters(1, salt), now);

        Assert.False(EntryTickets.TryConsume("late", 7, new SecurityParameters(1, salt), now + EntryTickets.Lifetime + TimeSpan.FromSeconds(1)));
        // 만료로 지운 뒤에는 제때 와도 없다.
        Assert.False(EntryTickets.TryConsume("late", 7, new SecurityParameters(1, salt), now));
    }

    private static byte[] Flipped(byte[] value)
    {
        byte[] copy = [.. value];
        copy[^1] ^= 0x01;
        return copy;
    }

    private static int CountOccurrences(string text, string value)
    {
        int count = 0;
        int index = text.IndexOf(value, StringComparison.Ordinal);

        while (index >= 0)
        {
            count++;
            index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal);
        }

        return count;
    }
}
