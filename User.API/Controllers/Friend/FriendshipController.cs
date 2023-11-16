using Azure.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System;
using System.Text.RegularExpressions;
using User.API.DataContext.User;
using User.API.Entities.Friend;
using User.API.Entities.User;
using User.API.Filters;
using User.API.RabbitMQ;
using User.API.Redis;
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

    public class AddFriendForm
    {
        public AddFriendForm(int targetId, string? message, string? remark, int friendsGroupId)
        {
            TargetId = targetId;
            Message = message;
            Remark = remark;
            FriendsGroupId = friendsGroupId;
        }

        public int TargetId { get; set; }
        public string? Message { get; set; }
        public string? Remark { get; set; }
        public int FriendsGroupId { get; set; }
    }

    public class AddFriendRequestDataForReceiver
    {
        public AddFriendRequestDataForReceiver(int id, BriefUserInformation sender, string? message)
        {
            Id = id;
            Sender = sender;
            Message = message;
        }

        public int Id { get; set; } //添加好友请求的Id
        public BriefUserInformation Sender { get; set; } //请求者的个人信息
        public string? Message { get; set; } //请求者填写的验证信息
    }

    public class GetAddFriendRequestResponseData
    {
        public GetAddFriendRequestResponseData(List<AddFriendRequestDataForReceiver> dataList)
        {
            DataList = dataList;
        }

        public List<AddFriendRequestDataForReceiver> DataList { get; set; }
    }

    public class AgreeAddFriendForm
    {
        public AgreeAddFriendForm(string? remark, int friendsGroupId)
        {
            Remark = remark;
            FriendsGroupId = friendsGroupId;
        }

        public string? Remark { get; set; }
        public int FriendsGroupId { get; set; }
    }

    public class AgreeAddFriendRequestResponseData
    {
        public AgreeAddFriendRequestResponseData(Friendship friendship)
        {
            Friendship = friendship;
        }

        public Friendship Friendship { get; set; }
    }

    public class EditRemarkRequestData 
    {
        public EditRemarkRequestData(int friendshipId, string? remark)
        {
            FriendshipId = friendshipId;
            Remark = remark;
        }

        public int FriendshipId { get; set; }
        public string? Remark { get; set;}
    }

    public class EditRemarkResponseData
    {
        public EditRemarkResponseData(int friendshipId, string? remark, DateTime updatedTime)
        {
            FriendshipId = friendshipId;
            Remark = remark;
            UpdatedTime = updatedTime;
        }

        public int FriendshipId { get; set; }
        public string? Remark { get; set; }
        public DateTime UpdatedTime { get; set; }
    }

    public class DeleteFriendshipResponseData
    {
        public DeleteFriendshipResponseData(Friendship friendship)
        {
            Friendship = friendship;
        }

        public Friendship Friendship { get; set; }
    }

    [ApiController]
    [Route("/friendship")]
    [ServiceFilter(typeof(JWTAuthFilterService), IsReusable = true)]
    public class FriendshipController : Controller
    {
        //依赖注入
        private readonly UserContext _userContext;
        private readonly ILogger<FriendshipController> _logger;
        private readonly RedisConnection _redisConnection;
        private readonly IDistributedCache _distributedCache;
        private readonly IMessagePublisher _messagePublisher;

        public FriendshipController(UserContext userContext, ILogger<FriendshipController> logger, RedisConnection redisConnection, IDistributedCache distributedCache, IMessagePublisher messagePublisher)
        {
            _userContext = userContext;
            _logger = logger;
            _redisConnection = redisConnection;
            _distributedCache = distributedCache;
            _messagePublisher = messagePublisher;
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
            ResponseT<SyncFriendshipsResponseData> getSyncDataSucceed = new(0, "成功获取待同步的数据", syncFriendshipsResponseData);
            return Ok(getSyncDataSucceed);
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
                .Select(ship => new { ship.UUID, ship.FriendId,ship.IsDeleted })
                .Where(ship => ship.UUID == UUID && !ship.IsDeleted)
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
            ResponseT<SyncFriendsInformationResponseData> getSyncDataSucceed = new(0, "成功获取待同步的数据", syncFriendsInformationResponseData);
            return Ok(getSyncDataSucceed);
        }

        [HttpPost("request")]
        public async Task<IActionResult> SendAddFriendRequest([FromBody] AddFriendForm addFriendForm, [FromHeader] string JWT, [FromHeader] int UUID)
        {
            //首先进行一系列判定
            //已经是好友的，不允许发送好友请求
            //已经发送过请求的，覆写该请求，确保幂等性
            //不是好友且没发送过请求的，正常新建请求即可
            //另：记得做安全性检查，比如addFriendForm中的TargetId是否有效，以及传入的FriendsGroupId是否是请求者自己的好友分组

            //安全性检查：TargetId是否有效
            var account = await _userContext.UserAccounts.Select(account => new { account.UUID }).FirstOrDefaultAsync(account => account.UUID == addFriendForm.TargetId);
            if (account == null)
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]正在尝试向不存在的用户[ {targetId} ]发送添加好友请求，可能原因为用户正在尝试绕过前端进行操作。", UUID, addFriendForm.TargetId);
                return Ok(new ResponseT<string>(2, "您正在尝试添加不存在的用户为好友"));
            }

            if (addFriendForm.Remark != null)
            {
                //安全性检查：addFriendForm中的Remark是否符合要求
                var check = Regex.Split(addFriendForm.Remark, " +").ToList();
                check.RemoveAll(key => key == "");

                if (check.Count == 0)
                {
                    _logger.LogWarning("Warning：用户[ {UUID} ]在向[ {targetId} ]发送添加好友请求时尝试使用不符合要求的备注[ {remark} ]", UUID, addFriendForm.TargetId, addFriendForm.Remark);
                    return Ok(new ResponseT<string>(3, "备注不可为空"));
                }

                if (addFriendForm.Remark.Length > 15)
                {
                    _logger.LogWarning("Warning：用户[ {UUID} ]在向[ {targetId} ]发送添加好友请求时尝试使用超过长度限制的备注[ {remark} ]", UUID, addFriendForm.TargetId, addFriendForm.Remark);
                    return Ok(new ResponseT<string>(4, "备注长度超过限制"));
                }
            }

            //安全性检查：FriendsGroupId是否是请求者自己的好友分组
            var friendsGroupId = await _userContext.FriendsGroups.Select(group => new { group.Id, group.UUID,group.IsDeleted }).FirstOrDefaultAsync(group => group.Id == addFriendForm.FriendsGroupId && group.UUID == UUID && !group.IsDeleted);
            if (friendsGroupId == null)
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]在向[ {targetId} ]发送添加好友请求时尝试使用一个不存在的好友分组[ {friendsGroupId} ]，可能原因为用户正在尝试绕过前端进行操作。", UUID, addFriendForm.TargetId, addFriendForm.FriendsGroupId);
                return Ok(new ResponseT<string>(5, "您正在尝试使用一个不存在的好友分组"));
            }

            //判断是否已经是好友
            var friendship = await _userContext.Friendships.Select(ship => new { ship.UUID, ship.FriendId,ship.IsDeleted }).FirstOrDefaultAsync(ship => ship.UUID == UUID && ship.FriendId == addFriendForm.TargetId && !ship.IsDeleted);
            if (friendship != null)
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]尝试向其好友[ {targetId} ]发送添加好友请求，可能原因为发送请求前对方正好同意了原先发送的请求，或用户正在尝试绕过前端进行操作。", UUID, addFriendForm.TargetId);
                return Ok(new ResponseT<string>(6, "你们已经是好友了"));
            }

            //判断是否已经发送过请求
            AddFriendRequest? addFriendRequest = await _userContext.AddFriendRequests.FirstOrDefaultAsync(request => request.UUID == UUID && request.TargetId == addFriendForm.TargetId);
            if (addFriendRequest != null)
            {
                addFriendRequest.Message = addFriendForm.Message;
                addFriendRequest.Remark = addFriendForm.Remark;
                addFriendRequest.FriendsGroupId = addFriendForm.FriendsGroupId;
                addFriendRequest.IsPending = true;
            }
            else
            {
                _userContext.AddFriendRequests.Add(new AddFriendRequest(id: 0, UUID: UUID, targetId: addFriendForm.TargetId, message: addFriendForm.Message, remark: addFriendForm.Remark, friendsGroupId: addFriendForm.FriendsGroupId, isPending: true));
            }
            _userContext.SaveChanges();

            //在Redis中记录对方存在未读的添加好友请求
            _ = _distributedCache.SetStringAsync(addFriendForm.TargetId + "HasUnreadAddFriendRequest", "1");

            //尝试通过WebSocket向对方发送消息
            //通过Redis查找目标用户上一次在哪一台服务器连接了WebSocket，尝试由那台服务器发送消息
            string? webSocketPort = await _distributedCache.GetStringAsync(addFriendForm.TargetId + "WebSocket");
            if (webSocketPort != null)
            {
                _messagePublisher.SendMessage(new { type = "NewAddFriendRequest", data = addFriendForm.TargetId }, "friend", webSocketPort);
            }

            return Ok(new ResponseT<string>(0, "已成功发送添加好友请求"));
        }

        [HttpGet("request/hasUnread")]
        public async Task<IActionResult> CheckHasUnreadRequest([FromHeader] string JWT, [FromHeader] int UUID)
        {
            //查看在Redis中是否记录着自己存在未读的添加好友请求
            string? hasUnreadRequest = await _distributedCache.GetStringAsync(UUID + "HasUnreadAddFriendRequest");
            if (hasUnreadRequest != null)
            {
                return Ok(new ResponseT<bool>(0, "检查成功", true));
            }
            else
            {
                return Ok(new ResponseT<bool>(0, "检查成功", false));
            }
        }

        [HttpGet("request")]
        public async Task<IActionResult> GetAddFriendRequest([FromHeader] string JWT, [FromHeader] int UUID)
        {
            //获取所有TargetId为自己且未处理的添加好友请求
            List<AddFriendRequestDataForReceiver> dataList = await _userContext.AddFriendRequests
                .Select(request => new { request.Id, request.UUID, request.TargetId, request.Message, request.IsPending })
                .Where(request => request.TargetId == UUID && request.IsPending)
                .Join(_userContext.UserProfiles
                .Select(profile => new { profile.UUID, profile.Avatar, profile.Nickname, profile.UpdatedTime }),
                request => request.UUID, profile => profile.UUID, (request, profile) => new AddFriendRequestDataForReceiver(request.Id, new BriefUserInformation(profile.UUID, profile.Avatar, profile.Nickname, profile.UpdatedTime), request.Message))
                .ToListAsync();

            GetAddFriendRequestResponseData getAddFriendRequestResponseData = new(dataList);
            ResponseT<GetAddFriendRequestResponseData> getRequestDataSucceed = new(0, "成功获取所有未处理的好友请求", getAddFriendRequestResponseData);

            //清空在Redis中记录的自己存在未读的添加好友请求
            _ = _distributedCache.RemoveAsync(UUID + "HasUnreadAddFriendRequest");

            return Ok(getRequestDataSucceed);
        }

        [HttpPut("request/reject/{requestId}")]
        public async Task<IActionResult> RejectAddFriendRequest(int requestId, [FromHeader] string JWT, [FromHeader] int UUID)
        {
            //首先进行安全性检查
            //确保用户操作的是TargetId为自己且未处理的添加好友请求
            AddFriendRequest? addFriendRequest = await _userContext.AddFriendRequests.FirstOrDefaultAsync(request => request.Id == requestId && request.TargetId == UUID && request.IsPending);
            if (addFriendRequest == null)
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]正在尝试拒绝一个不属于该用户或属于该用户但已被处理的添加好友请求[ {requestId} ]，可能原因为用户拒绝请求前对方正好同意了用户发送的添加好友请求，或用户正在尝试绕过前端进行操作。", UUID, requestId);
                return Ok(new ResponseT<string>(2, "您正在尝试拒绝一个错误的添加好友请求"));
            }
            else
            {
                addFriendRequest.IsPending = false;
                _userContext.SaveChanges();
                return Ok(new ResponseT<string>(0, "您已成功拒绝该请求"));
            }
        }

        [HttpPut("request/agree/{requestId}")]
        public async Task<IActionResult> AgreeAddFriendRequest([FromRoute] int requestId, [FromBody] AgreeAddFriendForm agreeAddFriendForm, [FromHeader] string JWT, [FromHeader] int UUID)
        {
            //首先进行安全性检查
            //确保用户操作的是TargetId为自己且未处理的添加好友请求
            AddFriendRequest? addFriendRequest = await _userContext.AddFriendRequests.FirstOrDefaultAsync(request => request.Id == requestId && request.TargetId == UUID && request.IsPending);
            if (addFriendRequest == null)
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]正在尝试同意一个不属于该用户或属于该用户但已被处理的添加好友请求[ {requestId} ]，可能原因为用户同意请求前对方正好同意了用户发送的添加好友请求，或用户正在尝试绕过前端进行操作。", UUID, requestId);
                return Ok(new ResponseT<string>(2, "您正在尝试同意一个错误的添加好友请求"));
            }

            if (agreeAddFriendForm.Remark != null) 
            {
                //安全性检查：agreeAddFriendForm中的Remark是否符合要求
                var check = Regex.Split(agreeAddFriendForm.Remark, " +").ToList();
                check.RemoveAll(key => key == "");

                if (check.Count == 0)
                {
                    _logger.LogWarning("Warning：用户[ {UUID} ]在同意添加好友请求[ {requestId} ]时尝试使用不符合要求的备注[ {remark} ]", UUID, requestId, agreeAddFriendForm.Remark);
                    return Ok(new ResponseT<string>(3, "备注不可为空"));
                }

                if (agreeAddFriendForm.Remark.Length > 15)
                {
                    _logger.LogWarning("Warning：用户[ {UUID} ]在同意添加好友请求[ {requestId} ]时尝试使用超过长度限制的备注[ {remark} ]", UUID, requestId, agreeAddFriendForm.Remark);
                    return Ok(new ResponseT<string>(4, "备注长度超过限制"));
                }
            }

            //安全性检查：agreeAddFriendForm中的FriendsGroupId是否是自己的好友分组
            var friendsGroupId = await _userContext.FriendsGroups.Select(group => new { group.Id, group.UUID, group.IsDeleted }).FirstOrDefaultAsync(group => group.Id == agreeAddFriendForm.FriendsGroupId && group.UUID == UUID && !group.IsDeleted);
            if (friendsGroupId == null)
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]在同意添加好友请求[ {requestId} ]时尝试使用一个不存在的好友分组[ {friendsGroupId} ]，可能原因为用户正在尝试绕过前端进行操作。", UUID, requestId, agreeAddFriendForm.FriendsGroupId);
                return Ok(new ResponseT<string>(5, "您正在尝试使用一个不存在的好友分组"));
            }

            //有一种情况，双方都向对方发送了添加好友请求，其中一方先同意了
            //所以在同意前，先检查一遍，双方是否已经是好友，如果已经是了，将对方的请求设置为已处理
            //其中一方同意另一方发送的请求后，要检查这一方是否也向另一方发送了添加好友请求，需要将这一方的请求设置为已处理

            //同意前，先检查一遍，双方是否已经是好友
            var friendship = await _userContext.Friendships.Select(ship => new { ship.UUID, ship.FriendId, ship.IsDeleted }).FirstOrDefaultAsync(ship => ship.UUID == UUID && ship.FriendId == addFriendRequest.UUID && !ship.IsDeleted);
            if (friendship != null)
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]尝试同意其好友[ {targetId} ]发送的添加好友请求[ {requestId} ]，可能原因为同意请求前对方正好同意了先前由用户发送的请求。", UUID, addFriendRequest.UUID, addFriendRequest.Id);
                addFriendRequest.IsPending = false;
                _userContext.SaveChanges();
                return Ok(new ResponseT<string>(6, "你们已经是好友了"));
            }

            DateTime dateTime = DateTime.Now;

            //查找双方是否曾是好友
            var oldFriendshipForSender = await _userContext.Friendships
                .SingleOrDefaultAsync(ship => ship.UUID == addFriendRequest.UUID && ship.FriendId == addFriendRequest.TargetId && ship.IsDeleted);
            Friendship friendshipForSender;

            var oldFriendshipForReceiver = await _userContext.Friendships
                .SingleOrDefaultAsync(ship => ship.UUID == addFriendRequest.TargetId && ship.FriendId == addFriendRequest.UUID && ship.IsDeleted);
            Friendship friendshipForReceiver;

            if (oldFriendshipForSender != null)
            {
                oldFriendshipForSender.FriendsGroupId = addFriendRequest.FriendsGroupId;
                oldFriendshipForSender.Remark = addFriendRequest.Remark;
                oldFriendshipForSender.IsFocus = false;
                oldFriendshipForSender.IsDeleted = false;
                oldFriendshipForSender.UpdatedTime = dateTime;

                friendshipForSender = oldFriendshipForSender;
            }
            else
            {
                //新建对方的好友记录
                friendshipForSender = new(id: 0,
                    UUID: addFriendRequest.UUID,
                    friendsGroupId: addFriendRequest.FriendsGroupId,
                    friendId: addFriendRequest.TargetId,
                    shipCreatedTime: dateTime,
                    remark: addFriendRequest.Remark,
                    isFocus: false, 
                    isDeleted: false,
                    updatedTime: dateTime);
                _userContext.Friendships.Add(friendshipForSender);
            }

            if (oldFriendshipForReceiver != null)
            {
                oldFriendshipForReceiver.FriendsGroupId = agreeAddFriendForm.FriendsGroupId;
                oldFriendshipForReceiver.Remark = agreeAddFriendForm.Remark;
                oldFriendshipForReceiver.IsFocus = false;
                oldFriendshipForReceiver.IsDeleted = false;
                oldFriendshipForReceiver.UpdatedTime = dateTime;

                friendshipForReceiver = oldFriendshipForReceiver;
            }
            else
            {
                //新建自己的好友记录
                friendshipForReceiver = new(id: 0,
                    UUID: addFriendRequest.TargetId,
                    friendsGroupId: agreeAddFriendForm.FriendsGroupId,
                    friendId: addFriendRequest.UUID, 
                    shipCreatedTime: dateTime,
                    remark: agreeAddFriendForm.Remark, 
                    isFocus: false, 
                    isDeleted: false,
                    updatedTime: dateTime);
                _userContext.Friendships.Add(friendshipForReceiver);
            }
            //更改用户同步表中的用户的好友关系的最后一次更新时间
            UserSyncTable? syncTableForSender = await _userContext.UserSyncTables.FirstOrDefaultAsync(table => table.UUID == addFriendRequest.UUID);
            syncTableForSender!.UpdatedTimeForFriendships = dateTime;

            //更改用户同步表中的用户的好友关系的最后一次更新时间
            UserSyncTable? syncTableForReceiver = await _userContext.UserSyncTables.FirstOrDefaultAsync(table => table.UUID == UUID);
            syncTableForReceiver!.UpdatedTimeForFriendships = dateTime;

            addFriendRequest.IsPending = false;

            //检查另一方是否也发送了添加好友请求，将另一方的请求设置为已处理
            AddFriendRequest? anotherAddFriendRequest = await _userContext.AddFriendRequests.FirstOrDefaultAsync(request => request.UUID == UUID && request.TargetId == addFriendRequest.UUID && request.IsPending);
            if (anotherAddFriendRequest != null)
            {
                anotherAddFriendRequest.IsPending = false;
            }
            _userContext.SaveChanges();

            //尝试通过WebSocket向对方发送消息
            //通过Redis查找目标用户上一次在哪一台服务器连接了WebSocket，尝试由那台服务器发送消息
            string? webSocketPort = await _distributedCache.GetStringAsync(addFriendRequest.UUID + "WebSocket");
            if (webSocketPort != null)
            {
                _messagePublisher.SendMessage(new { type = "NewFriendship", data = friendshipForSender }, "friend", webSocketPort);
            }

            AgreeAddFriendRequestResponseData agreeAddFriendRequestResponseData = new(friendshipForReceiver);
            ResponseT<AgreeAddFriendRequestResponseData> agreeAddFriendRequestSucceed = new(0, "您已成功同意该请求", agreeAddFriendRequestResponseData);
            return Ok(agreeAddFriendRequestSucceed);
        }

        [HttpPut("remark")]
        public async Task<IActionResult> EditRemark([FromBody] EditRemarkRequestData editRemarkRequestData, [FromHeader] string JWT, [FromHeader] int UUID) 
        {
            var friendship = await _userContext.Friendships.FindAsync(editRemarkRequestData.FriendshipId);
            if (friendship == null)
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]在修改好友[ {friendshipId} ]备注时失败，原因为该好友关系不存在", UUID, editRemarkRequestData.FriendshipId);
                return Ok(new ResponseT<string>(2, "该好友关系不存在"));
            }
            else if (friendship.UUID != UUID || friendship.IsDeleted)
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]在修改好友[ {friendshipId} ]备注时失败，原因为该好友关系不属于该用户", UUID, editRemarkRequestData.FriendshipId);
                return Ok(new ResponseT<string>(3, "该用户不是您的好友"));
            }

            if (editRemarkRequestData.Remark != null)
            {
                var check = Regex.Split(editRemarkRequestData.Remark, " +").ToList();
                check.RemoveAll(key => key == "");

                if (check.Count == 0)
                {
                    _logger.LogWarning("Warning：用户[ {UUID} ]在修改好友[ {friendshipId} ]备注时尝试使用不符合要求的备注[ {remark} ]", UUID, editRemarkRequestData.FriendshipId, editRemarkRequestData.Remark);
                    return Ok(new ResponseT<string>(4, "备注不可为空"));
                }

                if (editRemarkRequestData.Remark.Length > 15)
                {
                    _logger.LogWarning("Warning：用户[ {UUID} ]在修改好友[ {friendshipId} ]备注时尝试使用超过长度限制的备注[ {remark} ]", UUID, editRemarkRequestData.FriendshipId, editRemarkRequestData.Remark);
                    return Ok(new ResponseT<string>(5, "备注长度超过限制"));
                }
            }

            DateTime now = DateTime.Now;
            friendship.Remark = editRemarkRequestData.Remark;
            friendship.UpdatedTime = now;
            //更改用户同步表中的用户的好友关系的最后一次更新时间
            UserSyncTable? syncTable = await _userContext.UserSyncTables.FirstOrDefaultAsync(table => table.UUID == UUID);
            syncTable!.UpdatedTimeForFriendships = now;
            _userContext.SaveChanges();

            return Ok(new ResponseT<EditRemarkResponseData>(0, "修改备注成功", new(editRemarkRequestData.FriendshipId, editRemarkRequestData.Remark, now)));
        }

        [HttpDelete("{friendshipId}")]
        public async Task<IActionResult> DeleteFriendship([FromRoute] int friendshipId, [FromHeader] string JWT, [FromHeader] int UUID) 
        {
            var senderFriendship = await _userContext.Friendships.SingleOrDefaultAsync(ship => ship.Id == friendshipId && !ship.IsDeleted);
            if (senderFriendship == null) 
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]删除好友时失败，原因为尝试对不存在的好友关系[ {friendshipId} ]进行删除。", UUID, friendshipId);
                return Ok(new ResponseT<string>(2, "您无法对不是好友的用户进行该操作"));
            }

            var receiverFriendship = await _userContext.Friendships.SingleAsync(ship => ship.UUID == senderFriendship.FriendId && ship.FriendId == UUID);

            DateTime now = DateTime.Now;

            senderFriendship.IsDeleted = true;
            senderFriendship.UpdatedTime = now;

            receiverFriendship.IsDeleted = true;
            receiverFriendship.UpdatedTime = now;

            //更改用户同步表中的用户的好友关系的最后一次更新时间
            UserSyncTable? senderSyncTable = await _userContext.UserSyncTables.FirstOrDefaultAsync(table => table.UUID == UUID);
            senderSyncTable!.UpdatedTimeForFriendships = now;
            UserSyncTable? receiverSyncTable = await _userContext.UserSyncTables.FirstOrDefaultAsync(table => table.UUID == senderFriendship.FriendId);
            receiverSyncTable!.UpdatedTimeForFriendships = now;

            _userContext.SaveChanges();

            // 删除好友的同时屏蔽对方
            var redis = _redisConnection.GetUserBlockListDatabase();
            _ = redis.SetAddAsync($"{UUID}BlockList", senderFriendship.FriendId);

            //尝试通过WebSocket向对方发送消息
            //通过Redis查找目标用户上一次在哪一台服务器连接了WebSocket，尝试由那台服务器发送消息
            string? webSocketPort = await _distributedCache.GetStringAsync(senderFriendship.FriendId + "WebSocket");
            if (webSocketPort != null)
            {
                _messagePublisher.SendMessage(new { type = "FriendshipBeDeleted", data = receiverFriendship }, "friend", webSocketPort);
            }

            DeleteFriendshipResponseData deleteFriendshipResponseData = new(senderFriendship);
            ResponseT<DeleteFriendshipResponseData> deleteFriendshipSucceed = new(0, "删除好友成功", deleteFriendshipResponseData);
            return Ok(deleteFriendshipSucceed);
        }
    }
}
