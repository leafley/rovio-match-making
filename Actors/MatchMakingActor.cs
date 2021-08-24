using System;
using System.Collections.Generic;
using Akka.Actor;

namespace Rovio.MatchMaking.Actors
{
    public class MatchMakingActor : ReceiveActor
    {
        public static Props Props() => Akka.Actor.Props.Create(() => new MatchMakingActor());

        private Dictionary<Guid, Ticket> _tickets = new();

        public MatchMakingActor()
        {
            Receive<Ticket>(Handle);
            Receive<CancelTicket>(Handle);
        }

        #region Handlers

        private void Handle(Ticket command)
        {
            if (!_tickets.TryAdd(command.Id, command))
            {
                _tickets[command.Id] = command;
            }
        }

        private void Handle(CancelTicket command)
        {
            _tickets.Remove(command.TicketId);
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
        #endregion Commands
    }
}