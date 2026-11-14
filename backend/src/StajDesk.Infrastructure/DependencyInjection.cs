using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;
using StajDesk.Infrastructure.Messaging;
using StajDesk.Infrastructure.Seed;

namespace StajDesk.Infrastructure;

public static class DependencyInjection
{
    /// <summary>API ve Worker'ın ortak kullandığı veri ve mesajlaşma altyapısı.</summary>
    public static IServiceCollection AddStajDeskInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<AuditInterceptor>();
        services.AddDbContext<AppDbContext>((sp, o) => o
            .UseNpgsql(config.GetConnectionString("Postgres"), npg => npg.EnableRetryOnFailure(3))
            .AddInterceptors(sp.GetRequiredService<AuditInterceptor>()));

        services.Configure<RabbitMqOptions>(config.GetSection("RabbitMq"));
        services.AddSingleton<RabbitMqPublisher>();
        services.AddScoped<IEventBus, OutboxEventBus>();
        services.AddScoped<OutboxDispatcher>();
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.Configure<Email.SmtpOptions>(config.GetSection("Smtp"));
        services.AddSingleton<Email.IEmailSender, Email.SmtpEmailSender>();
        services.AddScoped<DbSeeder>();
        return services;
    }
}
