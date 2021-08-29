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
    public class SessionsController : ControllerBase
    {
        private readonly ILogger<SessionsController> _logger;
        private readonly ActorService _actorService;

        public SessionsController(ILogger<SessionsController> logger, ActorService actorService)
        {
            _actorService = actorService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> PostAsync([FromBody] Models.Session session)
        {
            var result = await _actorService.CreateSessionAsync(session, TimeSpan.FromSeconds(60));

            return Ok(result);
        }
    }
}
