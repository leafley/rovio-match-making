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
    public class LobbiesController : ControllerBase
    {
        private readonly ILogger<LobbiesController> _logger;
        private readonly IActorRef _deliveryActor;

        public LobbiesController(ILogger<LobbiesController> logger, IActorRef deliveryActor)
        {
            _logger = logger;
            _deliveryActor = deliveryActor;
        }

        [HttpPost("{id}/tickets")]
        public IActionResult Post(Guid id, [FromBody] double latency)
        {
            var ticket = new Lobby.Ticket(id, latency);
            _deliveryActor.Tell(ticket);

            return Created($"{HttpContext.Request.Scheme}://{HttpContext.Request.Host.Value}{HttpContext.Request.Path}/{ticket.Id}", ticket);
        }

        [HttpDelete("{gameId}/tickets/{ticketId}")]
        public IActionResult Delete(Guid gameId, Guid ticketId)
        {
            _deliveryActor.Tell(new Lobby.CancelTicket(gameId, ticketId));
            return Accepted();
        }

        [HttpPost("{id}/bulk/{count}")]
        public IActionResult Bulk(Guid id, int count)
        {
            var r = new Random();

            for (int i = 0; i < count; i++)
            {
                _deliveryActor.Tell(new Lobby.Ticket(id, r.Next(1, 1000)));
            }

            return Ok();
        }
    }
}
