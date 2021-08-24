using System;

namespace Rovio.MatchMaking.Models
{
    public class TicketRequest
    {
        public Guid GameId { get; set; }
        public double Latency { get; set; }
    }
}