using Message.API.DataContext.Message;
using Message.API.Entities.Chat;
using Message.API.Entities.Message;
using Message.API.Filters;
using Message.API.MinIO;
using Message.API.RabbitMQ;
using Message.API.Redis;
using Message.API.ReusableClass;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using User.API.DataContext.User;
using User.API.Entities.User;

namespace Message.API.Controllers.CommonMessage
{
    public class MediaMetadata
    {
        public MediaMetadata(string type, string URL, double aspectRatio, string? previewImage, int? timeTotal)
        {
            Type = type;
            this.URL = URL;
            AspectRatio = aspectRatio;
            PreviewImage = previewImage;
            TimeTotal = timeTotal;
        }

        public string Type { get; set; }
        public string URL { get; set; }
        public double AspectRatio { get; set; }
        public string? PreviewImage { get; set; }
        //TimeTotal: milliseconds
        public int? TimeTotal { get; set; }
    }

    public class SendCommonMessageMediaMetadata
    {
        public SendCommonMessageMediaMetadata()
        {
        }

        public SendCommonMessageMediaMetadata(IFormFile file, double aspectRatio, IFormFile? previewImage, int? timeTotal)
        {
            File = file;
            AspectRatio = aspectRatio;
            PreviewImage = previewImage;
            TimeTotal = timeTotal;
        }

        public IFormFile File { get; set; }
        public double AspectRatio { get; set; }
        public IFormFile? PreviewImage { get; set; }
        public int? TimeTotal { get; set; }
    }

    // 适用于文字消息、带有图片或视频的消息、回复某条消息
    // 特殊消息与语音消息走其它接口
    public class SendCommonMessageRequestData
    {
        public SendCommonMessageRequestData()
        {
        }


        public int ReceiverId { get; set; }
        public int? MessageReplied { get; set; }
        public string? MessageText { get; set; }
        public List<SendCommonMessageMediaMetadata>? MessageMedias { get; set; }
    }

    public class CommonMessageDataForClient
    {
        public CommonMessageDataForClient(int id, int chatId, int senderId, int receiverId, DateTime createdTime, bool isCustom, bool isRecalled, bool isDeleted, bool isReply, bool isMediaMessage, bool isVoiceMessage, string? customType, string? minimumSupportVersion, string? textOnError, string? customMessageContent, int? messageReplied, string? messageText, string? messageMedias, string? messageVoice, int sequence)
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
    [Route("/commonMessage")]
    [ServiceFilter(typeof(JWTAuthFilterService), IsReusable = true)]
    public class CommonMessageController : Controller
    {
        //依赖注入
        private readonly IConfiguration _configuration;
        private readonly UserContext _userContext;
        private readonly MessageContext _messageContext;
        private readonly IDistributedCache _distributedCache;
        private readonly RedisConnection _redisConnection;
        private readonly CommonMessageMediasMinIOService _commonMessageMediasMinIOService;
        private readonly IMessagePublisher _messagePublisher;
        private readonly ILogger<CommonMessageController> _logger;

        public CommonMessageController(IConfiguration configuration, UserContext userContext, MessageContext messageContext, IDistributedCache distributedCache, RedisConnection redisConnection, CommonMessageMediasMinIOService commonMessageMediasMinIOService, IMessagePublisher messagePublisher, ILogger<CommonMessageController> logger)
        {
            _configuration = configuration;
            _userContext = userContext;
            _messageContext = messageContext;
            _distributedCache = distributedCache;
            _redisConnection = redisConnection;
            _commonMessageMediasMinIOService = commonMessageMediasMinIOService;
            _messagePublisher = messagePublisher;
            _logger = logger;
        }

        // 适用于文字消息、带有图片或视频的消息、回复某条消息
        // 特殊消息与语音消息走其它接口
        [HttpPost("common")]
        public async Task<IActionResult> SendCommonMessage([FromForm] SendCommonMessageRequestData formData, [FromHeader] string JWT, [FromHeader] int UUID)
        {
            if (formData.MessageText != null && formData.MessageText.Length == 0)
            {
                formData.MessageText = null;
            }
            formData.MessageMedias ??= new();

            //查找数据库
            int? targetUser = await _userContext.UserAccounts.Select(account => account.UUID).FirstOrDefaultAsync(UUID => UUID == formData.ReceiverId);
            if (targetUser == null)
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]正在尝试向不存在的用户[ {targetUser} ]发送消息", UUID, formData.ReceiverId);
                ResponseT<string> sendMessageFailed = new(2, "目标用户不存在");
                return Ok(sendMessageFailed);
            }

