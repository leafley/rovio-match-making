using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Rovio.MatchMaking.Services;

namespace Rovio.MatchMaking.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TestController : ControllerBase
    {
        private readonly ILogger<TestController> _logger;
        private readonly ActorService _actorService;

        public TestController(ILogger<TestController> logger, ActorService actorService)
        {
            _actorService = actorService;
            _logger = logger;
        }

        //------------------ These endpoints are for testing and convenience only -----------------------------
        [HttpPost("{lobbyId}/bulk")]
        public IActionResult Bulk(Guid lobbyId, [FromBody] int count)
        {
            var r = new Random();

            for (int i = 0; i < count; i++)
            {
                _actorService.QueueTicket(lobbyId, r.Next(1, 1000));
            }

            return Ok();
        }
    }
}
