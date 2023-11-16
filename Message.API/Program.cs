using Consul;
using Consul.AspNetCore;
using Message.API.DataCollection;
using Message.API.DataContext.Message;
using Message.API.Filters;
using Message.API.MinIO;
using Message.API.MongoDBServices;
using Message.API.RabbitMQ;
using Message.API.Redis;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Minio.AspNetCore;
using Serilog;
using User.API.DataContext.User;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//配置请求体最大容量
builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = int.MaxValue;
});

//配置接收Form最大长度
builder.Services.Configure<FormOptions>(option => {
    option.MultipartBodyLengthLimit = int.MaxValue;
});

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
builder.Services.AddDbContext<MessageContext>(options =>
  options.UseSqlServer(builder.Configuration.GetConnectionString("MessageContext")));

builder.Services.AddDbContext<UserContext>(options =>
  options.UseSqlServer(builder.Configuration.GetConnectionString("UserContext")));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

//配置DataCollection
builder.Services.Configure<StickerSeriesCollectionSettings>(
    builder.Configuration.GetSection("StickerSeriesCollection"));

builder.Services.AddSingleton<StickerSeriesService>();

//配置Redis
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});
builder.Services.AddSingleton<RedisConnection>();

//配置MinIO
builder.Services.AddMinio(options =>
{
    options.Endpoint = builder.Configuration["MinIO:Endpoint"]!;
    options.AccessKey = builder.Configuration["MinIO:AccessKey"]!;
    options.SecretKey = builder.Configuration["MinIO:SecretKey"]!;
});
builder.Services.AddSingleton<CommonMessageMediasMinIOService>();

//配置消息队列生产者（消息发布者）
builder.Services.AddScoped<IMessagePublisher, MessagePublisher>();

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

//确保数据库创建
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    var messageContext = services.GetRequiredService<MessageContext>();
    messageContext.Database.EnsureCreated();
}

//启用健康状态检查中间件
app.UseHealthChecks("/health");

//app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
