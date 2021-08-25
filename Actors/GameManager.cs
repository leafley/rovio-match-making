using System;
using System.Collections.Generic;
using Akka.Actor;

namespace Rovio.MatchMaking.Actors
{
    public class GameManager : ReceiveActor
    {
        public static Props Props() => Akka.Actor.Props.Create(() => new GameManager());

        public Dictionary<Guid, IActorRef> _matchMakingLookup = new();

        public GameManager()
        {
            Receive<Lobby.Ticket>(Handle);
            Receive<Lobby.CancelTicket>(Handle);
        }

        private void Handle(Lobby.CancelTicket command)
        {
            if (_matchMakingLookup.TryGetValue(command.GameId, out IActorRef matchMakingActor))
            {
                matchMakingActor.Forward(command);
            }
        }

        private void Handle(Lobby.Ticket command)
        {
            if (!_matchMakingLookup.TryGetValue(command.GameId, out IActorRef matchMakingActor))
            {
                matchMakingActor = Context.ActorOf(Lobby.Props(), command.GameId.ToString());
                _matchMakingLookup.Add(command.GameId, matchMakingActor);
            }
            matchMakingActor.Forward(command);
        }
    }
}