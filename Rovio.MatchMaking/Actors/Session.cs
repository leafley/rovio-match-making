using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;

namespace Rovio.MatchMaking.Actors
{
    public class Session : ReceiveActor
    {
        public static Props Props(
            IActorRef lobby,
            Guid lobbyId,
            Guid sessionId,
            double meanLatency,
            double standardDeviation,
            int remainingSlots,
            TimeSpan heartbeat) =>
            Akka.Actor.Props.Create(() => new Session(
                lobby,
                lobbyId,
                sessionId,
                meanLatency,
                standardDeviation,
                remainingSlots,
                heartbeat));

        private readonly Dictionary<Guid, Lobby.Ticket> _tickets;
        private readonly IActorRef _lobby;
        private readonly Guid _lobbyId;
        private readonly Guid _sessionId;
        private readonly double _meanLatency;
        private readonly double _standardDeviation;
        private bool _safeToClose;
        private int _remainingSlots;

        public Session(
            IActorRef lobby,
            Guid lobbyId,
            Guid sessionId,
            double meanLatency,
            double standardDeviation,
            int remainingSlots,
            TimeSpan heartbeat)
        {
            if (lobby is null || lobby == Akka.Actor.Nobody.Instance)
            {
                throw new ArgumentException("Invalid lobby actor ref", nameof(lobby));
            }
            if (lobbyId == Guid.Empty)
            {
                throw new ArgumentException("Invalid lobby ID", nameof(lobbyId));
            }
            if (meanLatency < 0)
            {
                throw new ArgumentException("The mean latency must be positive", nameof(meanLatency));
            }
            if (standardDeviation < 0)
            {
                throw new ArgumentException("The standard deviation must be positive", nameof(standardDeviation));
            }
            if (remainingSlots <= 0)
            {
                throw new ArgumentException("Cannot create an open session with no remaining slots", nameof(remainingSlots));
            }

            _lobby = lobby;
            _lobbyId = lobbyId;
            _sessionId = sessionId;
            _meanLatency = meanLatency;
            _standardDeviation = standardDeviation;
            _remainingSlots = remainingSlots;
            _tickets = new Dictionary<Guid, Lobby.Ticket>(_remainingSlots);

            Context.SetReceiveTimeout(heartbeat);

            Running();
        }

        private void IssueTickets()
        {
            Sender.Tell(new Lobby.Session(_lobbyId, _tickets.Values.ToList()), Self);
            _remainingSlots -= _tickets.Count;
            _tickets.Clear();
        }

        private void ReturnTickets()
        {
            foreach (var ticket in _tickets.Values)
            {
                _lobby.Tell(ticket, Self);
            }
            _tickets.Clear();
        }

        #region States

        // The session has space remaining and is accepting new tickets
        private void Running()
        {
            Receive<ReceiveTimeout>(_ => Become(Closing));
            Receive<Lobby.Ticket>(Handle);
            Receive<ClaimTickets>(_ =>
            {
                IssueTickets();

                // The session has been filled, shutting down
                if (_remainingSlots <= 0)
                {
                    _lobby.Tell(new Close(_sessionId), Self);
                    Become(Closing);
                }
            });
            Receive<Close>(_ =>
            {
                // Notify the lobby that the session is closing
                _lobby.Tell(new Close(_sessionId), Self);

                // Return session tickets to the lobby
                ReturnTickets();

                Become(Closing);
            });
            Receive<Lobby.CancelTicket>(command => _tickets.Remove(command.TicketId));
        }

        // The session has no space remaining and is wait for the last tickets to be collected
        private void Filled()
        {
            Receive<ReceiveTimeout>(_ => Become(Closing));
            Receive<Lobby.Ticket>(ticket => _lobby.Forward(ticket));
            Receive<ClaimTickets>(_ =>
            {
                IssueTickets();

                // We have no outstanding messages from the lobby
                if (_safeToClose)
                {
                    Self.Tell(PoisonPill.Instance, Self);
                }
                Become(Closing);
            });
            Receive<Close>(_ =>
            {
                // The lobby acknowledged the close and won't be sending us any more tickets
                if (_lobby.Equals(Sender))
                {
                    _safeToClose = true;
                }
                // Shutdown the session
                else
                {
                    _lobby.Tell(new Close(_sessionId), Self);
                    ReturnTickets();
                    Become(Closing);
                }
            });
            Receive<Lobby.CancelTicket>(command =>
            {
                _tickets.Remove(command.TicketId);

                if (_remainingSlots > _tickets.Count)
                {
                    _safeToClose = false;
                    _lobby.Tell(new Lobby.OpenSession(_sessionId, Self, _meanLatency, _standardDeviation), Self);
                    Become(Running);
                }
            });
        }

        // The session is shutting down and will no longer service requests
        private void Closing()
        {
            Receive<Lobby.Ticket>(ticket => _lobby.Forward(ticket));
            Receive<ClaimTickets>(command => Sender.Tell(new Lobby.Session(_lobbyId, new List<Lobby.Ticket>()), Self));
            Receive<Close>(_ => Self.Tell(PoisonPill.Instance, Self));
        }
        #endregion States

        #region Handlers

        private void Handle(Lobby.Ticket ticket)
        {
            if (ticket is null)
            {
                throw new ArgumentNullException(nameof(ticket));
            }

            if (_tickets.ContainsKey(ticket.Id))
            {
                _tickets[ticket.Id] = ticket;
            }
            else
            {
                _tickets.Add(ticket.Id, ticket);
            }

            if (_remainingSlots == _tickets.Count)
            {
                _lobby.Tell(new Close(_sessionId), Self);
                Become(Filled);
            }
        }

        #endregion Handlers

        #region Messages
        public class ClaimTickets
        {
            private ClaimTickets() { }

            public static ClaimTickets Instance { get; } = new();
        }

        public class Close
        {
            public Close(Guid sessionId)
            {
                SessionId = sessionId;
            }

            public Guid SessionId { get; }
        }
        #endregion Messages
    }
}