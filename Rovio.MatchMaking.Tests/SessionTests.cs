using Akka.Actor;
using Akka.TestKit.Xunit;
using Rovio.MatchMaking.Actors;
using System;
using Xunit;

namespace Rovio.MatchMaking.Tests
{
    public class SessionTests : TestKit
    {
        #region Lobby.Ticket
        [Fact]
        public void SessionFilled_TellsLobbyClose()
        {
            var lobbyProbe = CreateTestProbe();
            var lobbyId = Guid.NewGuid();
            var sessionId = Guid.NewGuid();
            var subject = Sys.ActorOf(Session.Props(lobbyProbe, lobbyId, sessionId, 100, 20, 1));

            subject.Tell(new Lobby.Ticket(lobbyId, 100), lobbyProbe);
            lobbyProbe.ExpectMsg<Session.Close>(TimeSpan.FromSeconds(3));
        }

        [Fact]
        public void SessionFilled_ReturnsExtraTicketsToLobby()
        {
            var lobbyProbe = CreateTestProbe();
            lobbyProbe.IgnoreMessages(message => message is Session.Close);
            var lobbyId = Guid.NewGuid();
            var sessionId = Guid.NewGuid();
            var subject = Sys.ActorOf(Session.Props(lobbyProbe, lobbyId, sessionId, 100, 20, 1));

            // Fill the session
            subject.Tell(new Lobby.Ticket(lobbyId, 100), lobbyProbe);
            // Extra ticket
            subject.Tell(new Lobby.Ticket(lobbyId, 100), lobbyProbe);

            lobbyProbe.ExpectMsg<Lobby.Ticket>(TimeSpan.FromSeconds(3));
        }

        [Fact]
        public void RunningSessionClosed_TellsLobbyClose()
        {
            var lobbyProbe = CreateTestProbe();
            var lobbyId = Guid.NewGuid();
            var sessionId = Guid.NewGuid();
            var subject = Sys.ActorOf(Session.Props(lobbyProbe, lobbyId, sessionId, 100, 20, 10));

            subject.Tell(new Session.Close(sessionId), TestActor);

            lobbyProbe.ExpectMsg<Session.Close>(TimeSpan.FromSeconds(3));
        }

        [Fact]
        public void RunningSessionClosed_ReturnsTicketsToLobby()
        {
            var lobbyProbe = CreateTestProbe();
            lobbyProbe.IgnoreMessages(message => message is Session.Close);
            var lobbyId = Guid.NewGuid();
            var sessionId = Guid.NewGuid();
            var subject = Sys.ActorOf(Session.Props(lobbyProbe, lobbyId, sessionId, 100, 20, 10));

            subject.Tell(new Lobby.Ticket(lobbyId, 100), lobbyProbe);
            subject.Tell(new Session.Close(sessionId), TestActor);

            lobbyProbe.ExpectMsg<Lobby.Ticket>(TimeSpan.FromSeconds(3));
        }

        [Fact]
        public void FilledSessionClosed_ReturnsTicketsToLobby()
        {
            var lobbyProbe = CreateTestProbe();
            lobbyProbe.IgnoreMessages(message => message is Session.Close);
            var lobbyId = Guid.NewGuid();
            var sessionId = Guid.NewGuid();
            var subject = Sys.ActorOf(Session.Props(lobbyProbe, lobbyId, sessionId, 100, 20, 1));

            subject.Tell(new Lobby.Ticket(lobbyId, 100), lobbyProbe);
            subject.Tell(new Session.Close(sessionId), TestActor);
            lobbyProbe.ExpectMsg<Lobby.Ticket>(TimeSpan.FromSeconds(3));
        }

        [Fact]
        public void ClosedSession_ReturnsTicketsToLobby()
        {
            var lobbyProbe = CreateTestProbe();
            lobbyProbe.IgnoreMessages(message => message is Session.Close);
            var lobbyId = Guid.NewGuid();
            var sessionId = Guid.NewGuid();
            var subject = Sys.ActorOf(Session.Props(lobbyProbe, lobbyId, sessionId, 100, 20, 1));

            subject.Tell(new Session.Close(sessionId), TestActor);
            subject.Tell(new Lobby.Ticket(lobbyId, 100), lobbyProbe);
            lobbyProbe.ExpectMsg<Lobby.Ticket>(TimeSpan.FromSeconds(3));
        }
        #endregion Lobby.Ticket

        #region Session.ClaimTickets
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public void RunningSession_ReturnsClaimedTickets(int ticketCount)
        {
            var lobbyProbe = CreateTestProbe();
            var lobbyId = Guid.NewGuid();
            var sessionId = Guid.NewGuid();
            var subject = Sys.ActorOf(Session.Props(lobbyProbe, lobbyId, sessionId, 100, 20, 10));

            for (int i = 0; i < ticketCount; i++)
            {
                subject.Tell(new Lobby.Ticket(lobbyId, 100), lobbyProbe);
            }

            subject.Tell(Session.ClaimTickets.Instance, TestActor);

            var session = ExpectMsg<Lobby.Session>(TimeSpan.FromSeconds(3));
            Assert.Equal(ticketCount, session.Tickets.Count);
        }

