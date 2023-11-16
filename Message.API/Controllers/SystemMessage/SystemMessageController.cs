using Message.API.Controllers.CommonMessage;
using Message.API.DataContext.Message;
using Message.API.Entities.Message;
using Message.API.Filters;
using Message.API.ReusableClass;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using User.API.DataContext.User;
using User.API.Entities.User;

namespace Message.API.Controllers.SystemMessage
{
    public class SystemMessageDataForClient
    {
        public SystemMessageDataForClient(int id, int chatId, int senderId, int receiverId, DateTime createdTime, bool isCustom, bool isRecalled, bool isDeleted, bool isReply, bool isMediaMessage, bool isVoiceMessage, string? customType, string? minimumSupportVersion, string? textOnError, string? customMessageContent, int? messageReplied, string? messageText, string? messageMedias, string? messageVoice, int sequence)
        {
            Id = id;
            ChatId = chatId;
            SenderId = senderId;
            ReceiverId = receiverId;
            CreatedTime = createdTime;
            IsCustom = isCustom;
            IsRecalled = isRecalled;
            IsDeleted = isDeleted;
            IsReply = isReply;
            IsMediaMessage = isMediaMessage;
            IsVoiceMessage = isVoiceMessage;
            CustomType = customType;
            MinimumSupportVersion = minimumSupportVersion;
            TextOnError = textOnError;
            CustomMessageContent = customMessageContent;
            MessageReplied = messageReplied;
            MessageText = messageText;
            MessageMedias = messageMedias;
            MessageVoice = messageVoice;
            Sequence = sequence;
        }

        public int Id { get; set; }
        public int ChatId { get; set; }
        public int SenderId { get; set; }
        public int ReceiverId { get; set; }
        public DateTime CreatedTime { get; set; }
        public bool IsCustom { get; set; }
        public bool IsRecalled { get; set; }
        public bool IsDeleted { get; set; }
        public bool IsReply { get; set; }
        public bool IsMediaMessage { get; set; }
        public bool IsVoiceMessage { get; set; }
        public string? CustomType { get; set; }
        public string? MinimumSupportVersion { get; set; }
        public string? TextOnError { get; set; }
        public string? CustomMessageContent { get; set; }
        public int? MessageReplied { get; set; }
        public string? MessageText { get; set; }
        public string? MessageMedias { get; set; }
        public string? MessageVoice { get; set; }
        public int Sequence { get; set; }
    }

    [ApiController]
    [Route("/systemMessage")]
    [ServiceFilter(typeof(JWTAuthFilterService), IsReusable = true)]
    public class SystemMessageController : Controller
    {
        //依赖注入
        private readonly UserContext _userContext;
        private readonly MessageContext _messageContext;
        private readonly ILogger<SystemMessageController> _logger;

        public SystemMessageController(UserContext userContext, MessageContext messageContext, ILogger<SystemMessageController> logger)
        {
            _userContext = userContext;
            _messageContext = messageContext;
            _logger = logger;
        }

        // 用户删除某条消息
        // 需要写进SystemMessageInbox中
        [HttpDelete("{messageId}")]
        public async Task<IActionResult> DeleteSystemMessage([FromRoute] int messageId, [FromHeader] string JWT, [FromHeader] int UUID)
        {
            var message = await _messageContext.SystemMessages
                .FirstOrDefaultAsync(message => message.Id == messageId);

            if (message == null)
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]删除消息时失败，用户正在尝试删除一条不存在的消息[ {messageId} ]。", UUID, messageId);
                ResponseT<string> deleteMessageFailed = new(2, "您正在尝试删除一条不存在的消息");
                return Ok(deleteMessageFailed);
            }

            if (message.SenderId != UUID && message.ReceiverId != UUID)
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]删除消息时失败，用户正在尝试删除一条不属于该用户的消息[ {messageId} ]。", UUID, messageId);
                ResponseT<string> deleteMessageFailed = new(3, "您无法删除一条不属于您的消息");
                return Ok(deleteMessageFailed);
            }

            //使用事务
            //由于需要操作不同的数据库，故这里应当使用分布式事务，但.NET中的分布式事务目前只支持Windows平台上的.NET7.0，故暂时无法使用
            //这里的事务使用方法是错误的
            using var messageContextTransaction = _messageContext.Database.BeginTransaction();
            {
                using var userContextTransaction = _userContext.Database.BeginTransaction();
                try
                {
                    int targetId;
                    if (message.SenderId != UUID)
                    {
                        targetId = message.SenderId;
                    }
                    else
                    {
                        targetId = message.ReceiverId;
                    }

                    int userChatId = _messageContext.Chats
                        .Select(chat => new { chat.Id, chat.UUID, chat.TargetId, chat.IsWithSystem })
                    .Single(chat => chat.UUID == UUID && chat.TargetId == targetId && chat.IsWithSystem)
                        .Id;

                    UserSyncTable? currentUserSyncTable = await _userContext.UserSyncTables.FirstOrDefaultAsync(table => table.UUID == UUID);
                    int newUserSequence = currentUserSyncTable!.SequenceForSystemMessages + 1;
                    SystemMessageInbox userInbox = new(id: 0, UUID: UUID, messageId: message.Id, chatId: userChatId, isDeleted: true, sequence: newUserSequence);

                    _messageContext.SystemMessageInboxes.Add(userInbox);
                    _messageContext.SaveChanges();

                    currentUserSyncTable.SequenceForSystemMessages++;
                    _userContext.SaveChanges();

                    userContextTransaction.Commit();
                    messageContextTransaction.Commit();

                    //返回相关信息，供删除消息者的客户端更新数据库
                    SystemMessageDataForClient dataForSender = new(message.Id, userInbox.ChatId, message.SenderId, message.ReceiverId, message.CreatedTime, message.IsCustom, message.IsRecalled, true, message.IsReply, message.IsMediaMessage, message.IsVoiceMessage, message.CustomType, message.MinimumSupportVersion, message.TextOnError, message.CustomMessageContent, message.MessageReplied, message.MessageText, message.MessageMedias, message.MessageVoice, currentUserSyncTable.SequenceForSystemMessages);
                    ResponseT<SystemMessageDataForClient> deleteMessageSucceed = new(0, "删除消息成功", dataForSender);
                    return Ok(deleteMessageSucceed);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error：用户[ {UUID} ]在删除消息[ {messageId} ]时发生错误。报错信息为[ {ex} ]。", UUID, messageId, ex);
                    ResponseT<string> deleteMessageFailed = new(4, "发生错误，消息删除失败");
                    return Ok(deleteMessageFailed);
                }
            }
        }
    }
}
