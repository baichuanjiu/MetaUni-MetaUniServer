using Microsoft.AspNetCore.Mvc;
using MiniApp.API.Filters;
using MiniApp.API.MongoDBServices.MiniApp;

namespace MiniApp.API.Controllers.MiniApp
{
    [ApiController]
    [Route("/miniApp")]
    [ServiceFilter(typeof(JWTAuthFilterService), IsReusable = true)]
    public class MiniAppController : Controller
    {
        private readonly MiniAppService _miniAppService;
        private readonly ILogger<MiniAppController> _logger;

        public MiniAppController(MiniAppService miniAppService, ILogger<MiniAppController> logger)
        {
            _miniAppService = miniAppService;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult GetMiniApps([FromHeader] int UUID, [FromHeader] string JWT)
        {
            IEnumerable<object> apps = _miniAppService.GetMiniApps();
            return Ok(apps);
        }
    }
}
