using System;

namespace Rovio.MatchMaking.Model
{
    public class TicketRequest
    {
        public Guid GameId { get; set; }
        public double Latency { get; set; }
    }
}