using System;
using System.Collections.Generic;
using Akka.Actor;

namespace Rovio.MatchMaking.Actors
{
    public class DeliveryActor : ReceiveActor
    {
        public static Props Props() => Akka.Actor.Props.Create(() => new DeliveryActor());

        public Dictionary<Guid, IActorRef> _matchMakingLookup = new();

        public DeliveryActor()
        {
            Receive<MatchMakingActor.Ticket>(Handle);
            Receive<MatchMakingActor.CancelTicket>(Handle);
        }

        private void Handle(MatchMakingActor.CancelTicket command)
        {
            if (_matchMakingLookup.TryGetValue(command.GameId, out IActorRef matchMakingActor))
            {
                matchMakingActor.Forward(command);
            }
        }

        private void Handle(MatchMakingActor.Ticket command)
        {
            if (!_matchMakingLookup.TryGetValue(command.GameId, out IActorRef matchMakingActor))
            {
                matchMakingActor = Context.ActorOf(MatchMakingActor.Props(), command.GameId.ToString());
                _matchMakingLookup.Add(command.GameId, matchMakingActor);
            }
            matchMakingActor.Forward(command);
        }
    }
}