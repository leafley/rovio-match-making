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
                delta2 = ticket.Latency - mean;
                m2 += delta1 * delta2;
            }

            double standardDeviation = Math.Sqrt(m2 / n);

            var now = NodaTime.SystemClock.Instance.GetCurrentInstant().ToUnixTimeTicks();
            double degreesPerTick = 90d / command.MaxWaitTime;

            var tickets = _ticketLookup.Values
                .Select(n => new
                {
                    Ticket = n,
                    AdjustedDeviation = GetAdjustedDeviation(
                        n.Latency,
                        mean,
                        now - n.RegisteredAt,
                        command.MaxWaitTime,
                        degreesPerTick)
                })
                .Where(n => Math.Abs(mean - n.AdjustedDeviation) <= standardDeviation)
                .ToList();
            tickets.Sort((x, y) =>
            {
                var result = x.AdjustedDeviation.CompareTo(y.AdjustedDeviation);

                return result == 0
                    ? Math.Abs(mean - x.Ticket.Latency).CompareTo(Math.Abs(mean - y.Ticket.Latency))
                    : result;
            });

            var sessionCount = Math.Min(command.MaxPlayerCount, tickets.Count);
            var sessionTickets = new List<Ticket>(sessionCount);
            for (int i = 0; i < sessionCount; i++)
            {
                _ticketLookup.Remove(tickets[i].Ticket.Id, out Ticket ticket);
                sessionTickets.Add(ticket);
            }

            Sender.Tell(new Session(command.LobbyId, sessionTickets), Self);
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

        private double GetAdjustedDeviation(double latency, double mean, long waitTime, long maxWaitTime, double degreesPerTick)
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
                return Math.Abs(mean - latency) * Math.Cos(degreesPerTick * waitTime);
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
            public Guid LobbyId { get; }
            public Guid Id { get; }
            public double Latency { get; }
            public long RegisteredAt { get; }

            public Ticket(Guid lobbyId, double latency)
                : this(lobbyId, Guid.NewGuid(), latency)
            {
            }

            public Ticket(Guid lobbyId, Guid ticketId, double latency)
                : this()
            {
                if (lobbyId == Guid.Empty)
                {
                    throw new ArgumentException("Invalid lobby ID", nameof(lobbyId));
                }
                if (ticketId == Guid.Empty)
                {
                    throw new ArgumentException("Invalid ticket ID", nameof(ticketId));
                }
                if (latency < 0)
                {
                    throw new ArgumentException("Latency cannot be less than zero", nameof(latency));
                }

                LobbyId = lobbyId;
                Id = ticketId;
                Latency = latency;
            }

            private Ticket()
            {
                RegisteredAt = NodaTime.SystemClock.Instance.GetCurrentInstant().ToUnixTimeTicks();
            }
        }

        public class CancelTicket
        {
            public CancelTicket(Guid lobbyId, Guid ticketId)
            {
                if (lobbyId == Guid.Empty)
                {
                    throw new ArgumentException("Invalid lobby ID", nameof(lobbyId));
                }
                if (ticketId == Guid.Empty)
                {
                    throw new ArgumentException("Invalid ticket ID", nameof(ticketId));
                }

                LobbyId = lobbyId;
                TicketId = ticketId;
            }

            public Guid LobbyId { get; }
            public Guid TicketId { get; }
        }

        public class CreateSession
        {
            public CreateSession(Guid lobbyId, int minPlayerCount, int maxPlayerCount, long maxWaitTime)
            {
                if (lobbyId == Guid.Empty)
                {
                    throw new ArgumentException("Invalid lobby ID", nameof(lobbyId));
                }
                if (minPlayerCount < 1)
                {
                    throw new ArgumentException("The minimum player count must be greater than 0", nameof(minPlayerCount));
                }
                if (maxPlayerCount < minPlayerCount)
                {
                    throw new ArgumentException("The maximum player count cannot be less than the minimum player count", nameof(maxPlayerCount));
                }
                if (maxWaitTime < 0)
                {
                    throw new ArgumentException("The maximum wait time cannot be less than 0", nameof(maxWaitTime));
                }

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