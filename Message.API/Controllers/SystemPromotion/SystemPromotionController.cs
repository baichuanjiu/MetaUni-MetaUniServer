using Message.API.DataContext.Message;
using Message.API.Filters;
using Message.API.ReusableClass;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using User.API.DataContext.User;
using User.API.Entities.User;

namespace Message.API.Controllers.SystemPromotion
{
    public class SyncSystemPromotionInformationResponseData
    {
        public SyncSystemPromotionInformationResponseData(List<Entities.SystemPromotion.SystemPromotion> dataList, DateTime updatedTime)
        {
            DataList = dataList;
            UpdatedTime = updatedTime;
        }

        public List<Entities.SystemPromotion.SystemPromotion> DataList { get; set; }
        public DateTime UpdatedTime { get; set; }
    }

    [ApiController]
    [Route("/systemPromotion")]
    [ServiceFilter(typeof(JWTAuthFilterService), IsReusable = true)]
    public class SystemPromotionController : Controller
    {
        //依赖注入
        private readonly MessageContext _messageContext;
        private readonly UserContext _userContext;
        private readonly IDistributedCache _distributedCache;
        private readonly ILogger<SystemPromotionController> _logger;

        public SystemPromotionController(MessageContext messageContext, UserContext userContext, IDistributedCache distributedCache, ILogger<SystemPromotionController> logger)
        {
            _messageContext = messageContext;
            _userContext = userContext;
            _distributedCache = distributedCache;
            _logger = logger;
        }

        [HttpGet("{queryUUID}")]
        public async Task<IActionResult> GetSystemPromotionInformation(int queryUUID, [FromHeader] string JWT, [FromHeader] int UUID)
        {
            //优先查找Redis缓存中的数据
            string? systemPromotionJson = _distributedCache.GetString(queryUUID.ToString() + "SystemPromotion");
            if (systemPromotionJson != null)
            {
                Entities.SystemPromotion.SystemPromotion systemPromotion = JsonSerializer.Deserialize<Entities.SystemPromotion.SystemPromotion>(systemPromotionJson)!;
                ResponseT<Entities.SystemPromotion.SystemPromotion> getInformationSucceed = new(0, "获取成功", systemPromotion);
                return Ok(getInformationSucceed);
            }
            else
            {
                //查找数据库
                var targetInformation = await _messageContext
                    .SystemPromotions
                    .FindAsync(queryUUID);
                if (targetInformation == null)
                {
                    _logger.LogWarning("Warning：用户[ {UUID} ]企图查询一个不存在的系统推送[ {queryUUID} ]的信息。", UUID, queryUUID);
                    ResponseT<string> getInformationFailed = new(2, "没有找到目标系统推送的信息");
                    return Ok(getInformationFailed);
                }

                //往Redis里做缓存
                //设置缓存在Redis中的过期时间
                DistributedCacheEntryOptions options = new DistributedCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromMinutes(15));
                options.SetSlidingExpiration(TimeSpan.FromMinutes(3));
                //将数据存入Redis
                _ = _distributedCache.SetStringAsync(queryUUID.ToString() + "SystemPromotion", JsonSerializer.Serialize(targetInformation), options);

                ResponseT<Entities.SystemPromotion.SystemPromotion> getInformationSucceed = new(0, "获取成功", targetInformation);
                return Ok(getInformationSucceed);
            }
        }

        [HttpGet("sync")]
        public async Task<IActionResult> SyncSystemPromotionInformation([FromQuery] DateTime lastSyncTime, [FromQuery] List<int> queryList, [FromHeader] string JWT, [FromHeader] int UUID)
        {
            //实际查询时间（冗余10分钟）
            DateTime queryTime = lastSyncTime.AddMinutes(-10);
            //查找从queryTime到currentTime内更新过的数据
            DateTime currentTime = DateTime.Now;

            //查找数据库
            List<Entities.SystemPromotion.SystemPromotion> dataList = await _messageContext
                .SystemPromotions
                .Where(info => queryList.Contains(info.UUID) && info.UpdatedTime > queryTime)
                .ToListAsync();

            UserSyncTable? userSyncTable = await _userContext
                .UserSyncTables
                .FirstOrDefaultAsync(table => table.UUID == UUID);
            userSyncTable!.LastSyncTimeForSystemPromotionInformation = currentTime;
            await _userContext.SaveChangesAsync();

            SyncSystemPromotionInformationResponseData syncSystemPromotionInformationResponseData = new(dataList, currentTime);
            ResponseT<SyncSystemPromotionInformationResponseData> getSyncDataSucceed = new(0, "成功获取待同步的数据", syncSystemPromotionInformationResponseData);
            return Ok(getSyncDataSucceed);
        }
    }
}
