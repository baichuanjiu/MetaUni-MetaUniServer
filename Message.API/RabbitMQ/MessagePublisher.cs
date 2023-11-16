using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace Message.API.RabbitMQ
{
    public class MessagePublisher : IMessagePublisher
    {
        //依赖注入
        private readonly IConfiguration _configuration;

        private IModel channel;

        public MessagePublisher(IConfiguration configuration)
        {
            _configuration = configuration;
            var factory = new ConnectionFactory
            {
                HostName = _configuration["RabbitMQ:HostName"]!,
                UserName = _configuration["RabbitMQ:UserName"]!,
                Password = _configuration["RabbitMQ:Password"]!,
                Port = int.Parse(_configuration["RabbitMQ:Port"]!)
            };

            var connection = factory.CreateConnection();
            channel = connection.CreateModel();
            channel.ExchangeDeclare("msg",type: ExchangeType.Direct);
        }

        public void SendMessage<T>(T message,string exchangeName,string routingKey)
        {
            var json = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(json);

            channel.BasicPublish(exchange: exchangeName, routingKey: routingKey, body: body);
        }
    }
}