            if (formData.MessageReplied != null)
            {
                var messageReplied = await _messageContext.CommonMessages
                    .Select(message => new { message.Id, message.SenderId, message.ReceiverId })
                    .FirstOrDefaultAsync(m => m.Id == formData.MessageReplied);

                if (messageReplied == null || !((messageReplied.SenderId == UUID && messageReplied.ReceiverId == formData.ReceiverId) || (messageReplied.ReceiverId == UUID && messageReplied.SenderId == formData.ReceiverId)))
                {
                    _logger.LogWarning("Warning：用户[ {UUID} ]正在尝试回复一条不属于该用户与[ {targetUser} ]的消息[ {messageReplied} ]", UUID, formData.ReceiverId, formData.MessageReplied);
                    ResponseT<string> sendMessageFailed = new(3, "您正在尝试回复一条不属于该对话的消息");
                    return Ok(sendMessageFailed);
                }
            }

            if (formData.MessageText == null && formData.MessageMedias.Count == 0)
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]发送消息时失败，原因为消息文字内容与媒体文件内容同时为空", UUID);
                ResponseT<string> sendMessageFailed = new(4, "发送消息失败，文字内容与媒体文件内容不能同时为空");
                return Ok(sendMessageFailed);
            }

            if (formData.MessageMedias.Count > 9)
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]发送消息时失败，原因为用户上传了超过限制数量的媒体文件", UUID);
                ResponseT<string> sendMessageFailed = new(5, "发送消息失败，上传媒体文件数超过限制");
                return Ok(sendMessageFailed);
            }

            for (int i = 0; i < formData.MessageMedias.Count; i++)
            {
                if ((!formData.MessageMedias[i].File.ContentType.Contains("image") && !formData.MessageMedias[i].File.ContentType.Contains("video")) || (formData.MessageMedias[i].File.ContentType.Contains("video") && (formData.MessageMedias[i].PreviewImage == null || (formData.MessageMedias[i].PreviewImage != null && !formData.MessageMedias[i].PreviewImage!.ContentType.Contains("image")))))
                {
                    _logger.LogWarning("Warning：用户[ {UUID} ]发送消息时失败，原因为用户上传了图片或视频以外的媒体文件", UUID);
                    ResponseT<string> sendMessageFailed = new(6, "发送消息失败，禁止上传规定格式以外的文件");
                    return Ok(sendMessageFailed);
                }
            }

            var redis = _redisConnection.GetUserBlockListDatabase();
            if (redis.SetContains($"{formData.ReceiverId}BlockList", UUID))
            {
                ResponseT<string> sendMessageFailed = new(7, "发送消息失败，您已被对方屏蔽");
                return Ok(sendMessageFailed);
            }

            List<Task<bool>> tasks = new();
            List<MediaMetadata> medias = new();
            List<string> paths = new();
            for (int i = 0; i < formData.MessageMedias.Count; i++)
            {
                if (formData.MessageMedias[i].File.ContentType.Contains("image"))
                {
                    IFormFile file = formData.MessageMedias[i].File;

                    string extension = Path.GetExtension(file.FileName);

                    Stream stream = file.OpenReadStream();

                    string timestamp = (DateTime.Now - new DateTime(1970, 1, 1, 0, 0, 0)).TotalMilliseconds.ToString().Replace(".", "");

                    string fileName = UUID.ToString() + "_" + timestamp + extension;

                    paths.Add(fileName);

                    string url = _configuration["MinIO:CommonMessageMediasURLPrefix"]! + fileName;

                    tasks.Add(_commonMessageMediasMinIOService.UploadImageAsync(fileName, stream));

                    medias.Add(new MediaMetadata("image", url, formData.MessageMedias[i].AspectRatio, null, null));
                }
                else if (formData.MessageMedias[i].File.ContentType.Contains("video"))
                {
                    IFormFile file = formData.MessageMedias[i].File;

                    string extension = Path.GetExtension(file.FileName);

                    Stream stream = file.OpenReadStream();

                    DateTime now = DateTime.Now;

                    string timestamp = (now - new DateTime(1970, 1, 1, 0, 0, 0)).TotalMilliseconds.ToString().Replace(".", "");

                    string fileName = UUID.ToString() + "_" + timestamp + extension;

                    paths.Add(fileName);

                    string url = _configuration["MinIO:CommonMessageMediasURLPrefix"]! + fileName;

                    tasks.Add(_commonMessageMediasMinIOService.UploadVideoAsync(fileName, stream));

                    IFormFile preview = formData.MessageMedias[i].PreviewImage!;

                    string previewExtension = Path.GetExtension(preview.FileName);

                    Stream previewStream = preview.OpenReadStream();

                    string previewTimestamp = (now - new DateTime(1970, 1, 1, 0, 0, 0)).TotalMilliseconds.ToString().Replace(".", "");

                    string previewFileName = previewTimestamp + previewExtension;

                    paths.Add(previewFileName);

                    string previewURL = _configuration["MinIO:CommonMessageMediasURLPrefix"]! + previewFileName;

                    tasks.Add(_commonMessageMediasMinIOService.UploadImageAsync(previewFileName, previewStream));

                    medias.Add(new MediaMetadata("video", url, formData.MessageMedias[i].AspectRatio, previewURL, formData.MessageMedias[i].TimeTotal));
                }
            }

            Task.WaitAll(tasks.ToArray());
            bool isStoreMediasSucceed = true;
            foreach (var task in tasks)
            {
                if (!task.Result)
                {
                    isStoreMediasSucceed = false;
                    break;
                }
            }
            if (!isStoreMediasSucceed)
            {
                _ = _commonMessageMediasMinIOService.DeleteFilesAsync(paths);
                _logger.LogWarning("Warning：用户[ {UUID} ]发送消息时失败，MinIO存储媒体文件时发生错误。", UUID);
                ResponseT<string> sendMessageFailed = new(8, "发生错误，消息发送失败");
                return Ok(sendMessageFailed);
            }

            //使用事务
            //由于需要操作不同的数据库，故这里应当使用分布式事务，但.NET中的分布式事务目前只支持Windows平台上的.NET7.0，故暂时无法使用
            //这里的事务使用方法是错误的
            using var messageContextTransaction = _messageContext.Database.BeginTransaction();
            {
                using var userContextTransaction = _userContext.Database.BeginTransaction();
                try
                {
                    Entities.Message.CommonMessage message = new(id: 0, senderId: UUID, receiverId: formData.ReceiverId, createdTime: DateTime.Now, isCustom: false, isRecalled: false, isReply: formData.MessageReplied != null, isMediaMessage: medias.Count != 0, isVoiceMessage: false, customType: null, minimumSupportVersion: null, textOnError: null, customMessageContent: null, messageReplied: formData.MessageReplied, messageText: formData.MessageText, messageMedias: medias.Count == 0 ? null : JsonSerializer.Serialize(medias, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }), messageVoice: null);
                    _messageContext.CommonMessages.Add(message);

                    _messageContext.SaveChanges();

                    Entities.Chat.Chat? senderChat = await _messageContext.Chats.FirstOrDefaultAsync(chat => chat.UUID == UUID && chat.TargetId == formData.ReceiverId && chat.IsWithOtherUser);
                    Entities.Chat.Chat? receiverChat = await _messageContext.Chats.FirstOrDefaultAsync(chat => chat.UUID == formData.ReceiverId && chat.TargetId == UUID && chat.IsWithOtherUser);
                    int senderChatId;
                    int receiverChatId;
                    if (senderChat == null)
                    {
                        Entities.Chat.Chat newSenderChat = new(id: 0, UUID: UUID, targetId: formData.ReceiverId, isWithOtherUser: true, isWithGroup: false, isWithSystem: false, isStickyOnTop: false, isDeleted: false, numberOfUnreadMessages: 0, lastMessageId: message.Id, updatedTime: message.CreatedTime);
                        _messageContext.Chats.Add(newSenderChat);
                        _messageContext.SaveChanges();

                        senderChatId = newSenderChat.Id;

                        Entities.Chat.Chat newReceiverChat = new(id: 0, UUID: formData.ReceiverId, targetId: UUID, isWithOtherUser: true, isWithGroup: false, isWithSystem: false, isStickyOnTop: false, isDeleted: false, numberOfUnreadMessages: 1, lastMessageId: message.Id, updatedTime: message.CreatedTime);
                        _messageContext.Chats.Add(newReceiverChat);
                        _messageContext.SaveChanges();

                        receiverChatId = newReceiverChat.Id;

                        CommonChatStatus newSenderChatStatus = new(id: 0, UUID: newSenderChat.UUID, chatId: senderChatId, targetUserChatId: receiverChatId, lastMessageSendByMe: message.Id, lastMessageBeReadSendByMe: null, readTime: null, updatedTime: message.CreatedTime);
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

                    UserSyncTable? currentReceiverSyncTable = await _userContext.UserSyncTables.FirstOrDefaultAsync(table => table.UUID == formData.ReceiverId);
                    int newReceiverSequence = currentReceiverSyncTable!.SequenceForCommonMessages + 1;
                    CommonMessageInbox receiverInbox = new(id: 0, UUID: formData.ReceiverId, messageId: message.Id, chatId: receiverChatId, isDeleted: false, sequence: newReceiverSequence);

                    _messageContext.CommonMessageInboxes.Add(senderInbox);
                    _messageContext.CommonMessageInboxes.Add(receiverInbox);
                    _messageContext.SaveChanges();

                    currentSenderSyncTable.SequenceForCommonMessages++;
                    currentSenderSyncTable.UpdatedTimeForChats = message.CreatedTime;
                    currentReceiverSyncTable.SequenceForCommonMessages++;
                    currentReceiverSyncTable.UpdatedTimeForChats = message.CreatedTime;
                    _userContext.SaveChanges();

                    userContextTransaction.Commit();
                    messageContextTransaction.Commit();

                    CommonMessageDataForClient dataForReceiver = new(message.Id, receiverInbox.ChatId, message.SenderId, message.ReceiverId, message.CreatedTime, message.IsCustom, message.IsRecalled, false, message.IsReply, message.IsMediaMessage, message.IsVoiceMessage, message.CustomType, message.MinimumSupportVersion, message.TextOnError, message.CustomMessageContent, message.MessageReplied, message.MessageText, message.MessageMedias, message.MessageVoice, currentReceiverSyncTable.SequenceForCommonMessages);

                    //操作完数据库后，使用消息队列发送消息
                    //通过Redis查找目标用户上一次在哪一台服务器连接了WebSocket，尝试由那台服务器发送消息
                    string? webSocketPort = await _distributedCache.GetStringAsync(formData.ReceiverId + "WebSocket");
                    if (webSocketPort != null)
                    {
                        _messagePublisher.SendMessage(new { type = "NewCommonMessage", data = dataForReceiver }, "msg", webSocketPort);
                    }
                    else
                    {
                        //表明无法通过WebSocket发送此消息，需要将该消息视作发送失败，进入MongoDB中
                    }

                    //返回相关信息，供发送者的客户端更新数据库
                    CommonMessageDataForClient dataForSender = new(message.Id, senderInbox.ChatId, message.SenderId, message.ReceiverId, message.CreatedTime, message.IsCustom, message.IsRecalled, false, message.IsReply, message.IsMediaMessage, message.IsVoiceMessage, message.CustomType, message.MinimumSupportVersion, message.TextOnError, message.CustomMessageContent, message.MessageReplied, message.MessageText, message.MessageMedias, message.MessageVoice, currentSenderSyncTable.SequenceForCommonMessages);
                    ResponseT<CommonMessageDataForClient> sendMessageSucceed = new(0, "发送成功", dataForSender);
                    return Ok(sendMessageSucceed);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error：用户[ {UUID} ]在向[ {targetUser} ]发送消息时发生错误。报错信息为[ {ex} ]。", UUID, formData.ReceiverId, ex);
                    ResponseT<string> sendMessageFailed = new(9, "发生错误，消息发送失败");
                    return Ok(sendMessageFailed);
                }
            }
        }

        // 撤回某条消息
        // 需要写进CommonMessageInbox（双方）中，并通过WebSocket通知对方
        [HttpPut("recall/{messageId}")]
        public async Task<IActionResult> RecallCommonMessage([FromRoute] int messageId, [FromHeader] string JWT, [FromHeader] int UUID)
        {
            var message = await _messageContext.CommonMessages
                .FirstOrDefaultAsync(message => message.Id == messageId);

            if (message == null)
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]撤回消息时失败，用户正在尝试撤回一条不存在的消息[ {messageId} ]。", UUID, messageId);
                ResponseT<string> recallMessageFailed = new(2, "您正在尝试撤回一条不存在的消息");
                return Ok(recallMessageFailed);
            }

            if (message.SenderId != UUID)
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]撤回消息时失败，用户正在尝试撤回一条不属于该用户的消息[ {messageId} ]。", UUID, messageId);
                ResponseT<string> recallMessageFailed = new(3, "您无法撤回一条不是由您发送的消息");
                return Ok(recallMessageFailed);
            }

            if (message.IsRecalled)
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]撤回消息时失败，原因为该消息[ {messageId} ]在这之前就已被撤回。", UUID, messageId);
                ResponseT<string> recallMessageFailed = new(4, "您无法对已被撤回的消息再次撤回");
                return Ok(recallMessageFailed);
            }

            //使用事务
            //由于需要操作不同的数据库，故这里应当使用分布式事务，但.NET中的分布式事务目前只支持Windows平台上的.NET7.0，故暂时无法使用
            //这里的事务使用方法是错误的
            using var messageContextTransaction = _messageContext.Database.BeginTransaction();
            {
                using var userContextTransaction = _userContext.Database.BeginTransaction();
                try
                {
                    message.IsRecalled = true;
                    _messageContext.SaveChanges();

                    int senderChatId = _messageContext.Chats
                        .Select(chat => new { chat.Id, chat.UUID, chat.TargetId, chat.IsWithOtherUser })
                        .Single(chat => chat.UUID == UUID && chat.TargetId == message.ReceiverId && chat.IsWithOtherUser)
                        .Id;
                    int receiverChatId = _messageContext.Chats
                        .Select(chat => new { chat.Id, chat.UUID, chat.TargetId, chat.IsWithOtherUser })
                        .Single(chat => chat.UUID == message.ReceiverId && chat.TargetId == UUID && chat.IsWithOtherUser)
                        .Id;

                    UserSyncTable? currentSenderSyncTable = await _userContext.UserSyncTables.FirstOrDefaultAsync(table => table.UUID == UUID);
                    int newSenderSequence = currentSenderSyncTable!.SequenceForCommonMessages + 1;
                    CommonMessageInbox senderInbox = new(id: 0, UUID: UUID, messageId: message.Id, chatId: senderChatId, isDeleted: false, sequence: newSenderSequence);

                    UserSyncTable? currentReceiverSyncTable = await _userContext.UserSyncTables.FirstOrDefaultAsync(table => table.UUID == message.ReceiverId);
                    int newReceiverSequence = currentReceiverSyncTable!.SequenceForCommonMessages + 1;
                    CommonMessageInbox receiverInbox = new(id: 0, UUID: message.ReceiverId, messageId: message.Id, chatId: receiverChatId, isDeleted: false, sequence: newReceiverSequence);

                    _messageContext.CommonMessageInboxes.Add(senderInbox);
                    _messageContext.CommonMessageInboxes.Add(receiverInbox);
                    _messageContext.SaveChanges();

                    currentSenderSyncTable.SequenceForCommonMessages++;
                    currentReceiverSyncTable.SequenceForCommonMessages++;
                    _userContext.SaveChanges();

                    userContextTransaction.Commit();
                    messageContextTransaction.Commit();

                    CommonMessageDataForClient dataForReceiver = new(message.Id, receiverInbox.ChatId, message.SenderId, message.ReceiverId, message.CreatedTime, message.IsCustom, message.IsRecalled, false, message.IsReply, message.IsMediaMessage, message.IsVoiceMessage, message.CustomType, message.MinimumSupportVersion, message.TextOnError, message.CustomMessageContent, message.MessageReplied, message.MessageText, message.MessageMedias, message.MessageVoice, currentReceiverSyncTable.SequenceForCommonMessages);

                    //操作完数据库后，使用消息队列发送消息
                    //通过Redis查找目标用户上一次在哪一台服务器连接了WebSocket，尝试由那台服务器发送消息
                    string? webSocketPort = await _distributedCache.GetStringAsync(message.ReceiverId + "WebSocket");
                    if (webSocketPort != null)
                    {
                        _messagePublisher.SendMessage(new { type = "CommonMessageBeRecalled", data = dataForReceiver }, "msg", webSocketPort);
                    }
                    else
                    {
                        //表明无法通过WebSocket发送此消息，需要将该消息视作发送失败，进入MongoDB中
                    }

                    //返回相关信息，供撤回消息者的客户端更新数据库
                    CommonMessageDataForClient dataForSender = new(message.Id, senderInbox.ChatId, message.SenderId, message.ReceiverId, message.CreatedTime, message.IsCustom, message.IsRecalled, false, message.IsReply, message.IsMediaMessage, message.IsVoiceMessage, message.CustomType, message.MinimumSupportVersion, message.TextOnError, message.CustomMessageContent, message.MessageReplied, message.MessageText, message.MessageMedias, message.MessageVoice, currentSenderSyncTable.SequenceForCommonMessages);
                    ResponseT<CommonMessageDataForClient> recallMessageSucceed = new(0, "撤回消息成功", dataForSender);
                    return Ok(recallMessageSucceed);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error：用户[ {UUID} ]在撤回消息[ {messageId} ]时发生错误。报错信息为[ {ex} ]。", UUID, messageId, ex);
                    ResponseT<string> recallMessageFailed = new(5, "发生错误，消息撤回失败");
                    return Ok(recallMessageFailed);
                }
            }
        }

        // 删除某条消息（只对自己这边进行删除，不会通知对方，对方仍能看到此消息）
        // 需要写进CommonMessageInbox（只有自己这边）中
        [HttpDelete("{messageId}")]
        public async Task<IActionResult> DeleteCommonMessage([FromRoute] int messageId, [FromHeader] string JWT, [FromHeader] int UUID)
        {
            var message = await _messageContext.CommonMessages
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
                        .Select(chat => new { chat.Id, chat.UUID, chat.TargetId, chat.IsWithOtherUser })
                        .Single(chat => chat.UUID == UUID && chat.TargetId == targetId && chat.IsWithOtherUser)
                        .Id;

                    UserSyncTable? currentUserSyncTable = await _userContext.UserSyncTables.FirstOrDefaultAsync(table => table.UUID == UUID);
                    int newUserSequence = currentUserSyncTable!.SequenceForCommonMessages + 1;
                    CommonMessageInbox userInbox = new(id: 0, UUID: UUID, messageId: message.Id, chatId: userChatId, isDeleted: true, sequence: newUserSequence);

                    _messageContext.CommonMessageInboxes.Add(userInbox);
                    _messageContext.SaveChanges();

                    currentUserSyncTable.SequenceForCommonMessages++;
                    _userContext.SaveChanges();

                    userContextTransaction.Commit();
                    messageContextTransaction.Commit();

                    //返回相关信息，供删除消息者的客户端更新数据库
                    CommonMessageDataForClient dataForSender = new(message.Id, userInbox.ChatId, message.SenderId, message.ReceiverId, message.CreatedTime, message.IsCustom, message.IsRecalled, true, message.IsReply, message.IsMediaMessage, message.IsVoiceMessage, message.CustomType, message.MinimumSupportVersion, message.TextOnError, message.CustomMessageContent, message.MessageReplied, message.MessageText, message.MessageMedias, message.MessageVoice, currentUserSyncTable.SequenceForCommonMessages);
                    ResponseT<CommonMessageDataForClient> deleteMessageSucceed = new(0, "删除消息成功", dataForSender);
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
