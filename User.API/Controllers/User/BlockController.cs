using Microsoft.AspNetCore.Mvc;
using User.API.DataContext.User;
using User.API.Filters;
using User.API.Redis;
using User.API.ReusableClass;

namespace User.API.Controllers.User
{
    [ApiController]
    [Route("/user/block")]
    [ServiceFilter(typeof(JWTAuthFilterService), IsReusable = true)]
    public class BlockController : Controller
    {
        //依赖注入
        private readonly UserContext _userContext;
        private readonly RedisConnection _redisConnection;
        private readonly ILogger<BlockController> _logger;

        public BlockController(UserContext userContext, RedisConnection redisConnection, ILogger<BlockController> logger)
        {
            _userContext = userContext;
            _redisConnection = redisConnection;
            _logger = logger;
        }

        [HttpGet("check/{targetUUID}")]
        public IActionResult CheckBlockStatus([FromRoute] int targetUUID, [FromHeader] string JWT, [FromHeader] int UUID) 
        {
            var redis = _redisConnection.GetUserBlockListDatabase();
            return Ok(new ResponseT<bool>(0, "检查屏蔽状态成功", redis.SetContains($"{UUID}BlockList", targetUUID)));
        }

        [HttpPut("{targetUUID}")]
        public IActionResult ChangeBlockStatus([FromRoute] int targetUUID, [FromHeader] string JWT, [FromHeader] int UUID) 
        {
            // 安全性检查，不能屏蔽自己，不能对不存在的账号进行屏蔽
            var targetUser = _userContext.UserAccounts
                .Select(user => new{ user.UUID})
                .SingleOrDefault(user => user.UUID == targetUUID);

            if (targetUser == null)
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]正试图对不存在的用户[ {targetUUID} ]执行屏蔽或解除屏蔽操作", UUID,targetUUID);
                return Ok(new ResponseT<string>(2, "您无法对不存在的用户进行此操作"));
            }
            else if (targetUser.UUID == UUID) 
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]正试图对自己执行屏蔽或解除屏蔽操作", UUID);
                return Ok(new ResponseT<string>(3, "您无法对自己进行此操作"));
            }

            var redis = _redisConnection.GetUserBlockListDatabase();
            if (redis.SetContains($"{UUID}BlockList", targetUUID))
            {
                _ = redis.SetRemoveAsync($"{UUID}BlockList", targetUUID);
                return Ok(new ResponseT<bool>(0,"已解除对该用户的屏蔽",false));
            }
            else 
            {
                _ = redis.SetAddAsync($"{UUID}BlockList", targetUUID);
                return Ok(new ResponseT<bool>(0, "已屏蔽该用户", true));
            }
        }

        [HttpGet("blockAnyway/{targetUUID}")]
        public IActionResult BlockAnyway([FromRoute] int targetUUID, [FromHeader] string JWT, [FromHeader] int UUID)
        {
            // 安全性检查，不能屏蔽自己，不能对不存在的账号进行屏蔽
            var targetUser = _userContext.UserAccounts
                .Select(user => new { user.UUID })
                .SingleOrDefault(user => user.UUID == targetUUID);

            if (targetUser == null)
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]正试图对不存在的用户[ {targetUUID} ]执行屏蔽或解除屏蔽操作", UUID, targetUUID);
                return Ok(new ResponseT<string>(2, "您无法对不存在的用户进行此操作"));
            }
            else if (targetUser.UUID == UUID)
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]正试图对自己执行屏蔽或解除屏蔽操作", UUID);
                return Ok(new ResponseT<string>(3, "您无法对自己进行此操作"));
            }

            var redis = _redisConnection.GetUserBlockListDatabase();
            _ = redis.SetAddAsync($"{UUID}BlockList", targetUUID);
            return Ok(new ResponseT<bool>(0, "已屏蔽该用户", true));
        }
    }
}
