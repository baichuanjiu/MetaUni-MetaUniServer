using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using User.API.Controllers.Profile;
using User.API.DataContext.User;
using User.API.Entities.Friend;
using User.API.Entities.User;
using User.API.Filters;
using User.API.ReusableClass;

namespace User.API.Controllers.Friend
{
    public class BriefUserInformation
    {
        public BriefUserInformation(int UUID, string avatar, string nickname, DateTime updatedTime)
        {
            this.UUID = UUID;
            Avatar = avatar;
            Nickname = nickname;
            UpdatedTime = updatedTime;
        }

        public int UUID { get; set; }
        public string Avatar { get; set; }
        public string Nickname { get; set; }
        public DateTime UpdatedTime { get; set; }
    }

    public class SyncFriendshipsResponseData
    {
        public SyncFriendshipsResponseData(List<Friendship> friendshipsList, List<BriefUserInformation> briefUserInformationList, DateTime updatedTime)
        {
            FriendshipsList = friendshipsList;
            BriefUserInformationList = briefUserInformationList;
            UpdatedTime = updatedTime;
        }

        public List<Friendship> FriendshipsList { get; set; }
        public List<BriefUserInformation> BriefUserInformationList { get; set; }
        public DateTime UpdatedTime { get; set; }
    }

    public class SyncFriendsInformationResponseData
    {
        public SyncFriendsInformationResponseData(List<BriefUserInformation> dataList, DateTime updatedTime)
        {
            DataList = dataList;
            UpdatedTime = updatedTime;
        }
        public List<BriefUserInformation> DataList { get; set; }
        public DateTime UpdatedTime { get; set; }
    }


    [ApiController]
    [Route("/friendship")]
    [ServiceFilter(typeof(JWTAuthFilterService), IsReusable = true)]
    public class FriendshipController : Controller
    {
        //依赖注入
        private readonly UserContext _userContext;
        private readonly ILogger<ProfileController> _logger;

        public FriendshipController(UserContext userContext, ILogger<ProfileController> logger)
        {
            _userContext = userContext;
            _logger = logger;
        }

        [HttpGet("sync")]
        public async Task<IActionResult> SyncFriendships([FromQuery] DateTime updatedTime, [FromHeader] string JWT, [FromHeader] int UUID)
        {
            //实际查询时间（冗余10分钟）
            DateTime queryTime = updatedTime.AddMinutes(-10);

            //查找数据库
            DateTime currentUpdatedTime = _userContext.UserSyncTables.Select(table => new { table.UUID, UpdatedTime = table.UpdatedTimeForFriendships }).FirstOrDefaultAsync(table => table.UUID == UUID).Result!.UpdatedTime;
            List<Friendship> friendshipsList = await _userContext.Friendships.Where(ship => ship.UUID == UUID && ship.UpdatedTime > queryTime).ToListAsync();
            List<BriefUserInformation> briefUserInformationList = friendshipsList
                .Select(ship => ship.FriendId)
                .Join(_userContext.UserProfiles.Select(profile => new { profile.UUID, profile.Avatar, profile.Nickname, profile.UpdatedTime })
                , friendId => friendId
                , profile => profile.UUID
                , (ship, profile) => new BriefUserInformation(profile.UUID, profile.Avatar, profile.Nickname, profile.UpdatedTime))
                .ToList();

            SyncFriendshipsResponseData syncFriendshipsResponseData = new(friendshipsList, briefUserInformationList, currentUpdatedTime);
            ResponseT<SyncFriendshipsResponseData> getSyncDataSuccessed = new(0, "成功获取待同步的数据", syncFriendshipsResponseData);
            return Ok(getSyncDataSuccessed);
        }

        [HttpGet("friendsInformation/sync")]
        public async Task<IActionResult> SyncFriendsInformation([FromQuery] DateTime lastSyncTime, [FromHeader] string JWT, [FromHeader] int UUID)
        {
            //实际查询时间（冗余10分钟）
            DateTime queryTime = lastSyncTime.AddMinutes(-10);
            //查找从queryTime到currentTime内更新过的数据
            DateTime currentTime = DateTime.Now;

            //查找数据库
            List<BriefUserInformation> dataList = await _userContext.Friendships
                .Select(ship => new { ship.UUID, ship.FriendId })
                .Where(ship => ship.UUID == UUID)
                .Select(ship => ship.FriendId)
                .Join(_userContext.UserProfiles
                .Select(profile => new { profile.UUID, profile.Avatar, profile.Nickname, profile.UpdatedTime })
                .Where(profile => profile.UpdatedTime > queryTime),
                friendId => friendId, profile => profile.UUID, (friendId, profile) => new BriefUserInformation(profile.UUID, profile.Avatar, profile.Nickname, profile.UpdatedTime))
                .ToListAsync();

            UserSyncTable? userSyncTable = await _userContext.UserSyncTables.FirstOrDefaultAsync(table => table.UUID == UUID);
            userSyncTable!.LastSyncTimeForFriendsBriefInformation = currentTime;
            await _userContext.SaveChangesAsync();

            SyncFriendsInformationResponseData syncFriendsInformationResponseData = new(dataList, currentTime);
            ResponseT<SyncFriendsInformationResponseData> getSyncDataSuccessed = new(0, "成功获取待同步的数据", syncFriendsInformationResponseData);
            return Ok(getSyncDataSuccessed);
        }

    }
}
