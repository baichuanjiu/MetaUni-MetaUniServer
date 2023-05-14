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

//配置Serilog
var configuration = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json").Build();
Log.Logger = new LoggerConfiguration().ReadFrom.Configuration(configuration).CreateLogger();
builder.Host.UseSerilog();

//添加健康检查
builder.Services.AddHealthChecks();

//配置Consul
builder.Services.AddConsul(options => options.Address = new Uri(builder.Configuration["Consul:Address"]!));
builder.Services.AddConsulServiceRegistration(options =>
{
    options.Check = new AgentServiceCheck()
    {
        DeregisterCriticalServiceAfter = TimeSpan.FromSeconds(5), //服务停止运行后多长时间自动注销该服务
        Interval = TimeSpan.FromSeconds(60), //心跳检查间隔
        HTTP = "http://" + builder.Configuration["Consul:IP"]! + ":" + builder.Configuration["Consul:Port"]! + "/health", //健康检查地址
        Timeout = TimeSpan.FromSeconds(10), //超时时间
    };
    options.ID = builder.Configuration["Consul:ID"]!;
    options.Name = builder.Configuration["Consul:Name"]!;
    options.Address = builder.Configuration["Consul:IP"]!;
    options.Port = int.Parse(builder.Configuration["Consul:Port"]!);
});

//配置DbContext
builder.Services.AddDbContext<UserContext>(options =>
  options.UseSqlServer(builder.Configuration.GetConnectionString("UserContext")));
builder.Services.AddDbContext<MessageContext>(options =>
  options.UseSqlServer(builder.Configuration.GetConnectionString("MessageContext")));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

//配置Redis
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});

//配置WebSocketsManager（将所有建立连接的WebSockets当作Static资源进行处理）
builder.Services.AddSingleton<WebSocketsManager>();

//配置消息队列生产者（消息发布者）
builder.Services.AddScoped<IMessagePublisher, MessagePublisher>();

//配置消息队列消费者
builder.Services.AddScoped<MsgConsumer>();
builder.Services.AddScoped<FriendConsumer>();

//配置Filters
builder.Services.AddScoped<JWTAuthFilterService>();

var app = builder.Build();

//使用Serilog处理请求日志
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//启用健康状态检查中间件
app.UseHealthChecks("/health");

//配置WebSocket
var webSocketOptions = new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromMinutes(2)
};
app.UseWebSockets(webSocketOptions);

//app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
