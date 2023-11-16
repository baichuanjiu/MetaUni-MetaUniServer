using Microsoft.Extensions.Caching.Distributed;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text;
using WebSocket.API.ReusableClass;
using Message.API.Entities.Chat;

namespace WebSocket.API.RabbitMQ
{
    public class MsgConsumer
    {
        //依赖注入
        private readonly IConfiguration _configuration;
        private readonly IDistributedCache _distributedCache;
        private readonly WebSocketsManager _webSocketsManager;

        private readonly IModel channel;

        public MsgConsumer(IConfiguration configuration, IDistributedCache distributedCache, WebSocketsManager webSocketsManager)
        {
            _configuration = configuration;
            _distributedCache = distributedCache;
            _webSocketsManager = webSocketsManager;

            var factory = new ConnectionFactory
            {
                HostName = _configuration["RabbitMQ:HostName"]!,
                UserName = _configuration["RabbitMQ:UserName"]!,
                Password = _configuration["RabbitMQ:Password"]!,
                Port = int.Parse(_configuration["RabbitMQ:Port"]!)
            };

            var connection = factory.CreateConnection();
            channel = connection.CreateModel();
            channel.ExchangeDeclare("msg", type: ExchangeType.Direct);
            string queueName = "msg" + _configuration["Consul:Port"]!;
            channel.QueueDeclare(queueName, exclusive: false);
            channel.QueueBind(queue: queueName, exchange: "msg", routingKey: _configuration["Consul:Port"]!);
            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += async (model, eventArgs) =>
            {
                var body = eventArgs.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                Dictionary<string, dynamic>? json = JsonSerializer.Deserialize<Dictionary<string, dynamic>>(message);

                string type = JsonSerializer.Deserialize<string>(json!["type"]);

                switch (type)
                {
                    case "NewCommonMessage":
                        {
                            CommonMessageDataForClient commonMessageData = JsonSerializer.Deserialize<CommonMessageDataForClient>(json["data"].ToString());
                            string? ReceiverJWT = await _distributedCache.GetStringAsync(commonMessageData.ReceiverId.ToString());
                            if (ReceiverJWT == null)
                            {
                                //表明无法通过WebSocket发送此消息
                            }
                            else
                            {
                                int UUID = commonMessageData.ReceiverId;
                                string JWT = ReceiverJWT;
                                if (_webSocketsManager.webSockets.ContainsKey(UUID))
                                {
                                    if (_webSocketsManager.webSockets[UUID].TryGetValue(JWT, out System.Net.WebSockets.WebSocket? webSocket))
                                    {
                                        if (webSocket.State == WebSocketState.Open)
                                        {
                                            var sendDataJson = JsonSerializer.Serialize(new { type = "NewCommonMessage", data = commonMessageData }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                                            var sendDataBytes = Encoding.UTF8.GetBytes(sendDataJson);
                                            var sendData = new ArraySegment<byte>(sendDataBytes);
                                            _ = webSocket.SendAsync(sendData, WebSocketMessageType.Text, true, CancellationToken.None);
                                            //需要检查是否发送成功，即客户端接收到webSocket发送的消息后需要返回Ack进行确认
                                            //Ack逻辑还没有写
                                        }
                                    }
                                    else
                                    {
                                        //表明无法通过WebSocket发送此消息
                                    }
                                }
                                else
                                {
                                    //表明无法通过WebSocket发送此消息
                                }
                            }
                            break;
                        }
                    case "CommonMessagesBeRead":
                        {
                            CommonChatStatus commonChatStatus = JsonSerializer.Deserialize<CommonChatStatus>(json["data"].ToString());
                            string? ReceiverJWT = await _distributedCache.GetStringAsync(commonChatStatus.UUID.ToString());
                            if (ReceiverJWT == null)
                            {
                                //表明无法通过WebSocket发送此消息
                            }
                            else
                            {
                                int UUID = commonChatStatus.UUID;
                                string JWT = ReceiverJWT;
                                if (_webSocketsManager.webSockets.ContainsKey(UUID))
                                {
                                    if (_webSocketsManager.webSockets[UUID].TryGetValue(JWT, out System.Net.WebSockets.WebSocket? webSocket))
                                    {
                                        if (webSocket.State == WebSocketState.Open)
                                        {
                                            CommonChatStatusDataForClient commonChatStatusDataForClient = new(commonChatStatus.ChatId,commonChatStatus.LastMessageBeReadSendByMe,commonChatStatus.ReadTime,commonChatStatus.UpdatedTime);
                                            var sendDataJson = JsonSerializer.Serialize(new { type = "CommonMessagesBeRead", data = commonChatStatusDataForClient }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                                            var sendDataBytes = Encoding.UTF8.GetBytes(sendDataJson);
                                            var sendData = new ArraySegment<byte>(sendDataBytes);
                                            _ = webSocket.SendAsync(sendData, WebSocketMessageType.Text, true, CancellationToken.None);
                                            //需要检查是否发送成功，即客户端接收到webSocket发送的消息后需要返回Ack进行确认
                                            //Ack逻辑还没有写
                                        }
                                    }
                                    else
                                    {
                                        //表明无法通过WebSocket发送此消息
                                    }
                                }
                                else
                                {
                                    //表明无法通过WebSocket发送此消息
                                }
                            }
                            break;
                        }
                    case "CommonMessageBeRecalled":
                        {
                            CommonMessageDataForClient commonMessageData = JsonSerializer.Deserialize<CommonMessageDataForClient>(json["data"].ToString());
                            string? ReceiverJWT = await _distributedCache.GetStringAsync(commonMessageData.ReceiverId.ToString());
                            if (ReceiverJWT == null)
                            {
                                //表明无法通过WebSocket发送此消息
                            }
                            else
                            {
                                int UUID = commonMessageData.ReceiverId;
                                string JWT = ReceiverJWT;
                                if (_webSocketsManager.webSockets.ContainsKey(UUID))
                                {
                                    if (_webSocketsManager.webSockets[UUID].TryGetValue(JWT, out System.Net.WebSockets.WebSocket? webSocket))
                                    {
                                        if (webSocket.State == WebSocketState.Open)
                                        {
                                            var sendDataJson = JsonSerializer.Serialize(new { type = "CommonMessageBeRecalled", data = commonMessageData }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                                            var sendDataBytes = Encoding.UTF8.GetBytes(sendDataJson);
                                            var sendData = new ArraySegment<byte>(sendDataBytes);
                                            _ = webSocket.SendAsync(sendData, WebSocketMessageType.Text, true, CancellationToken.None);
                                            //需要检查是否发送成功，即客户端接收到webSocket发送的消息后需要返回Ack进行确认
                                            //Ack逻辑还没有写
                                        }
                                    }
                                    else
                                    {
                                        //表明无法通过WebSocket发送此消息
                                    }
                                }
                                else
                                {
                                    //表明无法通过WebSocket发送此消息
                                }
                            }
                            break;
                        }
                    case "NewSystemMessage":
                        {
                            SystemMessageDataForClient systemMessageData = JsonSerializer.Deserialize<SystemMessageDataForClient>(json["data"].ToString());
                            string? ReceiverJWT = await _distributedCache.GetStringAsync(systemMessageData.ReceiverId.ToString());
                            if (ReceiverJWT == null)
                            {
                                //表明无法通过WebSocket发送此消息
                            }
                            else
                            {
                                int UUID = systemMessageData.ReceiverId;
                                string JWT = ReceiverJWT;
                                if (_webSocketsManager.webSockets.ContainsKey(UUID))
                                {
                                    if (_webSocketsManager.webSockets[UUID].TryGetValue(JWT, out System.Net.WebSockets.WebSocket? webSocket))
                                    {
                                        if (webSocket.State == WebSocketState.Open)
                                        {
                                            var sendDataJson = JsonSerializer.Serialize(new { type = "NewSystemMessage", data = systemMessageData }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                                            var sendDataBytes = Encoding.UTF8.GetBytes(sendDataJson);
                                            var sendData = new ArraySegment<byte>(sendDataBytes);
                                            _ = webSocket.SendAsync(sendData, WebSocketMessageType.Text, true, CancellationToken.None);
                                            //需要检查是否发送成功，即客户端接收到webSocket发送的消息后需要返回Ack进行确认
                                            //Ack逻辑还没有写
                                        }
                                    }
                                    else
                                    {
                                        //表明无法通过WebSocket发送此消息
                                    }
                                }
                                else
                                {
                                    //表明无法通过WebSocket发送此消息
                                }
                            }
                            break;
                        }
                    default: { break; }
                }
            };
            channel.BasicConsume(queue: queueName, autoAck: true, consumer: consumer);

        }
    }
}
