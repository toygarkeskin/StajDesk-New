using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;
using StajDesk.Infrastructure.Messaging;

namespace StajDesk.Api.Common;


/// <summary>Outbox'taki olayları 2 saniyede bir RabbitMQ'ya aktarır.</summary>
public class OutboxPublisherService(IServiceScopeFactory scopes, ILogger<OutboxPublisherService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<OutboxDispatcher>();
                var sent = await dispatcher.DispatchAsync(50, stoppingToken);
                if (sent == 50) continue; // biriken varsa beklemeden devam et
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning("Outbox aktarımı başarısız: {Error}", ex.Message);
            }
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }
}
