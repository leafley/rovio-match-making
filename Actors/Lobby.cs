using System;
using System.Collections.Generic;
using Akka.Actor;

namespace Rovio.MatchMaking.Actors
{
    public class Lobby : ReceiveActor
    {
        public static Props Props() => Akka.Actor.Props.Create(() => new Lobby());

        private Dictionary<Guid, Ticket> _tickets = new();

        public Lobby()
        {
            Receive<CancelTicket>(Handle);
            Receive<CreateSession>(Handle);
            Receive<Ticket>(Handle);
        }

        #region Handlers

        private void Handle(CreateSession command)
        {
            if (_tickets.Count < command.MinPlayerCount)
            {
                Context.Sender.Tell("test value", Self);
            }
        }

        private void Handle(CancelTicket command)
        {
            _tickets.Remove(command.TicketId);
        }

        private void Handle(Ticket command)
        {
            if (!_tickets.TryAdd(command.Id, command))
            {
                _tickets[command.Id] = command;
            }
        }

        #endregion Handlers

        #region Commands
        public class Ticket
        {
            public Guid GameId { get; }
            public Guid Id { get; }
            public double Latency { get; }
            public long RegisteredAt { get; }

            public Ticket(Guid gameId, double latency)
            {
                GameId = gameId;
                Id = Guid.NewGuid();
                Latency = latency;
                RegisteredAt = NodaTime.SystemClock.Instance.GetCurrentInstant().ToUnixTimeTicks();
            }
        }

        public class CancelTicket
        {
            public CancelTicket(Guid gameId, Guid ticketId)
            {
                GameId = gameId;
                TicketId = ticketId;
            }

            public Guid GameId { get; }
            public Guid TicketId { get; }
        }

        public class CreateSession
        {
            public CreateSession(Guid gameId, uint minPlayerCount, uint maxPlayerCount)
            {
                GameId = gameId;
                MinPlayerCount = minPlayerCount;
                MaxPlayerCount = maxPlayerCount;
            }

            public Guid GameId { get; }
            public uint MinPlayerCount { get; }
            public uint MaxPlayerCount { get; }
        }
        #endregion Commands
    }
}