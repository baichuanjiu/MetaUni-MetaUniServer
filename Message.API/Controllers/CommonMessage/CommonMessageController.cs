using Message.API.DataContext.Message;
using Message.API.Entities.Chat;
using Message.API.Entities.Message;
using Message.API.Filters;
using Message.API.RabbitMQ;
using Message.API.ReusableClass;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using User.API.Controllers.Profile;
using User.API.DataContext.User;
using User.API.Entities.User;

namespace Message.API.Controllers.CommonMessage
{
    public class TextMessageData
    {
        public TextMessageData(int receiverId, string messageText)
        {
            ReceiverId = receiverId;
            MessageText = messageText;
        }

        public int ReceiverId { get; set; }
        public string MessageText { get; set; }
    }
    public class CommonMessageDataForClient
    {
        public CommonMessageDataForClient(int id, int chatId, int senderId, int receiverId, DateTime createdTime, bool isCustom, bool isRecalled, bool isDeleted, bool isReply, bool isImageMessage, bool isVoiceMessage, string? customType, string? minimumSupportVersion, string? textOnError, string? customMessageContent, int? messageReplied, string? messageText, string? messageImage, string? messageVoice, int sequence)
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
            IsImageMessage = isImageMessage;
            IsVoiceMessage = isVoiceMessage;
            CustomType = customType;
            MinimumSupportVersion = minimumSupportVersion;
            TextOnError = textOnError;
            CustomMessageContent = customMessageContent;
            MessageReplied = messageReplied;
            MessageText = messageText;
            MessageImage = messageImage;
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
        public bool IsImageMessage { get; set; }
        public bool IsVoiceMessage { get; set; }
        public string? CustomType { get; set; }
        public string? MinimumSupportVersion { get; set; }
        public string? TextOnError { get; set; }
        public string? CustomMessageContent { get; set; }
        public int? MessageReplied { get; set; }
        public string? MessageText { get; set; }
        public string? MessageImage { get; set; }
        public string? MessageVoice { get; set; }
        public int Sequence { get; set; }
    }

    [ApiController]
    [Route("/commonMessage")]
    [ServiceFilter(typeof(JWTAuthFilterService), IsReusable = true)]
    public class CommonMessageController : Controller
    {
        //依赖注入
        private readonly UserContext _userContext;
        private readonly MessageContext _messageContext;
        private readonly IDistributedCache _distributedCache;
        private readonly IMessagePublisher _messagePublisher;
        private readonly ILogger<ProfileController> _logger;

        public CommonMessageController(UserContext userContext, MessageContext messageContext, IDistributedCache distributedCache, IMessagePublisher messagePublisher, ILogger<ProfileController> logger)
        {
            _userContext = userContext;
            _messageContext = messageContext;
            _distributedCache = distributedCache;
            _messagePublisher = messagePublisher;
            _logger = logger;
        }

        [HttpPost("text")]
        public async Task<IActionResult> SendTextMessage([FromBody] TextMessageData textMessageData, [FromHeader] string JWT, [FromHeader] int UUID)
        {
            //查找数据库
            int? targetUser = await _userContext.UserAccounts.Select(account => account.UUID).FirstOrDefaultAsync(UUID => UUID == textMessageData.ReceiverId);
            if (targetUser == null)
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]正在尝试向不存在的用户[ {targetUser} ]发送消息", UUID, textMessageData.ReceiverId);
                ResponseT<string> sendMessageFailed = new(2, "目标用户不存在");
                return Ok(sendMessageFailed);
            }

