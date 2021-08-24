using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Rovio.MatchMaking.Model;

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
        public IActionResult Post([FromBody]TicketRequest request)
        {
            var id = Guid.NewGuid();
            var ticket = new Actors.MatchMakingActor.Ticket(request.GameId, request.Latency);
            _deliveryActor.Tell(ticket);
            
            return Created($"{HttpContext.Request.Scheme}://{HttpContext.Request.Host.Value}{HttpContext.Request.Path}/{id}", ticket);
        }

        [HttpDelete("{id}")]
        public IActionResult Delete(Guid id)
        {
            return Accepted();
        }
    }
}
