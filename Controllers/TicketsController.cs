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

        public TicketsController(ILogger<TicketsController> logger, ActorSystem actorSystem)
        {
            _logger = logger;
        }

        [HttpPost]
        public IActionResult Post([FromBody]Ticket ticket)
        {
            var id = Guid.NewGuid();
            return Created($"{HttpContext.Request.Scheme}://{HttpContext.Request.Host.Value}{HttpContext.Request.Path}/{id}", new { Id = id, Game = ticket.Game });
        }

        [HttpDelete("{id}")]
        public IActionResult Delete(Guid id)
        {
            return Accepted();
        }
    }
}
