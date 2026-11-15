using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;

namespace StajDesk.Infrastructure.Messaging;

public class RabbitMqOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string User { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";

    public ConnectionFactory CreateFactory(string clientName) => new()
    {
        HostName = Host, Port = Port, UserName = User, Password = Password, VirtualHost = VirtualHost,
        ClientProvidedName = clientName, AutomaticRecoveryEnabled = true, TopologyRecoveryEnabled = true,
    };
}
