using Message.API.Controllers.CommonMessage;
using Message.API.DataContext.Message;
using Message.API.Entities.Chat;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
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
        private readonly MessageContext _messageContext;
        private readonly ILogger<WebSocketController> _logger;

        public WebSocketController(WebSocketsManager webSocketsManager, IDistributedCache distributedCache, IConfiguration configuration, IMessagePublisher messagePublisher, MsgConsumer msgConsumer, MessageContext messageContext, ILogger<WebSocketController> logger)
        {
            _webSocketsManager = webSocketsManager;
            _distributedCache = distributedCache;
            _configuration = configuration;
            _messagePublisher = messagePublisher;
            _msgConsumer = msgConsumer;
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
                    _ = Task.Run(async () =>
                    {
                        await oldWebSocket.CloseAsync(WebSocketCloseStatus.Empty, "同一JWT多次连接", CancellationToken.None);
                    });

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
            var receiveResult = await webSocket.ReceiveAsync(
                new ArraySegment<byte>(buffer), CancellationToken.None);

            while (!receiveResult.CloseStatus.HasValue)
            {
                ////测试用，用于测试该WebSocket是否正常连接（会将客户端发送的信息原样返回）
                ////平时会被注释掉
                //await webSocket.SendAsync(
                //    new ArraySegment<byte>(buffer, 0, receiveResult.Count),
                //    receiveResult.MessageType,
                //    receiveResult.EndOfMessage,
                //    CancellationToken.None);

                //receiveResult = await webSocket.ReceiveAsync(
                //    new ArraySegment<byte>(buffer), CancellationToken.None);

                if (receiveResult.MessageType == WebSocketMessageType.Text) 
                {
                    string message = Encoding.UTF8.GetString(new ArraySegment<byte>(buffer, 0, receiveResult.Count));
                    Dictionary<string, dynamic>? json = JsonSerializer.Deserialize<Dictionary<string, dynamic>>(message);
                    string type = JsonSerializer.Deserialize<string>(json!["type"]);

                    switch (type) 
                    {
                        case "ReadMessages":
                            ReadMessagesRequestData readMessagesRequestData = JsonSerializer.Deserialize<ReadMessagesRequestData>(json["data"].ToString())!;
                            //记得先验证UUID和JWT，还没做

                            //在这里处理读取消息操作
                            //将对应Chat的未读消息数清空
                            //对应CommonChatStatus的状态更新
                            //然后尝试向双方发送WebSocket消息
                            //对于请求方可以直接通过该WebSocket发送请求成功的回执
                            //另一方则应当通过消息队列的方式发送“对方已读消息”的消息
                            Chat? chat = await _messageContext.Chats.FirstOrDefaultAsync(chat => chat.Id == readMessagesRequestData.ChatId);
                            if (chat != null)
                            {
                                chat.NumberOfUnreadMessages = 0;
                                CommonChatStatus commonChatStatus = (await _messageContext.CommonChatStatuses.FirstOrDefaultAsync(status => status.TargetUserChatId == readMessagesRequestData.ChatId))!;
                                commonChatStatus.IsRead = true;
                                DateTime currentTime = DateTime.Now;
                                commonChatStatus.ReadTime = currentTime;
                                commonChatStatus.UpdatedTime = currentTime;
                                _messageContext.SaveChanges();
                                _messageContext.Entry(chat).State = EntityState.Detached;
                                _messageContext.Entry(commonChatStatus).State = EntityState.Detached;

                                //操作完数据库后，使用消息队列发送消息
                                //通过Redis查找目标用户上一次在哪一台服务器连接了WebSocket，尝试由那台服务器发送消息
                                string? webSocketPort = await _distributedCache.GetStringAsync(commonChatStatus.UUID + "WebSocket");
                                if (webSocketPort != null)
                                {
                                    _messagePublisher.SendMessage(new { type = "MessagesBeRead", data = commonChatStatus }, "msg", webSocketPort);
                                }
                                else
                                {
                                    //表明无法通过WebSocket发送此消息，需要将该消息视作发送失败，进入MongoDB中
                                }

                                ReadMessagesResponseData readMessagesResponseData = new(chatId: readMessagesRequestData.ChatId);
                                var sendDataJson = JsonSerializer.Serialize(new { type = "ReadMessagesSucceed", data = readMessagesResponseData }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                                byte[] bytes = Encoding.UTF8.GetBytes(sendDataJson);
                                ArraySegment<byte> sendData = new(bytes);

                                await webSocket.SendAsync(
                                    sendData,
                                    WebSocketMessageType.Text,
                                    true,
                                    CancellationToken.None);
                            }

                            break;
                    }

                }
                
                receiveResult = await webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer), CancellationToken.None);

            }

            _webSocketsManager.webSockets[UUID].Remove(JWT);
            if (_webSocketsManager.webSockets[UUID].Count == 0)
            {
                _webSocketsManager.webSockets.Remove(UUID);
            }

            await webSocket.CloseAsync(
                receiveResult.CloseStatus.Value,
                receiveResult.CloseStatusDescription,
                CancellationToken.None);

            //从Redis中删除用户WebSocket所连接的服务器信息（WebSocket关闭连接）
            await _distributedCache.RemoveAsync(UUID + "WebSocket");
        }
    }
}
