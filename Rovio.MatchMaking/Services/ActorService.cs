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

        public void CloseSession(Guid lobbyId, Guid sessionId)
        {
            var session = _system.ActorSelection($"user/{lobbyId}/{sessionId}");
            session.Tell(new Session.Close(sessionId));
        }

        public async Task<Lobby.Session> CreateSessionAsync(Guid lobbyId, int minTickets, int maxTickets, int maxWaitSeconds, TimeSpan timeOut)
        {
            if (lobbyId == Guid.Empty)
            {
                throw new ArgumentException("The lobby ID is invalid", nameof(lobbyId));
            }
            if (minTickets < 1)
            {
                throw new ArgumentException("The minimum session size be greater than 0", nameof(minTickets));
            }
            if (maxTickets < minTickets)
            {
                throw new ArgumentException("The session maximum tickets cannot be less than the minimum tickets", nameof(maxTickets));
            }
            if (maxWaitSeconds < 0)
            {
                throw new ArgumentException("The maximum wait time for the lobby must be positive", nameof(maxWaitSeconds));
            }

            var lobby = GetLobbyRef(lobbyId);

            if (lobby == Akka.Actor.Nobody.Instance)
            {
                return null;
            }

            var result = await lobby.Ask(
                new Lobby.CreateSession(
                    lobbyId,
                    minTickets,
                    maxTickets,
                    NodaTime.Duration.FromSeconds(maxWaitSeconds).BclCompatibleTicks),
                timeOut);

            return result as Lobby.Session;
        }

        public void DequeueTicket(Guid lobbyId, Guid ticketId)
        {
            var lobby = GetLobbyRef(lobbyId);
        }

        public async Task<Lobby.Session> GetSessionTicketsAsync(Guid lobbyId, Guid sessionId)
        {
            var session = _system.ActorSelection($"user/{lobbyId}/{sessionId}");

            var result = await session.Ask(Session.ClaimTickets.Instance);

            return result as Lobby.Session;
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
            // If the lobby doesn't exist or if the existing actor ref has died
            if ((!_lobbyLookup.TryGetValue(lobbyId, out IActorRef lobbyRef) || lobbyRef == Akka.Actor.Nobody.Instance) &&
                createIfNotExists)
            {
                lobbyRef = _system.ActorOf(Lobby.Props(), lobbyId.ToString());
                // Overwrite the dead actor ref
                if (_lobbyLookup.ContainsKey(lobbyId))
                {
                    _lobbyLookup[lobbyId] = lobbyRef;
                }
                // Add the new actor ref
                else
                {
                    _lobbyLookup.Add(lobbyId, lobbyRef);
                }
                return lobbyRef;
            }

            return lobbyRef ?? Akka.Actor.Nobody.Instance;
        }
    }
}