        [Fact]
        public void RunningSession_TracksRemainingSlots()
        {
            var lobbyProbe = CreateTestProbe();
            lobbyProbe.IgnoreMessages(message => !(message is Session.Close));
            var lobbyId = Guid.NewGuid();
            var sessionId = Guid.NewGuid();
            var subject = Sys.ActorOf(Session.Props(lobbyProbe, lobbyId, sessionId, 100, 20, 2));

            subject.Tell(new Lobby.Ticket(lobbyId, 100), lobbyProbe);
            subject.Tell(Session.ClaimTickets.Instance, TestActor);
            subject.Tell(new Lobby.Ticket(lobbyId, 100), lobbyProbe);

            lobbyProbe.ExpectMsg<Session.Close>(TimeSpan.FromSeconds(3));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(10)]
        public void FilledSession_ReturnsAllTickets(int ticketCount)
        {
            //Given
            var lobbyProbe = CreateTestProbe();
            var lobbyId = Guid.NewGuid();
            var sessionId = Guid.NewGuid();
            var subject = Sys.ActorOf(Session.Props(lobbyProbe, lobbyId, sessionId, 100, 20, ticketCount));

            //When
            for (int i = 0; i < ticketCount; i++)
            {
                subject.Tell(new Lobby.Ticket(lobbyId, 100), lobbyProbe);
            }
            subject.Tell(Session.ClaimTickets.Instance, TestActor);

            //Then
            var session = ExpectMsg<Lobby.Session>(TimeSpan.FromSeconds(3));
            Assert.Equal(ticketCount, session.Tickets.Count);
        }

        [Fact]
        public void ClosingSession_ReturnsNoTickets()
        {
            //Given
            var lobbyProbe = CreateTestProbe();
            var lobbyId = Guid.NewGuid();
            var sessionId = Guid.NewGuid();
            var subject = Sys.ActorOf(Session.Props(lobbyProbe, lobbyId, sessionId, 100, 20, 10));

            //When
            subject.Tell(new Lobby.Ticket(lobbyId, 100), lobbyProbe);
            subject.Tell(new Session.Close(sessionId), TestActor);
            subject.Tell(Session.ClaimTickets.Instance, TestActor);

            //Then
            var session = ExpectMsg<Lobby.Session>(TimeSpan.FromSeconds(3));
            Assert.Empty(session.Tickets);
        }
        #endregion Session.ClaimTickets


        #region Session.Close
        [Fact]
        public void FilledSession_ShutdownAfterClaim()
        {
            //Given
            var lobbyProbe = CreateTestProbe();
            var deathWatch = CreateTestProbe();
            var lobbyId = Guid.NewGuid();
            var sessionId = Guid.NewGuid();
            var subject = Sys.ActorOf(Session.Props(lobbyProbe, lobbyId, sessionId, 100, 20, 1));
            deathWatch.Watch(subject);

            //When
            subject.Tell(new Lobby.Ticket(lobbyId, 100), lobbyProbe);
            // The lobby needs to acknowledge the close sent by the session
            subject.Tell(new Session.Close(sessionId), lobbyProbe);
            subject.Tell(Session.ClaimTickets.Instance, TestActor);

            //Then
            var msg = deathWatch.ExpectMsg<Terminated>(TimeSpan.FromSeconds(3));
            Assert.Equal(subject, msg.ActorRef);
        }

        [Fact]
        public void FilledSession_WaitForFinalClaim()
        {
            //Given
            var lobbyProbe = CreateTestProbe();
            var deathWatch = CreateTestProbe();
            var lobbyId = Guid.NewGuid();
            var sessionId = Guid.NewGuid();
            var subject = Sys.ActorOf(Session.Props(lobbyProbe, lobbyId, sessionId, 100, 20, 1));
            deathWatch.Watch(subject);

            //When
            subject.Tell(new Lobby.Ticket(lobbyId, 100), lobbyProbe);
            // The lobby needs to acknowledge the close sent by the session
            subject.Tell(new Session.Close(sessionId), lobbyProbe);

            //Then
            deathWatch.ExpectNoMsg(TimeSpan.FromSeconds(3));
        }

        [Fact]
        public void ClosingSession_WaitForLobbyToAcknowledge()
        {
            //Given
            var lobbyProbe = CreateTestProbe();
            var deathWatch = CreateTestProbe();
            var lobbyId = Guid.NewGuid();
            var sessionId = Guid.NewGuid();
            var subject = Sys.ActorOf(Session.Props(lobbyProbe, lobbyId, sessionId, 100, 20, 1));
            deathWatch.Watch(subject);

            //When
            subject.Tell(new Session.Close(sessionId), TestActor);

            //Then
            deathWatch.ExpectNoMsg(TimeSpan.FromSeconds(3));
        }

        [Fact]
        public void ClosingSession_ShutdownOnLobbyAcknowledge()
        {
            //Given
            var lobbyProbe = CreateTestProbe();
            var deathWatch = CreateTestProbe();
            var lobbyId = Guid.NewGuid();
            var sessionId = Guid.NewGuid();
            var subject = Sys.ActorOf(Session.Props(lobbyProbe, lobbyId, sessionId, 100, 20, 1));
            deathWatch.Watch(subject);

            //When
            subject.Tell(new Session.Close(sessionId), TestActor);
            subject.Tell(new Session.Close(sessionId), lobbyProbe);

            //Then
            var msg = deathWatch.ExpectMsg<Terminated>(TimeSpan.FromSeconds(3));
            Assert.Equal(subject, msg.ActorRef);
        }
        #endregion Session.Close
    }
}
