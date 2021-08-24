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

        public SessionsController(ILogger<SessionsController> logger, ActorSystem actorSystem)
        {
            _logger = logger;
        }

        [HttpPost]
        public IActionResult Post([FromBody]Guid game)
        {
            var r = new Random();
            var userCount = r.Next(999);

            return Ok(new
            {
                Game = game,
                Tickets = Enumerable.Range(0, userCount)
                    .Select(n => new
                    {
                        Id = Guid.NewGuid(),
                        Latency = r.NextDouble() * 1000,
                        QueueTime = r.NextDouble() * 300
                    })
            });
        }
    }
}
