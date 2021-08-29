using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;

namespace Rovio.MatchMaking.Actors
{
    public class Session : ReceiveActor
    {
        public static Props Props(IActorRef lobby, Guid lobbyId, int spaceRemaining) => Akka.Actor.Props.Create(() => new Session(lobby, lobbyId, spaceRemaining));

        private readonly Dictionary<Guid, Lobby.Ticket> _tickets;
        private readonly IActorRef _lobby;
        private readonly Guid _lobbyId;

        private bool _safeToClose;
        private int _spaceRemaining;

        public Session(IActorRef lobby, Guid lobbyId, int spaceRemaining)
        {
            if (lobby is null || lobby == Akka.Actor.Nobody.Instance)
            {
                throw new ArgumentException("Invalid lobby actor ref", nameof(lobby));
            }
            if (lobbyId == Guid.Empty)
            {
                throw new ArgumentException("Invalid lobby ID", nameof(lobbyId));
            }
            if (spaceRemaining < 1)
            {
                throw new ArgumentException("The session must have space remaining", nameof(spaceRemaining));
            }

            _lobby = lobby;
            _lobbyId = lobbyId;
            _spaceRemaining = spaceRemaining;
            _tickets = new Dictionary<Guid, Lobby.Ticket>(_spaceRemaining);

            Running();
        }

        private void IssueTickets()
        {
            Sender.Tell(new Lobby.Session(_lobbyId, _tickets.Values.ToList()), Self);
            _spaceRemaining -= _tickets.Count;
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
            Receive<AddTicket>(Handle);
            Receive<ClaimTickets>(_ =>
            {
                IssueTickets();

                // The session has been filled, shutting down
                if (_spaceRemaining <= 0)
                {
                    _lobby.Tell(Close.Instance, Self);
                    Become(Closing);
                }
            });
            Receive<Close>(_ =>
            {
                // Notify the lobby that the session is closing
                _lobby.Tell(Close.Instance, Self);

                // Return session tickets to the lobby
                ReturnTickets();
                
                Become(Closing);
            });
        }

        // The session has no space remaining and is wait for the last tickets to be collected
        private void Filled()
        {
            Receive<AddTicket>(command => _lobby.Forward(command.Ticket));
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
                    _lobby.Tell(Close.Instance, Self);
                    ReturnTickets();
                    Become(Closing);
                }
            });
        }

        // The session is shutting down and will no longer service requests
        private void Closing()
        {
            Receive<AddTicket>(command => _lobby.Forward(command.Ticket));
            Receive<ClaimTickets>(command => Sender.Tell(new Lobby.Session(_lobbyId, new List<Lobby.Ticket>()), Self));
            Receive<Close>(_ => Self.Tell(PoisonPill.Instance, Self));
        }
        #endregion States

        #region Handlers

        private void Handle(AddTicket command)
        {
            if (command is null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            if (_tickets.ContainsKey(command.Ticket.Id))
            {
                _tickets[command.Ticket.Id] = command.Ticket;
            }
            else
            {
                _tickets.Add(command.Ticket.Id, command.Ticket);
            }

            if (_spaceRemaining == _tickets.Count)
            {
                _lobby.Tell(Close.Instance, Self);
                Become(Filled);
            }
        }

        #endregion Handlers

        #region Messages
        public class AddTicket
        {
            public Lobby.Ticket Ticket { get; }

            public AddTicket(Lobby.Ticket ticket)
            {
                this.Ticket = ticket ?? throw new ArgumentNullException(nameof(ticket));
            }
        }

        public class ClaimTickets
        {
            private ClaimTickets() { }

            public static ClaimTickets Instance { get; } = new();
        }

        public class Close
        {
            private Close() { }

            public static Close Instance { get; } = new();
        }
        #endregion Messages
    }
}