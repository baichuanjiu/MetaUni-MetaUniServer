using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using User.API.Controllers.Profile;
using User.API.DataContext.User;
using User.API.Entities.Friend;
using User.API.Filters;
using User.API.ReusableClass;

namespace User.API.Controllers.Friend
{
    public class SyncFriendsGroupsResponseData
    {
        public SyncFriendsGroupsResponseData(List<FriendsGroup> dataList, DateTime updatedTime)
        {
            DataList = dataList;
            UpdatedTime = updatedTime;
        }

        public List<FriendsGroup> DataList { get; set; }
        public DateTime UpdatedTime { get; set; }
    }

    [ApiController]
    [Route("/friendGroup")]
    [ServiceFilter(typeof(JWTAuthFilterService), IsReusable = true)]
    public class FriendGroupController : Controller
    {
        //依赖注入
        private readonly UserContext _userContext;
        private readonly ILogger<FriendGroupController> _logger;

        public FriendGroupController(UserContext userContext, ILogger<FriendGroupController> logger)
        {
            _userContext = userContext;
            _logger = logger;
        }

        [HttpGet("sync")]
        public async Task<IActionResult> SyncFriendsGroups([FromQuery] DateTime updatedTime, [FromHeader] string JWT, [FromHeader] int UUID)
        {
            //实际查询时间（冗余10分钟）
            DateTime queryTime = updatedTime.AddMinutes(-10);

            //查找数据库
            DateTime currentUpdatedTime = _userContext.UserSyncTables.Select(table => new { table.UUID, UpdatedTime = table.UpdatedTimeForFriendsGroups }).FirstOrDefaultAsync(table => table.UUID == UUID).Result!.UpdatedTime;
            List<FriendsGroup> dataList = await _userContext.FriendsGroups.Where(group => group.UUID == UUID && group.UpdatedTime > queryTime).ToListAsync();

            SyncFriendsGroupsResponseData syncFriendsGroupsResponseData = new(dataList, currentUpdatedTime);
            ResponseT<SyncFriendsGroupsResponseData> getSyncDataSucceed = new(0, "成功获取待同步的数据", syncFriendsGroupsResponseData);
            return Ok(getSyncDataSucceed);
        }
    }
}
