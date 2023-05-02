using Microsoft.Extensions.Caching.Distributed;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text;

namespace WebSocket.API.RabbitMQ
{
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
    public class MsgConsumer
    {
        //依赖注入
        private readonly IConfiguration _configuration;
        private readonly IDistributedCache _distributedCache;
        private readonly WebSocketsManager _webSocketsManager;

        private IModel channel;

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
                                //表明无法通过WebSocket发送此消息，需要将该消息视作发送失败，进入MongoDB中
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
                                            //发送失败的话，需要将该消息送入MongoDB中
                                            //Ack逻辑还没有写
                                        }
                                    }
                                    else
                                    {
                                        //表明无法通过WebSocket发送此消息，需要将该消息视作发送失败，进入MongoDB中
                                    }
                                }
                                else
                                {
                                    //表明无法通过WebSocket发送此消息，需要将该消息视作发送失败，进入MongoDB中
                                }
                            }
                            break;
                        }
                    default: { break; }
                }
                //理一下逻辑，客户端调用HTTP请求发送消息，首先在对应的Message控制器下处理该HTTP请求
                //然后Message控制器负责进行数据库的存取（使用事务），存取完成后，使用MessagePublisher发布一条消息进队列
                //发布消息进入队列后，被MessageConsumer消费，判断一下WebSocket的状态（即用户是否在线）
                //确认用户在线后，使用WebSocket向指定用户发送消息，向队列返回Ack
                //用户不在线，则不发送消息，但会将消息存入MongoDB（存在过期时间），并且视作将消息队列中的消息消费（向队列返回Ack）
                //若使用WebSocket发送消息，但失败了，用户未接收到消息，则也将该消息存入MongoDB

                //对于消息机制，客户端先发送同步请求，同步完成后再开启WebSocket
                //由于用户同步完成后才会开启WebSocket，故在用户完成同步操作与开启WebSocket之间的时间间隙里，可能会有发送给用户的消息产生，但无法通过WebSocket送达
                //或者出于某种原因，消息通过WebSocket发送失败了，用户未通过WebSocket接收到某条消息
                //以上两种情况，通过WebSocket发送失败的消息，都将被存入MongoDB中
                //开启WebSocket后，客户端会先请求查看MongoDB中是否有未发送给用户的消息
                //由对应的Message控制器处理用户对MongoDB中消息的同步请求
                //并且来自MongoDB的消息将与正常流程产生的消息一样，通过WebSocket发送
                //通过WebSoccket发送失败的消息，进入MongoDB中
                //客户端会对来自WebSocket的消息进行校验后再存入客户端数据库中
            };
            channel.BasicConsume(queue: queueName, autoAck: true, consumer: consumer);

        }
    }
}
