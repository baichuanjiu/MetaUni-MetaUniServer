using Message.API.DataContext.Message;
using Message.API.Entities.Chat;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using User.API.DataContext.User;
using WebSocket.API.Filters;
using WebSocket.API.RabbitMQ;
using WebSocket.API.ReusableClass;

namespace WebSocket.API.Controllers
{
    [ApiController]
    [Route("/ws")]
    [ServiceFilter(typeof(JWTAuthFilterService), IsReusable = true)]
    public class WebSocketController : Controller
    {
        //依赖注入
        private readonly WebSocketsManager _webSocketsManager;
        private readonly IDistributedCache _distributedCache;
        private readonly IConfiguration _configuration;
        private readonly IMessagePublisher _messagePublisher;
        private readonly MsgConsumer _msgConsumer;
        private readonly FriendConsumer _friendConsumer;
        private readonly UserContext _userContext;
        private readonly MessageContext _messageContext;
        private readonly ILogger<WebSocketController> _logger;

        public WebSocketController(WebSocketsManager webSocketsManager, IDistributedCache distributedCache, IConfiguration configuration, IMessagePublisher messagePublisher, MsgConsumer msgConsumer, FriendConsumer friendConsumer, UserContext userContext, MessageContext messageContext, ILogger<WebSocketController> logger)
        {
            _webSocketsManager = webSocketsManager;
            _distributedCache = distributedCache;
            _configuration = configuration;
            _messagePublisher = messagePublisher;
            _msgConsumer = msgConsumer;
            _friendConsumer = friendConsumer;
            _userContext = userContext;
            _messageContext = messageContext;
            _logger = logger;
        }

