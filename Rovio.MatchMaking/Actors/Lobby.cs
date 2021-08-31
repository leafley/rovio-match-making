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
        private Dictionary<Guid, OpenSession> _openSessionsLookup = new();

        public Lobby()
        {
            Receive<CancelTicket>(Handle);
            Receive<CreateSession>(Handle);
            Receive<OpenSession>(Handle);
            Receive<Ticket>(Handle);

            Receive<Actors.Session.Close>(Handle);
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
            double radiansPerTick = Math.PI / (2 * command.MaxWaitTime);

            var tickets = _ticketLookup.Values
                .Select(n => new
                {
                    Ticket = n,
                    AdjustedDeviation = GetAdjustedDeviation(
                        n.Latency,
                        mean,
                        now - n.RegisteredAt,
                        command.MaxWaitTime,
                        radiansPerTick)
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

            var session = new Session(command.LobbyId, sessionTickets);
            Sender.Tell(session, Self);

            if (sessionCount < command.MaxPlayerCount)
            {
                var sessionActor = Context.ActorOf(
                    Actors.Session.Props(
                        Self,
                        command.LobbyId,
                        session.SessionId,
                        mean,
                        standardDeviation,
                        command.MaxPlayerCount - sessionCount,
                        command.Heartbeat),
                    session.SessionId.ToString());
                var openSession = new OpenSession(
                    session.SessionId,
                    sessionActor,
                    mean,
                    standardDeviation);
                _openSessionsLookup.Add(session.SessionId, openSession);
            }
        }

        private void Handle(CancelTicket command)
        {
            // If the ticket doesn't existing in the lobby it might be queued in an open session
            if (!_ticketLookup.Remove(command.TicketId))
            {
                foreach (var session in Context.GetChildren())
                {
                    session.Forward(command);
                }
            }
        }

        private void Handle(Ticket ticket)
        {
            if (_ticketLookup.ContainsKey(ticket.Id))
            {
                _ticketLookup[ticket.Id] = ticket;
            }
            else
            {
                foreach (var session in _openSessionsLookup.Values)
                {
                    if (Math.Abs(session.MeanLatency - ticket.Latency) <= session.MeanLatency)
                    {
                        session.Actor.Tell(ticket);
                        // Bail out if we find an open session that can handle the ticket
                        return;
                    }
                }

                // If we get here it means there are no open sessions that will take the ticket
                _ticketLookup.Add(ticket.Id, ticket);
            }
        }

        private void Handle(Actors.Session.Close command)
        {
            _openSessionsLookup.Remove(command.SessionId);
            Sender.Tell(command, Self);
        }

        private void Handle(Lobby.OpenSession command)
        {
            if (!_openSessionsLookup.ContainsKey(command.SessionId))
            {
                _openSessionsLookup.Add(command.SessionId, command);
            }
        }

        #endregion Handlers

        private double GetAdjustedDeviation(double latency, double mean, long waitTime, long maxWaitTime, double radiansPerTick)
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
                return Math.Abs(mean - latency) * Math.Cos(radiansPerTick * waitTime);
            }
        }

        #region Messages
        public class Session
        {
            public Guid LobbyId { get; }
            public Guid SessionId { get; }
            public List<Ticket> Tickets { get; }

            public Session(Guid lobbyId)
                : this(lobbyId, new())
            {
            }

            public Session(Guid lobbyId, List<Ticket> tickets)
            {
                LobbyId = lobbyId;
                SessionId = Guid.NewGuid();
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
            public CreateSession(
                Guid lobbyId,
                int minPlayerCount,
                int maxPlayerCount,
                long maxWaitTime,
                TimeSpan heartbeat)
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
                Heartbeat = heartbeat;
            }

            public Guid LobbyId { get; }
            public int MinPlayerCount { get; }
            public int MaxPlayerCount { get; }
            public long MaxWaitTime { get; }
            public TimeSpan Heartbeat { get; }
        }

        public class OpenSession
        {
            public Guid SessionId { get; }
            public IActorRef Actor { get; }
            public double MeanLatency { get; }
            public double StandardDeviation { get; }

            public OpenSession(Guid sessionId, IActorRef actor, double meanLatency, double standardDeviation)
            {
                if (sessionId == Guid.Empty)
                {
                    throw new ArgumentException("Invalid session ID", nameof(sessionId));
                }
                if (actor is null || actor == Akka.Actor.Nobody.Instance)
                {
                    throw new ArgumentException("Invalid session actor ref", nameof(actor));
                }
                if (meanLatency < 0)
                {
                    throw new ArgumentException("Mean latency must be positive", nameof(meanLatency));
                }
                if (standardDeviation < 0)
                {
                    throw new ArgumentException("Standard deviation must be positive", nameof(standardDeviation));
                }

                SessionId = sessionId;
                Actor = actor;
                MeanLatency = meanLatency;
                StandardDeviation = standardDeviation;
            }
        }
        #endregion Messages
    }
}