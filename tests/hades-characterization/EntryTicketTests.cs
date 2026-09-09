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
