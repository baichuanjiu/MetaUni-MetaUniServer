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

//�����������������
builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = int.MaxValue;
});

//���ý���Form��󳤶�
builder.Services.Configure<FormOptions>(option => {
    option.MultipartBodyLengthLimit = int.MaxValue;
});

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
builder.Services.AddDbContext<MessageContext>(options =>
  options.UseSqlServer(builder.Configuration.GetConnectionString("MessageContext")));

builder.Services.AddDbContext<UserContext>(options =>
  options.UseSqlServer(builder.Configuration.GetConnectionString("UserContext")));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

//����DataCollection
builder.Services.Configure<StickerSeriesCollectionSettings>(
    builder.Configuration.GetSection("StickerSeriesCollection"));

builder.Services.AddSingleton<StickerSeriesService>();

//����Redis
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});
builder.Services.AddSingleton<RedisConnection>();

//����MinIO
builder.Services.AddMinio(options =>
{
    options.Endpoint = builder.Configuration["MinIO:Endpoint"]!;
    options.AccessKey = builder.Configuration["MinIO:AccessKey"]!;
    options.SecretKey = builder.Configuration["MinIO:SecretKey"]!;
});
builder.Services.AddSingleton<CommonMessageMediasMinIOService>();

//������Ϣ���������ߣ���Ϣ�����ߣ�
builder.Services.AddScoped<IMessagePublisher, MessagePublisher>();

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

//ȷ�����ݿⴴ��
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    var messageContext = services.GetRequiredService<MessageContext>();
    messageContext.Database.EnsureCreated();
}

//���ý���״̬����м��
app.UseHealthChecks("/health");

//app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
