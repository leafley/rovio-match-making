using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Rovio.MatchMaking.Actors;
using Rovio.MatchMaking.Models;

namespace Rovio.MatchMaking.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class GamesController : ControllerBase
    {
        private readonly ILogger<GamesController> _logger;
        private readonly IActorRef _deliveryActor;

        public GamesController(ILogger<GamesController> logger, IActorRef deliveryActor)
        {
            _logger = logger;
            _deliveryActor = deliveryActor;
        }

        [HttpPost("{id}/tickets")]
        public IActionResult Post(Guid gameId, [FromBody] double latency)
        {
            var ticket = new Lobby.Ticket(gameId, latency);
            _deliveryActor.Tell(ticket);

            return Created($"{HttpContext.Request.Scheme}://{HttpContext.Request.Host.Value}{HttpContext.Request.Path}/{ticket.Id}", ticket);
        }

        [HttpDelete("{gameId}/tickets/{ticketId}")]
        public IActionResult Delete(Guid gameId, Guid ticketId)
        {
            _deliveryActor.Tell(new Lobby.CancelTicket(gameId, ticketId));
            return Accepted();
        }
    }
}
