using System;
using System.Collections.Generic;
using Akka.Actor;

namespace Rovio.MatchMaking.Actors
{
    public class LobbyManager : ReceiveActor
    {
        public static Props Props() => Akka.Actor.Props.Create(() => new LobbyManager());

        public Dictionary<Guid, IActorRef> _lobbyLookup = new();

        public LobbyManager()
        {
            Receive<Lobby.CancelTicket>(Handle);
            Receive<Lobby.CreateSession>(Handle);
            Receive<Lobby.Ticket>(Handle);
        }

        private void Handle(Lobby.CreateSession command)
        {
            if (_lobbyLookup.TryGetValue(command.LobbyId, out IActorRef lobbyActor))
            {
                lobbyActor.Forward(command);
            }
        }

        private void Handle(Lobby.CancelTicket command)
        {
            if (_lobbyLookup.TryGetValue(command.LobbyId, out IActorRef lobbyActor))
            {
                lobbyActor.Forward(command);
            }
        }

        private void Handle(Lobby.Ticket command)
        {
            if (!_lobbyLookup.TryGetValue(command.LobbyId, out IActorRef lobbyActor))
            {
                lobbyActor = Context.ActorOf(Lobby.Props(), command.LobbyId.ToString());
                _lobbyLookup.Add(command.LobbyId, lobbyActor);
            }
            lobbyActor.Forward(command);
        }
    }
}