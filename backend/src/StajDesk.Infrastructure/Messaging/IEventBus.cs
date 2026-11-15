using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;

namespace StajDesk.Infrastructure.Messaging;


/// <summary>
/// Olayı doğrudan RabbitMQ'ya göndermek yerine aynı veritabanı işleminde outbox tablosuna yazar.
/// Böylece RabbitMQ geçici olarak erişilemez olsa bile olay kaybolmaz.
/// </summary>
public interface IEventBus
{
    void Enqueue(IntegrationEvent evt);
}
