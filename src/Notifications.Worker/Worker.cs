using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Notifications.Worker;

public class Worker(IConfiguration config, ILogger<Worker> logger) : BackgroundService
{
    private const string Exchange = "shipments";
    private const string Queue = "notifications.shipment-delivered";
    private const string RoutingKey = "shipment.delivered";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = config["RabbitMq:HostName"] ?? "localhost",
            UserName = config["RabbitMq:UserName"] ?? "shipments",
            Password = config["RabbitMq:Password"] ?? "devpassword"
        };

        using var connection = await factory.CreateConnectionAsync(stoppingToken);
        using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        // Our side of the setup: the exchange (same settings as the publisher), our own queue, and the binding
        await channel.ExchangeDeclareAsync(Exchange, ExchangeType.Topic, durable: true, cancellationToken: stoppingToken);
        await channel.QueueDeclareAsync(Queue, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
        await channel.QueueBindAsync(Queue, Exchange, RoutingKey, cancellationToken: stoppingToken);

        // One message at a time: the next arrives only after this one is confirmed
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var message = JsonSerializer.Deserialize<ShipmentDelivered>(ea.Body.ToArray(), JsonOptions)
                              ?? throw new InvalidOperationException("Empty message");

                // A real service would send an email here
                logger.LogInformation(
                    "Email to customer: your shipment {TrackingNumber} from {Origin} to {Destination} was delivered on {DeliveredAt:dd MMM yyyy HH:mm}.",
                    message.TrackingNumber, message.Origin, message.Destination, message.DeliveredAt);

                // Done: RabbitMQ can remove the message
                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                // Can't process it: reject it so a broken message doesn't block the queue forever
                logger.LogError(ex, "Could not process message {DeliveryTag}", ea.DeliveryTag);
                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
            }
        };

        await channel.BasicConsumeAsync(Queue, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);
        logger.LogInformation("Listening for {RoutingKey} on queue {Queue}", RoutingKey, Queue);

        // Keep running until the app is stopped
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
    }
}