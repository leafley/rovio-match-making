using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Rovio.MatchMaking.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class SessionsController : ControllerBase
    {
        private readonly ILogger<SessionsController> _logger;
        private readonly IActorRef _deliveryActor;

        public SessionsController(ILogger<SessionsController> logger, IActorRef deliveryActor)
        {
            _logger = logger;
            _deliveryActor = deliveryActor;
        }

        [HttpPost]
        public async Task<IActionResult> PostAsync([FromBody] Models.Session session)
        {
            var result = await _deliveryActor.Ask(
                new Actors.Lobby.CreateSession(
                    session.LobbyId,
                    session.MinTickets,
                    session.MaxTickets,
                    NodaTime.Duration.FromSeconds(session.MaxWaitSeconds).BclCompatibleTicks),
                TimeSpan.FromSeconds(60));

            return Ok(result);
        }
    }
}
