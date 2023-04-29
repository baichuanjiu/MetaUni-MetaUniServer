using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using System.Net.WebSockets;
using WebSocket.API.Filters;
using WebSocket.API.RabbitMQ;

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
        private readonly MsgConsumer _msgConsumer;
        private readonly ILogger<WebSocketController> _logger;

        public WebSocketController(WebSocketsManager webSocketsManager, IDistributedCache distributedCache, IConfiguration configuration, MsgConsumer msgConsumer, ILogger<WebSocketController> logger)
        {
            _webSocketsManager = webSocketsManager;
            _distributedCache = distributedCache;
            _configuration = configuration;
            _msgConsumer = msgConsumer;
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
                //测试用，用于测试该WebSocket是否正常连接（会将客户端发送的信息原样返回）
                //平时会被注释掉
                await webSocket.SendAsync(
                    new ArraySegment<byte>(buffer, 0, receiveResult.Count),
                    receiveResult.MessageType,
                    receiveResult.EndOfMessage,
                    CancellationToken.None);

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
