using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Rovio.MatchMaking.Actors;
using Rovio.MatchMaking.Models;
using Rovio.MatchMaking.Services;

namespace Rovio.MatchMaking.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class LobbiesController : ControllerBase
    {
        private readonly ILogger<LobbiesController> _logger;
        private readonly ActorService _actorService;

        public LobbiesController(ILogger<LobbiesController> logger, ActorService actorService)
        {
            _actorService = actorService;
            _logger = logger;
        }

        [HttpPost("{lobbyId}/tickets")]
        public IActionResult Post(Guid lobbyId, [FromBody] double latency)
        {
            var ticket = _actorService.QueueTicket(lobbyId, latency);

            return Created($"{HttpContext.Request.Scheme}://{HttpContext.Request.Host.Value}{HttpContext.Request.Path}/{ticket.Id}", ticket);
        }

        [HttpPut("{lobbyId}/tickets/{ticketId}")]
        public IActionResult Put(Guid lobbyId, Guid ticketId, [FromBody] double latency)
        {
            var ticket = _actorService.UpdateTicket(lobbyId, ticketId, latency);

            return Created($"{HttpContext.Request.Scheme}://{HttpContext.Request.Host.Value}{HttpContext.Request.Path}/{ticket.Id}", ticket);
        }

        [HttpDelete("{lobbyId}/tickets/{ticketId}")]
        public IActionResult Delete(Guid lobbyId, Guid ticketId)
        {
            _actorService.DequeueTicket(lobbyId, ticketId);
            return Accepted();
        }

        [HttpPost("{lobbyId}/sessions")]
        public async Task<IActionResult> PostSessionAsync(Guid lobbyId, [FromBody] Models.Session session)
        {
            var result = await _actorService.CreateSessionAsync(session, TimeSpan.FromSeconds(60));
            return Ok(result);
        }
    }
}
