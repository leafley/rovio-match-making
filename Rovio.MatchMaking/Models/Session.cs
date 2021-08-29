using System;

namespace Rovio.MatchMaking.Models
{
    public class Session
    {
        public Guid LobbyId { get; set; }
        public int MinTickets { get; set; }
        public int MaxTickets { get; set; }
        public int MaxWaitSeconds { get; set; }
    }
}