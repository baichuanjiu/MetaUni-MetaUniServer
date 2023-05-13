using Message.API.DataContext.Message;
using Message.API.Entities.Chat;
using Message.API.Filters;
using Message.API.ReusableClass;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using User.API.Controllers.Profile;
using User.API.DataContext.User;
using User.API.Entities.User;

namespace Message.API.Controllers.Chat
{
    public class SyncChatsResponseData
    {
        public SyncChatsResponseData(List<Entities.Chat.Chat> chatsList, List<BriefChatTargetInformation> briefChatTargetInformationList, DateTime updatedTime)
        {
            ChatsList = chatsList;
            BriefChatTargetInformationList = briefChatTargetInformationList;
            UpdatedTime = updatedTime;
        }

        public List<Entities.Chat.Chat> ChatsList { get; set; }
        public List<BriefChatTargetInformation> BriefChatTargetInformationList { get; set; }
        public DateTime UpdatedTime { get; set; }
    }

    public class BriefChatTargetInformation
    {
        public BriefChatTargetInformation(int targetType, int id, string avatar, string name, DateTime updatedTime)
        {
            TargetType = ((TargetTypeEnum)targetType).ToString();
            Id = id;
            Avatar = avatar;
            Name = name;
            UpdatedTime = updatedTime;
        }

        public enum TargetTypeEnum { user, group, system };
        public string TargetType { get; set; }
        public int Id { get; set; }
        public string Avatar { get; set; }
        public string Name { get; set; }
        public DateTime UpdatedTime { get; set; }
    }
    public class CommonChatStatusDataForClient
    {
        public CommonChatStatusDataForClient(int chatId, int? lastMessageBeReadSendByMe, DateTime? readTime, DateTime updatedTime)
        {
            ChatId = chatId;
            LastMessageBeReadSendByMe = lastMessageBeReadSendByMe;
            ReadTime = readTime;
            UpdatedTime = updatedTime;
        }

        public int ChatId { get; set; }
        public int? LastMessageBeReadSendByMe { get; set; }
        public DateTime? ReadTime { get; set; }
        public DateTime UpdatedTime { get; set; }
    }

    public class SyncCommonChatStatusesResponseData
    {
        public SyncCommonChatStatusesResponseData(List<CommonChatStatusDataForClient> dataList, DateTime updatedTime)
        {
            DataList = dataList;
            UpdatedTime = updatedTime;
        }

        public List<CommonChatStatusDataForClient> DataList { get; set; }
        public DateTime UpdatedTime { get; set; }
    }

    [ApiController]
    [Route("/chat")]
    [ServiceFilter(typeof(JWTAuthFilterService), IsReusable = true)]
    public class ChatController : Controller
    {
        //依赖注入
        private readonly UserContext _userContext;
        private readonly MessageContext _messageContext;
        private readonly ILogger<ProfileController> _logger;

        public ChatController(UserContext userContext, MessageContext messageContext, ILogger<ProfileController> logger)
        {
            _userContext = userContext;
            _messageContext = messageContext;
            _logger = logger;
        }

        [HttpGet("sync")]
        public async Task<IActionResult> SyncChats([FromQuery] DateTime updatedTime, [FromHeader] string JWT, [FromHeader] int UUID)
        {
            //实际查询时间（冗余10分钟）
            DateTime queryTime = updatedTime.AddMinutes(-10);

            //查找数据库
            DateTime currentUpdatedTime = _userContext.UserSyncTables.Select(table => new { table.UUID, UpdatedTime = table.UpdatedTimeForChats }).FirstOrDefaultAsync(table => table.UUID == UUID).Result!.UpdatedTime;
            List<Entities.Chat.Chat> chatsList = await _messageContext.Chats.Where(chat => chat.UUID == UUID && chat.UpdatedTime > queryTime).ToListAsync();

            List<BriefChatTargetInformation> briefChatTargetInformationList = new();

            //后续还会添加group与system
            List<int> usersList = new();
            List<BriefChatTargetInformation> usersInformationList = new();
            chatsList.ForEach(chat =>
            {
                if (chat.IsWithOtherUser)
                {
                    usersList.Add(chat.TargetId);
                }
            });
            usersInformationList = usersList
                .Join(_userContext.UserProfiles.Select(profile => new { profile.UUID, profile.Avatar, profile.Nickname, profile.UpdatedTime }),
                Id => Id, profile => profile.UUID,
                (Id, profile) => new BriefChatTargetInformation(0, profile.UUID, profile.Avatar, profile.Nickname, profile.UpdatedTime))
                .ToList();

            briefChatTargetInformationList.AddRange(usersInformationList);

            SyncChatsResponseData syncChatsResponseData = new(chatsList, briefChatTargetInformationList, currentUpdatedTime);
            ResponseT<SyncChatsResponseData> getSyncDataSucceed = new(0, "成功获取待同步的数据", syncChatsResponseData);
            return Ok(getSyncDataSucceed);
        }

        [HttpGet("commonChatStatus/sync")]
        public async Task<IActionResult> SyncCommonChatStatus([FromQuery] DateTime lastSyncTime, [FromHeader] string JWT, [FromHeader] int UUID)
        {
            //实际查询时间（冗余10分钟）
            DateTime queryTime = lastSyncTime.AddMinutes(-10);
            //查找从queryTime到currentTime内更新过的数据
            DateTime currentTime = DateTime.Now;

            //查找数据库
            List<CommonChatStatusDataForClient> dataList = await _messageContext.CommonChatStatuses.Select(status => new { status.UUID, status.ChatId, status.LastMessageBeReadSendByMe, status.ReadTime, status.UpdatedTime }).Where(status => status.UUID == UUID && status.UpdatedTime > queryTime).Select(status => new CommonChatStatusDataForClient(status.ChatId, status.LastMessageBeReadSendByMe, status.ReadTime, status.UpdatedTime)).ToListAsync();

            UserSyncTable? userSyncTable = await _userContext.UserSyncTables.FirstOrDefaultAsync(table => table.UUID == UUID);
            userSyncTable!.LastSyncTimeForCommonChatStatuses = currentTime;
            await _userContext.SaveChangesAsync();

            SyncCommonChatStatusesResponseData syncCommonChatStatusesResponseData = new(dataList, currentTime);
            ResponseT<SyncCommonChatStatusesResponseData> getSyncDataSucceed = new(0, "成功获取待同步的数据", syncCommonChatStatusesResponseData);
            return Ok(getSyncDataSucceed);
        }

    }
}
