using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;

namespace Rovio.MatchMaking.Actors
{
    public class Lobby : ReceiveActor
    {
        public static Props Props() => Akka.Actor.Props.Create(() => new Lobby());

        private Dictionary<Guid, Ticket> _ticketLookup = new();

        public Lobby()
        {
            Receive<CancelTicket>(Handle);
            Receive<CreateSession>(Handle);
            Receive<Ticket>(Handle);
        }

        #region Handlers

        private void Handle(CreateSession command)
        {
            // Not enough tickets to create a session
            if (_ticketLookup.Count < command.MinPlayerCount)
            {
                Context.Sender.Tell(new Session(command.LobbyId), Self);
            }
            // Not enough tickets to calculate mean, but enough to create a session
            else if (_ticketLookup.Count < 2)
            {
                Context.Sender.Tell(new Session(command.LobbyId, _ticketLookup.Values.ToList()), Self);
                _ticketLookup.Clear();
            }

            // Calculate the mean using Welford's online algorithm
            double mean = 0d, m2 = 0d, delta1 = 0d, delta2 = 0d;
            int n = 0;
            foreach (var ticket in _ticketLookup.Values)
            {
                n++;
                delta1 = ticket.Latency - mean;
                mean += delta1 / n;
                // delta2 = ticket.Latency - mean;
                // m2 += delta1 * delta2;
            }

            // double variance = m2 / n;

            var now = NodaTime.SystemClock.Instance.GetCurrentInstant().ToUnixTimeTicks();
            double timeFactor = 1d / command.MaxWaitTime;

            var tickets = _ticketLookup.Values.ToList();
            tickets.Sort((x, y) =>
            {
                var result = GetAdjustedDeviation(x.Latency, mean, now - x.RegisteredAt, command.MaxWaitTime, timeFactor).CompareTo(
                    GetAdjustedDeviation(y.Latency, mean, now - y.RegisteredAt, command.MaxWaitTime, timeFactor));

                return result == 0
                    ? Math.Abs(mean - x.Latency).CompareTo(Math.Abs(mean - y.Latency))
                    : result;
            });

            var session = new Session(command.LobbyId, tickets.GetRange(0, Math.Min(command.MaxPlayerCount, tickets.Count)));
            foreach (var ticket in session.Tickets)
            {
                _ticketLookup.Remove(ticket.Id);
            }

            Sender.Tell(session, Self);
        }

        private void Handle(CancelTicket command)
        {
            _ticketLookup.Remove(command.TicketId);
        }

        private void Handle(Ticket command)
        {
            if (!_ticketLookup.TryAdd(command.Id, command))
            {
                _ticketLookup[command.Id] = command;
            }
        }

        #endregion Handlers

        private double GetAdjustedDeviation(double latency, double mean, long waitTime, long maxWaitTime, double timeFactor)
        {
            if (maxWaitTime <= waitTime)
            {
                return 0;
            }
            else if (waitTime <= 0)
            {
                return Math.Abs(mean - latency);
            }
            else
            {
                return Math.Abs(mean - latency) * timeFactor * (maxWaitTime - waitTime);
            }
        }

        #region Messages
        public class Session
        {
            public Guid LobbyId { get; }
            public List<Ticket> Tickets { get; }

            public Session(Guid lobbyId)
            {
                Tickets = new();
                LobbyId = lobbyId;
            }

            public Session(Guid lobbyId, List<Ticket> tickets)
            {
                LobbyId = lobbyId;
                Tickets = tickets;
            }
        }

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
            public CreateSession(Guid lobbyId, int minPlayerCount, int maxPlayerCount, long maxWaitTime)
            {
                LobbyId = lobbyId;
                MinPlayerCount = minPlayerCount;
                MaxPlayerCount = maxPlayerCount;
                MaxWaitTime = maxWaitTime;
            }

            public Guid LobbyId { get; }
            public int MinPlayerCount { get; }
            public int MaxPlayerCount { get; }
            public long MaxWaitTime { get; }
        }
        #endregion Messages
    }
}