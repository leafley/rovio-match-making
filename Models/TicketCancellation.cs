using System;

namespace Rovio.MatchMaking.Models
{
    public class TicketCancellation
    {
        public Guid GameId { get; set; }
        public Guid TicketId { get; set; }
    }
}