            //使用事务
            //由于需要操作不同的数据库，故这里应当使用分布式事务，但.NET中的分布式事务目前只支持Windows平台上的.NET7.0，故暂时无法使用
            //这里的事务使用方法是错误的
            using var messageContextTransaction = _messageContext.Database.BeginTransaction();
            try
            {
                Entities.Message.CommonMessage message = new(id: 0, senderId: UUID, receiverId: textMessageData.ReceiverId, createdTime: DateTime.Now, isCustom: false, isRecalled: false, isReply: false, isImageMessage: false, isVoiceMessage: false, customType: null, minimumSupportVersion: null, textOnError: null, customMessageContent: null, messageReplied: null, messageText: textMessageData.MessageText, messageImage: null, messageVoice: null);
                _messageContext.CommonMessages.Add(message);

                _messageContext.SaveChanges();

                Entities.Chat.Chat? senderChat = await _messageContext.Chats.FirstOrDefaultAsync(chat => chat.UUID == UUID && chat.TargetId == textMessageData.ReceiverId && chat.IsWithOtherUser);
                Entities.Chat.Chat? receiverChat = await _messageContext.Chats.FirstOrDefaultAsync(chat => chat.UUID == textMessageData.ReceiverId && chat.TargetId == UUID && chat.IsWithOtherUser);
                int senderChatId;
                int receiverChatId;
                if (senderChat == null)
                {
                    Entities.Chat.Chat newSenderChat = new(id: 0, UUID: UUID, targetId: textMessageData.ReceiverId, isWithOtherUser: true, isWithGroup: false, isWithSystem: false, isStickyOnTop: false, isDeleted: false, numberOfUnreadMessages: 0, lastMessageId: message.Id, updatedTime: message.CreatedTime);
                    _messageContext.Chats.Add(newSenderChat);
                    _messageContext.SaveChanges();

                    senderChatId = newSenderChat.Id;

                    Entities.Chat.Chat newReceiverChat = new(id: 0, UUID: textMessageData.ReceiverId, targetId: UUID, isWithOtherUser: true, isWithGroup: false, isWithSystem: false, isStickyOnTop: false, isDeleted: false, numberOfUnreadMessages: 1, lastMessageId: message.Id, updatedTime: message.CreatedTime);
                    _messageContext.Chats.Add(newReceiverChat);
                    _messageContext.SaveChanges();

                    receiverChatId = newReceiverChat.Id;

                    CommonChatStatus newSenderChatStatus = new(id: 0, UUID: newSenderChat.UUID, chatId: senderChatId, targetUserChatId: receiverChatId, lastMessageSendByMe: message.Id,lastMessageBeReadSendByMe: null, readTime: null, updatedTime: message.CreatedTime);
                    _messageContext.CommonChatStatuses.Add(newSenderChatStatus);

                    CommonChatStatus newReceiverChatStatus = new(id: 0, UUID: newReceiverChat.UUID, chatId: receiverChatId, targetUserChatId: senderChatId, lastMessageSendByMe: null, lastMessageBeReadSendByMe: null, readTime: null, updatedTime: message.CreatedTime);
                    _messageContext.CommonChatStatuses.Add(newReceiverChatStatus);
                }
                else
                {
                    senderChat.IsDeleted = false;
                    senderChat.LastMessageId = message.Id;
                    senderChat.UpdatedTime = message.CreatedTime;

                    senderChatId = senderChat.Id;

                    CommonChatStatus? senderChatStatus = await _messageContext.CommonChatStatuses.FirstOrDefaultAsync(status => status.ChatId == senderChat.Id);
                    senderChatStatus!.LastMessageSendByMe = message.Id;

                    receiverChat!.IsDeleted = false;
                    receiverChat.NumberOfUnreadMessages++;
                    receiverChat.LastMessageId = message.Id;
                    receiverChat.UpdatedTime = message.CreatedTime;

                    receiverChatId = receiverChat.Id;
                }

                _messageContext.SaveChanges();

                UserSyncTable? currentSenderSyncTable = await _userContext.UserSyncTables.FirstOrDefaultAsync(table => table.UUID == UUID);
                int newSenderSequence = currentSenderSyncTable!.SequenceForCommonMessages + 1;
                CommonMessageInbox senderInbox = new(id: 0, UUID: UUID, messageId: message.Id, chatId: senderChatId, isDeleted: false, sequence: newSenderSequence);

                UserSyncTable? currentReceiverSyncTable = await _userContext.UserSyncTables.FirstOrDefaultAsync(table => table.UUID == textMessageData.ReceiverId);
                int newReceiverSequence = currentReceiverSyncTable!.SequenceForCommonMessages + 1;
                CommonMessageInbox receiverInbox = new(id: 0, UUID: textMessageData.ReceiverId, messageId: message.Id, chatId: receiverChatId, isDeleted: false, sequence: newReceiverSequence);

                _messageContext.CommonMessageInboxes.Add(senderInbox);
                _messageContext.CommonMessageInboxes.Add(receiverInbox);
                _messageContext.SaveChanges();

                //使用事务
                using var userContextTransaction = _userContext.Database.BeginTransaction();
                currentSenderSyncTable.SequenceForCommonMessages++;
                currentSenderSyncTable.UpdatedTimeForChats = message.CreatedTime;
                currentReceiverSyncTable.SequenceForCommonMessages++;
                currentReceiverSyncTable.UpdatedTimeForChats = message.CreatedTime;
                _userContext.SaveChanges();

                userContextTransaction.Commit();
                messageContextTransaction.Commit();

                CommonMessageDataForClient dataForReceiver = new(message.Id, receiverInbox.ChatId, message.SenderId, message.ReceiverId, message.CreatedTime, message.IsCustom, message.IsRecalled, false, message.IsReply, message.IsImageMessage, message.IsVoiceMessage, message.CustomType, message.MinimumSupportVersion, message.TextOnError, message.CustomMessageContent, message.MessageReplied, message.MessageText, message.MessageImage, message.MessageVoice, currentReceiverSyncTable.SequenceForCommonMessages);

                //操作完数据库后，使用消息队列发送消息
                //通过Redis查找目标用户上一次在哪一台服务器连接了WebSocket，尝试由那台服务器发送消息
                string? webSocketPort = await _distributedCache.GetStringAsync(textMessageData.ReceiverId + "WebSocket");
                if (webSocketPort != null)
                {
                    _messagePublisher.SendMessage(new { type = "NewCommonMessage", data = dataForReceiver }, "msg", webSocketPort);
                }
                else
                {
                    //表明无法通过WebSocket发送此消息，需要将该消息视作发送失败，进入MongoDB中
                }

                //返回相关信息，供发送者的客户端更新数据库
                CommonMessageDataForClient dataForSender = new(message.Id, senderInbox.ChatId, message.SenderId, message.ReceiverId, message.CreatedTime, message.IsCustom, message.IsRecalled, false, message.IsReply, message.IsImageMessage, message.IsVoiceMessage, message.CustomType, message.MinimumSupportVersion, message.TextOnError, message.CustomMessageContent, message.MessageReplied, message.MessageText, message.MessageImage, message.MessageVoice, currentSenderSyncTable.SequenceForCommonMessages);
                ResponseT<CommonMessageDataForClient> sendMessageSucceed = new(0, "发送成功", dataForSender);
                return Ok(sendMessageSucceed);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error：用户[ {UUID} ]在向[ {targetUser} ]发送消息时发生错误。报错信息为[ {ex} ]。", UUID, textMessageData.ReceiverId, ex);
                ResponseT<string> sendMessageFailed = new(3, "服务器端发生错误");
                return Ok(sendMessageFailed);
            }
        }

    }
}
