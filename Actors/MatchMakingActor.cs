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
            Receive<Guid>(Handle);
        }

        #region Handlers

        private void Handle(Ticket command)
        {
            if (!_tickets.TryAdd(command.Id, command))
            {
                _tickets[command.Id] = command;
            }
        }

        private void Handle(Guid command)
        {
            _tickets.Remove(command);
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
        #endregion Commands
    }
}