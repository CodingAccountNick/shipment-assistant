using System.Text.Json;
using RabbitMQ.Client;

namespace Shipments.Api.Messaging;

public class RabbitMqPublisher(IConfiguration config, ILogger<RabbitMqPublisher> logger) : IDisposable
{
    public const string Exchange = "shipments";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private IConnection? _connection;
    private readonly SemaphoreSlim _connectLock = new(1, 1);

    public async Task PublishAsync<T>(string routingKey, T message, CancellationToken ct = default)
    {
        var connection = await GetConnectionAsync(ct);
        using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        // Make sure the exchange exists (does nothing if it already does)
        await channel.ExchangeDeclareAsync(Exchange, ExchangeType.Topic, durable: true, cancellationToken: ct);

        var body = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            Persistent = true   // survive a RabbitMQ restart
        };

        await channel.BasicPublishAsync(Exchange, routingKey, mandatory: false,
            basicProperties: properties, body: body, cancellationToken: ct);

        logger.LogInformation("Published {RoutingKey} to exchange {Exchange}", routingKey, Exchange);
    }

    // One connection for the whole app, created the first time it's needed
    private async Task<IConnection> GetConnectionAsync(CancellationToken ct)
    {
        if (_connection is { IsOpen: true })
            return _connection;

        await _connectLock.WaitAsync(ct);
        try
        {
            if (_connection is { IsOpen: true })
                return _connection;

            var factory = new ConnectionFactory
            {
                HostName = config["RabbitMq:HostName"] ?? "localhost",
                UserName = config["RabbitMq:UserName"] ?? "shipments",
                Password = config["RabbitMq:Password"] ?? "devpassword"
            };
            _connection = await factory.CreateConnectionAsync(ct);
            return _connection;
        }
        finally
        {
            _connectLock.Release();
        }
    }

    public void Dispose() => _connection?.Dispose();
}