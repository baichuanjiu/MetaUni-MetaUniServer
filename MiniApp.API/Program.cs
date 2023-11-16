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

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

//����DataCollection
builder.Services.Configure<MiniAppCollectionSettings>(
    builder.Configuration.GetSection("MiniAppCollection"));
builder.Services.Configure<MiniAppIntroductionCollectionSettings>(
    builder.Configuration.GetSection("MiniAppIntroductionCollection"));
builder.Services.Configure<MiniAppReviewCollectionSettings>(
    builder.Configuration.GetSection("MiniAppReviewCollection"));

builder.Services.AddSingleton<MiniAppService>();
builder.Services.AddSingleton<MiniAppIntroductionService>();
builder.Services.AddSingleton<MiniAppReviewService>();

//����Redis
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});

//����Redis
builder.Services.AddSingleton<RedisConnection>();

//����TrendManager
builder.Services.AddSingleton<TrendManager>();

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

//app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
