using Consul;
using Consul.AspNetCore;
using Microsoft.EntityFrameworkCore;
using MiniApp.API.DataCollection.Introduction;
using MiniApp.API.DataCollection.MiniApp;
using MiniApp.API.DataCollection.Review;
using MiniApp.API.Filters;
using MiniApp.API.MongoDBServices.Introduction;
using MiniApp.API.MongoDBServices.MiniApp;
using MiniApp.API.MongoDBServices.Review;
using MiniApp.API.Redis;
using MiniApp.API.TrendManager;
using Serilog;
using User.API.DataContext.User;

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

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

//配置DataCollection
builder.Services.Configure<MiniAppCollectionSettings>(
    builder.Configuration.GetSection("MiniAppCollection"));
builder.Services.Configure<MiniAppIntroductionCollectionSettings>(
    builder.Configuration.GetSection("MiniAppIntroductionCollection"));
builder.Services.Configure<MiniAppReviewCollectionSettings>(
    builder.Configuration.GetSection("MiniAppReviewCollection"));

builder.Services.AddSingleton<MiniAppService>();
builder.Services.AddSingleton<MiniAppIntroductionService>();
builder.Services.AddSingleton<MiniAppReviewService>();

//配置Redis
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});

//配置Redis
builder.Services.AddSingleton<RedisConnection>();

//配置TrendManager
builder.Services.AddSingleton<TrendManager>();

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

//app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
