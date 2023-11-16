using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;
using Version.API.Filters;
using Version.API.Redis;
using Version.API.ReusableClass;

namespace Version.API.Controllers
{
    public class VersionInfo 
    {
        public VersionInfo(string version, string downloadUrl)
        {
            Version = version;
            DownloadUrl = downloadUrl;
        }

        public string Version { get; set; }
        public string DownloadUrl { get; set; }
    }

    [ApiController]
    [Route("/version")]
    [ServiceFilter(typeof(JWTAuthFilterService), IsReusable = true)]
    public class VersionController : Controller
    {
        private RedisConnection _redisConnection;
        private readonly ILogger<VersionController> _logger;

        public VersionController(RedisConnection redisConnection, ILogger<VersionController> logger)
        {
            _redisConnection = redisConnection;
            _logger = logger;
        }

        [HttpGet("latest")]
        public IActionResult GetLatestVersionInfo([FromHeader] string JWT, [FromHeader] int UUID) 
        {
            IDatabase versionRedis = _redisConnection.GetVersionDatabase();
            string? latestVersion = versionRedis.StringGet("latestVersion");
            latestVersion ??= "1.0.0";
            string? downloadUrl = versionRedis.StringGet("downloadUrl");
            downloadUrl ??= "";
            return Ok(new ResponseT<VersionInfo>(0, "获取成功",new(latestVersion, downloadUrl)));
        }
    }
}