        //连接WebSocket
        [HttpGet]
        public async Task ConnectWebSocket([FromHeader] int UUID, [FromHeader] string JWT)
        {
            if (HttpContext.WebSockets.IsWebSocketRequest)
            {
                if (!_webSocketsManager.webSockets.ContainsKey(UUID))
                {
                    _webSocketsManager.webSockets.Add(UUID, new Dictionary<string, System.Net.WebSockets.WebSocket>());
                }
                if (!_webSocketsManager.webSockets[UUID].ContainsKey(JWT))
                {
                    //连接后需要向同一用户建立的其它WebSocket连接发送关闭请求（如果存在的话）
                    //类似于“您的账号已在另一台设备上登录，如非本人操作……”
                    //可通过消息队列广播
                    //暂时还没做
                    using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
                    _webSocketsManager.webSockets[UUID].Add(JWT, webSocket);

                    //将用户WebSocket所连接的服务器信息（端口）存入Redis，消息队列需要用到此信息确定由哪台服务器消费消息
                    await _distributedCache.SetStringAsync(UUID + "WebSocket", _configuration["Consul:Port"]!);

                    await MaintainConnection(UUID, JWT, webSocket);
                }
                else
                {
                    //连接后需要向同一用户建立的其它WebSocket连接发送关闭请求（如果存在的话）
                    //类似于“您的账号已在另一台设备上登录，如非本人操作……”
                    //可通过消息队列广播
                    //暂时还没做
                    var oldWebSocket = _webSocketsManager.webSockets[UUID][JWT];
                    _ = oldWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "同一JWT多次连接", CancellationToken.None);

                    using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
                    _webSocketsManager.webSockets[UUID][JWT] = webSocket;

                    //将用户WebSocket所连接的服务器信息（端口）存入Redis，消息队列需要用到此信息确定由哪台服务器消费消息
                    await _distributedCache.SetStringAsync(UUID + "WebSocket", _configuration["Consul:Port"]!);

                    await MaintainConnection(UUID, JWT, webSocket);
                }
            }
            else
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]未使用ws或wss协议，尝试获取WebSocket连接。", UUID);
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
        }

        //负责维持WebSocket的连接
        private async Task MaintainConnection(int UUID, string JWT, System.Net.WebSockets.WebSocket webSocket)
        {
            var buffer = new byte[1024 * 4];

            try {
                WebSocketReceiveResult receiveResult;

                do {
                    ////测试用，用于测试该WebSocket是否正常连接（会将客户端发送的信息原样返回）
                    ////平时会被注释掉
                    //receiveResult = await webSocket.ReceiveAsync(
                    //    new ArraySegment<byte>(buffer), CancellationToken.None);

                    //await webSocket.SendAsync(
                    //    new ArraySegment<byte>(buffer, 0, receiveResult.Count),
                    //    receiveResult.MessageType,
                    //    receiveResult.EndOfMessage,
                    //    CancellationToken.None);

                    receiveResult = await webSocket.ReceiveAsync(
                new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (receiveResult.MessageType == WebSocketMessageType.Text)
                    {
                        string message = Encoding.UTF8.GetString(new ArraySegment<byte>(buffer, 0, receiveResult.Count));
                        Dictionary<string, dynamic>? json = JsonSerializer.Deserialize<Dictionary<string, dynamic>>(message);
                        string type = JsonSerializer.Deserialize<string>(json!["type"]);
                        int uuid = JsonSerializer.Deserialize<int>(json["uuid"]);
                        string jwt = JsonSerializer.Deserialize<string>(json["jwt"]);

                        //验证JWT
                        string? currentJWT = await _distributedCache.GetStringAsync(uuid.ToString());
                        if (currentJWT != jwt)
                        {
                            _logger.LogWarning("Warning：用户[ {UUID} ]在通过WebSocket传输数据时使用了无效的JWT。", uuid);

                            _webSocketsManager.webSockets[UUID].Remove(JWT);
                            if (_webSocketsManager.webSockets[UUID].Count == 0)
                            {
                                _webSocketsManager.webSockets.Remove(UUID);
                            }

                            //从Redis中删除用户WebSocket所连接的服务器信息（WebSocket关闭连接）
                            _ = _distributedCache.RemoveAsync(UUID + "WebSocket");

                            _ = webSocket.CloseAsync(
                                WebSocketCloseStatus.NormalClosure,
                                "传递了无效的JWT，现关闭连接",
                                CancellationToken.None);

                            return;
                        }
                        else
                        {
                            string? sendDataJson;
                            byte[] bytes;
                            ArraySegment<byte> sendData;

                            switch (type)
                            {
                                case "ReadCommonMessages":
                                    ReadMessagesRequestData readCommonMessagesRequestData = JsonSerializer.Deserialize<ReadMessagesRequestData>(json["data"].ToString())!;

                                    //在这里处理读取消息操作
                                    //将对应Chat的未读消息数清空
                                    //对应CommonChatStatus的状态更新
                                    //然后尝试向双方发送WebSocket消息
                                    //对于请求方可以直接通过该WebSocket发送请求成功的回执
                                    //另一方则应当通过消息队列的方式发送“对方已读消息”的消息
                                    Chat? commonChat = await _messageContext.Chats.FirstOrDefaultAsync(chat => chat.Id == readCommonMessagesRequestData.ChatId && chat.UUID == uuid);
                                    if (commonChat != null)
                                    {
                                        commonChat.NumberOfUnreadMessages = 0;
                                        CommonChatStatus commonChatStatus = (await _messageContext.CommonChatStatuses.FirstOrDefaultAsync(status => status.TargetUserChatId == readCommonMessagesRequestData.ChatId))!;
                                        commonChatStatus.LastMessageBeReadSendByMe = commonChatStatus.LastMessageSendByMe;
                                        DateTime currentTime = DateTime.Now;
                                        commonChatStatus.ReadTime = currentTime;
                                        commonChatStatus.UpdatedTime = currentTime;
                                        _messageContext.SaveChanges();
                                        _messageContext.Entry(commonChat).State = EntityState.Detached;
                                        _messageContext.Entry(commonChatStatus).State = EntityState.Detached;

                                        //操作完数据库后，使用消息队列发送消息
                                        //通过Redis查找目标用户上一次在哪一台服务器连接了WebSocket，尝试由那台服务器发送消息
                                        string? webSocketPort = await _distributedCache.GetStringAsync(commonChatStatus.UUID + "WebSocket");
                                        if (webSocketPort != null)
                                        {
                                            _messagePublisher.SendMessage(new { type = "CommonMessagesBeRead", data = commonChatStatus }, "msg", webSocketPort);
                                        }
                                        else
                                        {
                                            //表明无法通过WebSocket发送此消息，需要将该消息视作发送失败，进入MongoDB中
                                        }

                                        ReadMessagesResponseData readMessagesResponseData = new(chatId: readCommonMessagesRequestData.ChatId);
                                        sendDataJson = JsonSerializer.Serialize(new { type = "ReadCommonMessagesSucceed", data = readMessagesResponseData }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                                        bytes = Encoding.UTF8.GetBytes(sendDataJson);
                                        sendData = new(bytes);

                                        await webSocket.SendAsync(
                                            sendData,
                                            WebSocketMessageType.Text,
                                            true,
                                            CancellationToken.None);
                                    }

                                    break;
                                case "ReadSystemMessages":
                                    ReadMessagesRequestData readSystemMessagesRequestData = JsonSerializer.Deserialize<ReadMessagesRequestData>(json["data"].ToString())!;

                                    //在这里处理读取消息操作
                                    //将对应Chat的未读消息数清空
                                    Chat? systemChat = await _messageContext.Chats.FirstOrDefaultAsync(chat => chat.Id == readSystemMessagesRequestData.ChatId && chat.UUID == uuid);
                                    if (systemChat != null)
                                    {
                                        systemChat.NumberOfUnreadMessages = 0;
                                        DateTime currentTime = DateTime.Now;
                                        _messageContext.SaveChanges();
                                        _messageContext.Entry(systemChat).State = EntityState.Detached;

                                        ReadMessagesResponseData readMessagesResponseData = new(chatId: readSystemMessagesRequestData.ChatId);
                                        sendDataJson = JsonSerializer.Serialize(new { type = "ReadSystemMessagesSucceed", data = readMessagesResponseData }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                                        bytes = Encoding.UTF8.GetBytes(sendDataJson);
                                        sendData = new(bytes);

                                        await webSocket.SendAsync(
                                            sendData,
                                            WebSocketMessageType.Text,
                                            true,
                                            CancellationToken.None);
                                    }

                                    break;
                                case "SyncCommonMessages":
                                    SyncMessagesRequestData syncCommonMessagesRequestData = JsonSerializer.Deserialize<SyncMessagesRequestData>(json["data"].ToString())!;

                                    //一次最多同步600条消息
                                    //向前冗余同步100条消息
                                    int commonMessagesSyncCount = 600;
                                    int commonMessagesRedundanceCount = 100;

                                    var commonMessagesDataList = await _messageContext.CommonMessageInboxes
                                        .Select(inbox => new { inbox.UUID, inbox.MessageId, inbox.ChatId, inbox.IsDeleted, inbox.Sequence })
                                        .Where(inbox => inbox.UUID == uuid && inbox.Sequence > syncCommonMessagesRequestData.Sequence - commonMessagesRedundanceCount)
                                        .Join(_messageContext.CommonMessages,
                                        inbox => inbox.MessageId, message => message.Id, (inbox, message) => new { message.Id, inbox.ChatId, message.SenderId, message.ReceiverId, message.CreatedTime, message.IsCustom, message.IsRecalled, inbox.IsDeleted, message.IsReply, message.IsMediaMessage, message.IsVoiceMessage, message.CustomType, message.MinimumSupportVersion, message.TextOnError, message.CustomMessageContent, message.MessageReplied, message.MessageText, message.MessageMedias, message.MessageVoice, inbox.Sequence })
                                        .OrderBy(data => data.Sequence)
                                        .Take(commonMessagesSyncCount)
                                        .GroupBy(data => data.ChatId)
                                        .ToListAsync();

                                    int commonMessagesDataCount = 0;
                                    foreach (var group in commonMessagesDataList)
                                    {
                                        commonMessagesDataCount += group.Count();
                                    }
                                    int commonMessagesNewSequence = syncCommonMessagesRequestData.Sequence - commonMessagesRedundanceCount + commonMessagesDataCount;
                                    bool commonMessagesHasMore = true;
                                    if (commonMessagesDataCount < commonMessagesSyncCount)
                                    {
                                        commonMessagesHasMore = false;
                                    }

                                    sendDataJson = JsonSerializer.Serialize(new { type = "SyncCommonMessagesSucceed", dataList = commonMessagesDataList, newSequence = commonMessagesNewSequence, hasMore = commonMessagesHasMore }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                                    bytes = Encoding.UTF8.GetBytes(sendDataJson);
                                    sendData = new(bytes);

                                    await webSocket.SendAsync(
                                        sendData,
                                        WebSocketMessageType.Text,
                                        true,
                                        CancellationToken.None);
                                    break;
                                case "SyncSystemMessages":
                                    SyncMessagesRequestData syncSystemMessagesRequestData = JsonSerializer.Deserialize<SyncMessagesRequestData>(json["data"].ToString())!;

                                    //一次最多同步600条消息
                                    //向前冗余同步100条消息
                                    int systemMessagesSyncCount = 600;
                                    int systemMessagesRedundanceCount = 100;

                                    var systemMessagesDataList = await _messageContext.SystemMessageInboxes
                                        .Select(inbox => new { inbox.UUID, inbox.MessageId, inbox.ChatId, inbox.IsDeleted, inbox.Sequence })
                                        .Where(inbox => inbox.UUID == uuid && inbox.Sequence > syncSystemMessagesRequestData.Sequence - systemMessagesRedundanceCount)
                                        .Join(_messageContext.SystemMessages,
                                        inbox => inbox.MessageId, message => message.Id, (inbox, message) => new { message.Id, inbox.ChatId, message.SenderId, message.ReceiverId, message.CreatedTime, message.IsCustom, message.IsRecalled, inbox.IsDeleted, message.IsReply, message.IsMediaMessage, message.IsVoiceMessage, message.CustomType, message.MinimumSupportVersion, message.TextOnError, message.CustomMessageContent, message.MessageReplied, message.MessageText, message.MessageMedias, message.MessageVoice, inbox.Sequence })
                                        .OrderBy(data => data.Sequence)
                                        .Take(systemMessagesSyncCount)
                                        .GroupBy(data => data.ChatId)
                                        .ToListAsync();

                                    int systemMessagesDataCount = 0;
                                    foreach (var group in systemMessagesDataList)
                                    {
                                        systemMessagesDataCount += group.Count();
                                    }
                                    int systemMessagesNewSequence = syncSystemMessagesRequestData.Sequence - systemMessagesRedundanceCount + systemMessagesDataCount;
                                    bool systemMessagesHasMore = true;
                                    if (systemMessagesDataCount < systemMessagesSyncCount)
                                    {
                                        systemMessagesHasMore = false;
                                    }

                                    sendDataJson = JsonSerializer.Serialize(new { type = "SyncSystemMessagesSucceed", dataList = systemMessagesDataList, newSequence = systemMessagesNewSequence, hasMore = systemMessagesHasMore }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                                    bytes = Encoding.UTF8.GetBytes(sendDataJson);
                                    sendData = new(bytes);

                                    await webSocket.SendAsync(
                                        sendData,
                                        WebSocketMessageType.Text,
                                        true,
                                        CancellationToken.None);
                                    break;
                            }
                        }
                    }
                } while (!receiveResult.CloseStatus.HasValue);

                _webSocketsManager.webSockets[UUID].Remove(JWT);
                if (_webSocketsManager.webSockets[UUID].Count == 0)
                {
                    _webSocketsManager.webSockets.Remove(UUID);
                }

                //从Redis中删除用户WebSocket所连接的服务器信息（WebSocket关闭连接）
                _ = _distributedCache.RemoveAsync(UUID + "WebSocket");

                _ = webSocket.CloseAsync(
                    receiveResult.CloseStatus.Value,
                    receiveResult.CloseStatusDescription,
                    CancellationToken.None);

            } catch (Exception ex) {
                _logger.LogError("Error：用户[ {UUID} ]在使用WebSocket进行通信时发生错误，报错信息为[ {ex} ]", UUID, ex);

                _webSocketsManager.webSockets[UUID].Remove(JWT);
                if (_webSocketsManager.webSockets[UUID].Count == 0)
                {
                    _webSocketsManager.webSockets.Remove(UUID);
                }

                //从Redis中删除用户WebSocket所连接的服务器信息（WebSocket关闭连接）
                _ = _distributedCache.RemoveAsync(UUID + "WebSocket");

                _ = webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure, "服务器内部错误",
                    CancellationToken.None);
            }
        }
    }
}
