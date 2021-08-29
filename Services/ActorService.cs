using Akka.Actor;
using Akka.Configuration;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Rovio.MatchMaking.Actors;

namespace Rovio.MatchMaking.Services
{
    public class ActorService : IHostedService
    {
        private ActorSystem _system;
        private readonly Dictionary<Guid, IActorRef> _lobbyLookup = new();

        public ActorService()
        {
        }

        #region IHostedService
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var hocon = System.IO.File.Exists("akka.hocon")
                ? await System.IO.File.ReadAllTextAsync("akka.hocon")
                : string.Empty;
            var config = ConfigurationFactory.ParseString(hocon);
            _system = ActorSystem.Create("match-making", config);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _system.Terminate();

            return Task.CompletedTask;
        }
        #endregion IHostedService

        public async Task<List<Lobby.Ticket>> CreateSessionAsync(Models.Session session, TimeSpan timeOut)
        {
            if (session is null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            var lobby = GetLobbyRef(session.LobbyId);

            if (lobby == Akka.Actor.Nobody.Instance)
            {
                return new();
            }

            var result = await lobby.Ask(
                new Lobby.CreateSession(
                    session.LobbyId,
                    session.MinTickets,
                    session.MaxTickets,
                    NodaTime.Duration.FromSeconds(session.MaxWaitSeconds).BclCompatibleTicks),
                timeOut);

            return result as List<Lobby.Ticket>;
        }

        public void DequeueTicket(Guid lobbyId, Guid ticketId)
        {
            var lobby = GetLobbyRef(lobbyId);
        }

        public Lobby.Ticket QueueTicket(Guid lobbyId, double latency)
        {
            var ticket = new Lobby.Ticket(lobbyId, latency);

            TellTicket(lobbyId, ticket);

            return ticket;
        }

        public Lobby.Ticket UpdateTicket(Guid lobbyId, Guid ticketId, double latency)
        {
            var ticket = new Lobby.Ticket(lobbyId, ticketId, latency);

            TellTicket(lobbyId, ticket);

            return ticket;
        }

        private void TellTicket(Guid lobbyId, Lobby.Ticket ticket)
        {
            if (ticket is null)
            {
                throw new ArgumentNullException(nameof(ticket));
            }

            var lobby = GetLobbyRef(lobbyId, true);

            if (lobby == Akka.Actor.Nobody.Instance)
            {
                throw new InvalidOperationException("Unable to retrieve lobby actor reference.");
            }

            lobby.Tell(ticket);
        }

        private IActorRef GetLobbyRef(Guid lobbyId, bool createIfNotExists = false)
        {
            if (!_lobbyLookup.TryGetValue(lobbyId, out IActorRef lobby) &&
                createIfNotExists)
            {
                lobby = _system.ActorOf(Lobby.Props(), lobbyId.ToString());
                _lobbyLookup.Add(lobbyId, lobby);
                return lobby;
            }

            return lobby;
        }
    }
}