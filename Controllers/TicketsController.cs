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
    public class TicketsController : ControllerBase
    {
        private readonly ILogger<TicketsController> _logger;
        private readonly IActorRef _deliveryActor;

        public TicketsController(ILogger<TicketsController> logger, IActorRef deliveryActor)
        {
            _logger = logger;
            _deliveryActor = deliveryActor;
        }

        [HttpPost]
        public IActionResult Post([FromBody] TicketRequest request)
        {
            var ticket = new MatchMakingActor.Ticket(request.GameId, request.Latency);
            _deliveryActor.Tell(ticket);

            return Created($"{HttpContext.Request.Scheme}://{HttpContext.Request.Host.Value}{HttpContext.Request.Path}/{ticket.Id}", ticket);
        }

        [HttpDelete]
        public IActionResult Delete([FromBody] TicketCancellation cancellation)
        {
            _deliveryActor.Tell(new MatchMakingActor.CancelTicket(cancellation.GameId, cancellation.TicketId));
            return Accepted();
        }
    }
}
