using Serilog;
using Serilog.Events;
using StackExchange.Redis;
using StajDesk.Infrastructure;
using StajDesk.Infrastructure.Data;
using StajDesk.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true).AddEnvironmentVariables();
SecretGuard.EnsureConfigured(builder.Configuration, "ConnectionStrings:Postgres", "RabbitMq:Password");

builder.Services.AddSerilog((_, lc) => lc
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "StajDesk.Worker")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.Seq(builder.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341"));

builder.Services.AddSingleton<ICurrentUser, SystemUser>();
builder.Services.AddStajDeskInfrastructure(builder.Configuration);

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var o = ConfigurationOptions.Parse(builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379");
    o.AbortOnConnectFail = false; // Redis kapalıyken de Worker çalışsın (gerçek zamanlı kanal atlanır)
    o.ConnectTimeout = 2000;
    return ConnectionMultiplexer.Connect(o);
});

// Bildirim kanalları (sıra önemli: önce kalıcı kayıt, sonra anlık itme ve e-posta).
// Yeni bir kanal (SMS, push) eklemek için INotificationChannel uygulamak yeterlidir.
builder.Services.AddScoped<INotificationChannel, InAppChannel>();
builder.Services.AddScoped<INotificationChannel, RealtimeChannel>();
builder.Services.AddScoped<INotificationChannel, EmailChannel>();
builder.Services.AddScoped<NotificationRouter>();
builder.Services.AddHostedService<NotificationConsumer>();

builder.Build().Run();
