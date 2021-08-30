using System;

namespace Rovio.MatchMaking.Models
{
    public class Session
    {
        public int MinTickets { get; set; }
        public int MaxTickets { get; set; }
        public int MaxWaitSeconds { get; set; }
    }
}