using Consul;
using Consul.AspNetCore;
using Message.API.DataContext.Message;
using Microsoft.EntityFrameworkCore;
using Serilog;
using User.API.DataContext.User;
using WebSocket.API;
using WebSocket.API.Filters;
using WebSocket.API.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//����Serilog
var configuration = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json").Build();
Log.Logger = new LoggerConfiguration().ReadFrom.Configuration(configuration).CreateLogger();
builder.Host.UseSerilog();

//��ӽ������
builder.Services.AddHealthChecks();

//����Consul
builder.Services.AddConsul(options => options.Address = new Uri(builder.Configuration["Consul:Address"]!));
builder.Services.AddConsulServiceRegistration(options =>
{
    options.Check = new AgentServiceCheck()
    {
        DeregisterCriticalServiceAfter = TimeSpan.FromSeconds(5), //����ֹͣ���к�೤ʱ���Զ�ע���÷���
        Interval = TimeSpan.FromSeconds(60), //���������
        HTTP = "http://" + builder.Configuration["Consul:IP"]! + ":" + builder.Configuration["Consul:Port"]! + "/health", //��������ַ
        Timeout = TimeSpan.FromSeconds(10), //��ʱʱ��
    };
    options.ID = builder.Configuration["Consul:ID"]!;
    options.Name = builder.Configuration["Consul:Name"]!;
    options.Address = builder.Configuration["Consul:IP"]!;
    options.Port = int.Parse(builder.Configuration["Consul:Port"]!);
});

//����DbContext
builder.Services.AddDbContext<UserContext>(options =>
  options.UseSqlServer(builder.Configuration.GetConnectionString("UserContext")));
builder.Services.AddDbContext<MessageContext>(options =>
  options.UseSqlServer(builder.Configuration.GetConnectionString("MessageContext")));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

//����Redis
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});

//����WebSocketsManager�������н������ӵ�WebSockets����Static��Դ���д���
builder.Services.AddSingleton<WebSocketsManager>();

//������Ϣ���������ߣ���Ϣ�����ߣ�
builder.Services.AddScoped<IMessagePublisher, MessagePublisher>();

//������Ϣ����������
builder.Services.AddScoped<MsgConsumer>();
builder.Services.AddScoped<FriendConsumer>();

//����Filters
builder.Services.AddScoped<JWTAuthFilterService>();

var app = builder.Build();

//ʹ��Serilog����������־
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//���ý���״̬����м��
app.UseHealthChecks("/health");

//����WebSocket
var webSocketOptions = new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromMinutes(2)
};
app.UseWebSockets(webSocketOptions);

//app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
